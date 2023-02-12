using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using ServiceStack;
using Wavefront;
using Wavefront.Geometry;

namespace HikerModel.Model
{
    public class ObstacleLayer : VectorLayer
    {
        public WavefrontAlgorithm WavefrontAlgorithm { get; private set; }

        public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
            UnregisterAgent unregisterAgent = null)
        {
            var initLayer = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
            if (!initLayer)
            {
                return false;
            }

            var obstacleGeometries = Features.Map(f => new Obstacle(f.VectorStructured.Geometry));

            var actualIsActiveState = PerformanceMeasurement.IS_ACTIVE;

            // Measure overall constructor performance. Detailed performance measurements within the constructor will
            // be deactivated here but (possibly) re-activated below.
            var result = PerformanceMeasurement.ForFunction(
                () =>
                {
                    // Do not measure the performance within the constructor call, because then pre-processing functions
                    // are probably executed several times to measure their performance. This will distort the
                    // measurement of the overall constructor performance.
                    PerformanceMeasurement.IS_ACTIVE = false;
                    WavefrontAlgorithm = new WavefrontAlgorithm(obstacleGeometries);
                    PerformanceMeasurement.IS_ACTIVE = actualIsActiveState;
                },
                "WavefrontAlgorithmCreation");
            result.Print();
            result.WriteToFile();

            return true;
        }
    }
}