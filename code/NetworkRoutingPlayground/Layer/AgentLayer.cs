using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Core.Data;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using NetworkRoutingPlayground.Model;
using HybridVisibilityGraphRouting;
using HybridVisibilityGraphRouting.Geometry;

namespace NetworkRoutingPlayground.Layer
{
    public class AgentLayer : AbstractLayer
    {
        public GeoHashEnvironment<Agent> Environment { get; private set; }

        public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
            UnregisterAgent unregisterAgent = null)
        {
            var layerInitialized = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

            var agentManager = layerInitData.Container.Resolve<IAgentManager>();
            agentManager.Spawn<Agent, AgentLayer>().ToList();

            return layerInitialized;
        }

        public void InitEnvironment(HybridVisibilityGraphRouting graph)
        {
            Environment = GeoHashEnvironment<Agent>.BuildByBBox(graph.BoundingBox, 1);
        }
    }
}