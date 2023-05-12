using System.Linq;
using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using ServiceStack;
using HybridVisibilityGraphRouting;
using HybridVisibilityGraphRouting.Geometry;

namespace HikerModel.Model
{
    public class ObstacleLayer : VectorLayer
    {
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

            // When performance measurement active -> Turn off performance measurements within the constructor call.
            // Below this if-block, the performance measurement is reactivated and the calls within the constructor will
            // be measured as intended.
            // When performance measurement inactive -> Skip this and just call the constructor below. It'll not measure
            // any internal calls, since measurement is disabled.
            if (PerformanceMeasurement.IsActive)
            {
                var result = PerformanceMeasurement.NewMeasurementForFunction(
                    () =>
                    {
                        // Deactivate measurement within constructor:
                        // PerformanceMeasurement.IsActive = false;
                        HybridVisibilityGraphGenerator.Generate(features);
                    },
                    "GenerateGraph", 5, 2);
                result.Print();
                result.WriteToFile();

                // PerformanceMeasurement.IsActive = true;
            }

            HybridVisibilityGraph = HybridVisibilityGraphGenerator.Generate(features);

            return true;
        }
    }
}