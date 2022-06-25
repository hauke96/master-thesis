using Mars.Components.Layers;
using Mars.Core.Data;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using RoutingWithoutObstacles.Model;

namespace RoutingWithoutObstacles.Layer
{
    public class Layer : VectorLayer
    {
        public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
            UnregisterAgent unregisterAgent = null)
        {
            var layerInitialized = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

            var agentManager = layerInitData.Container.Resolve<IAgentManager>();
            agentManager.Spawn<Agent, Layer>().ToList();

            // TODO Simple diagonal line as obstacle:
            // var vectorStructuredData = new VectorStructuredData();
            // vectorStructuredData.Geometry =
            //     new LineString(new[] { new Coordinate(0.02, 0.02), new Coordinate(0.07, 0.07) });
            // Insert(vectorStructuredData);

            return layerInitialized;
        }
    }
}