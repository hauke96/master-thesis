using System.Diagnostics;
using GeoJsonRouting.Layer;
using Mars.Components.Environments.Cartesian;
using Mars.Components.Layers;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Numerics;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NetTopologySuite.IO;
using NetTopologySuite.IO.Converters;
using Newtonsoft.Json;
using ServiceStack;
using Wavefront;
using Wavefront.Geometry;
using Wavefront.IO;
using Feature = NetTopologySuite.Features.Feature;
using GeometryFactory = NetTopologySuite.Geometries.GeometryFactory;
using Position = Mars.Interfaces.Environments.Position;

namespace GeoJsonRouting.Model
{
    public class Agent : ICharacter, IAgent<VectorLayer>
    {
        private static readonly double STEP_SIZE = 0.00001;

        [PropertyDescription] public UnregisterAgent UnregisterHandle { get; set; }
        [PropertyDescription] public ObstacleLayer ObstacleLayer { get; set; }

        public Position? Position { get; set; }
        public Guid ID { get; set; } = Guid.NewGuid();
        public double Extent { get; set; } = 0.0002; // -> Umrechnung in lat/lon-differenz für Euklidische Distanz

        private Position? _targetPosition;
        private Queue<Waypoint> _waypoints = new();

        public void Init(VectorLayer layer)
        {
            Position = ObstacleLayer.GetRandomStart();
            _targetPosition = ObstacleLayer.GetRandomTarget();

            while (_targetPosition.Equals(Position))
            {
                _targetPosition = ObstacleLayer.GetRandomTarget();
            }

            DetermineNewWaypoints();

            SharedEnvironment.Environment.Insert(this, Position);
        }

        public void Tick()
        {
            try
            {
                TickInternal();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void TickInternal()
        {
            var currentWaypoint = _waypoints.Peek();

            var distanceToTarget = Distance.Euclidean(currentWaypoint.Position.PositionArray, Position.PositionArray);
            if (distanceToTarget <= STEP_SIZE)
            {
                _waypoints.Dequeue();

                // The current waypoint was the last one -> we're done
                if (_waypoints.Count == 0)
                {
                    Kill();
                }

                return;
            }

            var bearing = Angle.GetBearing(Position, currentWaypoint.Position);
            var oldPosition = (Position)Position.Clone();
            for (int i = 0; i < 4; i++)
            {
                var newPosition =
                    SharedEnvironment.Environment.Move(this, (bearing + i * 45) % 360, STEP_SIZE);
                var distanceInMTo = Distance.Euclidean(oldPosition.PositionArray, Position.PositionArray);
                // Console.WriteLine($"{distanceInMTo}");
                if (!newPosition.Equals(oldPosition) && distanceInMTo >= 0.5 * STEP_SIZE)
                {
                    break;
                }
                else
                {
                    Console.WriteLine("Try another direction");
                }
            }
            // TODO Wenn nicht bewegt ggf. andere Position anlaufen
        }

        public CollisionKind? HandleCollision(ICharacter other)
        {
            var distanceInMTo = other.Position.DistanceInMTo(Position);
            return distanceInMTo <= 5 ? CollisionKind.Block : CollisionKind.Pass;
            // return CollisionKind.Pass;
        }

        private void Kill()
        {
            Console.WriteLine("Agent reached target");
            SharedEnvironment.Environment.Remove(this);
            UnregisterHandle.Invoke(ObstacleLayer, this);
        }

        private void DetermineNewWaypoints()
        {
            try
            {
                var watch = Stopwatch.StartNew();
                var routingResult = ObstacleLayer.HybridGeometricRouter.RouteLegacy(Position, _targetPosition);
                watch.Stop();
                Console.WriteLine($"Routing duration: {watch.ElapsedMilliseconds}ms");

                if (routingResult.OptimalRoute.IsEmpty())
                {
                    throw new Exception($"No route found from {Position} to {_targetPosition}");
                }

                _waypoints = new Queue<Waypoint>(routingResult.OptimalRoute);

                Exporter.WriteRoutesToFile(routingResult.AllRoutes);
                Exporter.WriteVisitedPositionsToFile(routingResult.AllRoutes);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}