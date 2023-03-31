using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using ServiceStack;
using Wavefront;

namespace HikerModel.Model
{
    public class ObstacleLayer : VectorLayer
    {
        public HybridGeometricRouter HybridGeometricRouter { get; private set; }

        public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
            UnregisterAgent unregisterAgent = null)
        {
            var initLayer = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
            if (!initLayer)
            {
                return false;
            }

            // When performance measurement active -> Turn off performance measurements within the constructor call.
            // Below this if-block, the performance measurement is reactivated and the calls within the constructor will
            // be measured as intended.
            // When performance measurement inactive -> Skip this and just call the constructor below. It'll not measure
            // any internal calls, since measurement is disabled.
            if (PerformanceMeasurement.IS_ACTIVE)
            {
                var result = PerformanceMeasurement.ForFunction(
                    () =>
                    {
                        // Deactivate measurement within constructor:
                        PerformanceMeasurement.IS_ACTIVE = false;
                        new HybridGeometricRouter(Features.Map(f => f.VectorStructured));
                    },
                    "WavefrontAlgorithmCreation");
                result.Print();
                result.WriteToFile();

                PerformanceMeasurement.IS_ACTIVE = true;
            }

            HybridGeometricRouter = new HybridGeometricRouter(Features.Map(f => f.VectorStructured), true);

            return true;
        }
    }
}