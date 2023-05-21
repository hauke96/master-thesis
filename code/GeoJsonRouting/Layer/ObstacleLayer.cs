using System.Diagnostics;
using Mars.Common;
using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using ServiceStack;
using HybridVisibilityGraphRouting;
using HybridVisibilityGraphRouting.Geometry;
using HybridVisibilityGraphRouting.Graph;
using Position = Mars.Interfaces.Environments.Position;

namespace GeoJsonRouting.Layer
{
    public class ObstacleLayer : VectorLayer
    {
        private List<Position> _startPositions;
        private List<Position> _targetPositions;

        private readonly Random _random = new(DateTime.Now.ToString().GetHashCode());

        public HybridVisibilityGraph HybridVisibilityGraph { get; private set; }

        public ObstacleLayer()
        {
            _startPositions = new List<Position>();
            _targetPositions = new List<Position>();
        }

        public override bool InitLayer(
            LayerInitData layerInitData,
            RegisterAgent registerAgentHandle = null,
            UnregisterAgent unregisterAgent = null)
        {
            var initSuccessful = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
            if (!initSuccessful)
            {
                return false;
            }

            _startPositions = FindLocationsByKey("start");
            _targetPositions = FindLocationsByKey("target");

            var watch = Stopwatch.StartNew();
            HybridVisibilityGraph = HybridVisibilityGraphGenerator.Generate(Features.Map(f => f.VectorStructured));
            Console.WriteLine($"Graph creation: {watch.ElapsedMilliseconds}ms");

            return true;
        }

        public Position GetRandomStart()
        {
            return _startPositions[_random.Next(_startPositions.Count)].Copy();
        }

        public Position GetRandomTarget()
        {
            return _targetPositions[_random.Next(_targetPositions.Count)].Copy();
        }

        private List<Position> FindLocationsByKey(string attributeName)
        {
            return Features.Where(f => f.VectorStructured.Attributes.Exists(attributeName))
                .Map(f => f.VectorStructured.Geometry.Coordinates[0].ToPosition())
                .ToList();
        }
    }
}