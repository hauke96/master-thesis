using Mars.Components.Layers;
using Mars.Core.Data;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using NetworkRoutingPlayground.Model;

namespace NetworkRoutingPlayground.Layer
{
    public class AgentLayer : VectorLayer
    {
        public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
            UnregisterAgent unregisterAgent = null)
        {
            var layerInitialized = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

            var agentManager = layerInitData.Container.Resolve<IAgentManager>();
            // var agents = agentManager.Spawn<Agent, AgentLayer>().ToList();
            
            return layerInitialized;
        }
    }
}