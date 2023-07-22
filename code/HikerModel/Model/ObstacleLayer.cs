using System;
using System.Diagnostics;
using System.Linq;
using HybridVisibilityGraphRouting;
using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using ServiceStack;
using HybridVisibilityGraphRouting.Graph;
using HybridVisibilityGraphRouting.IO;

namespace HikerModel.Model
{
    public class ObstacleLayer : VectorLayer
    {
        public PerformanceMeasurement.Result GraphGenerationResult;

        public HybridVisibilityGraph HybridVisibilityGraph { get; private set; }

        public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
            UnregisterAgent unregisterAgent = null)
        {
            var initLayer = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
            if (!initLayer)
            {
                return false;
            }

            var features = Features.Map(f => f.VectorStructured).ToList();
            
            Console.WriteLine($"PID: {Process.GetCurrentProcess().Id}");
            Console.Write("Press ENTER to continue...");
            Console.Read();
            PerformanceMeasurement.TimestampBeforeGraphGeneration = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (PerformanceMeasurement.IsActive)
            {
                GraphGenerationResult = PerformanceMeasurement.NewMeasurementForFunction(
                    () =>
                    {
                        HybridVisibilityGraph = HybridVisibilityGraphGenerator.Generate(
                            features: features,
                            roadExpressions: HybridVisibilityGraphGenerator.DefaultRoadExpressions
                        );
                    },
                    "GenerateGraph");
                GraphGenerationResult.Print();
                GraphGenerationResult.WriteToFile();
            }
            else
            {
                HybridVisibilityGraph = HybridVisibilityGraphGenerator.Generate(
                    features: features,
                    roadExpressions: HybridVisibilityGraphGenerator.DefaultRoadExpressions
                );
            }
            PerformanceMeasurement.TimestampAfterGraphGeneration = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            Exporter.WriteGraphToFile(HybridVisibilityGraph.Graph);

            return true;
        }
    }
}