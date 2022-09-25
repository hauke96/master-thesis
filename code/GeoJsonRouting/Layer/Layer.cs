using GeoJsonRouting.Model;
using Mars.Components.Layers;
using Mars.Core.Data;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;

namespace GeoJsonRouting.Layer
{
    public class Layer : VectorLayer
    {
        public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
            UnregisterAgent unregisterAgent = null)
        {
            var layerInitialized = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

            var agentManager = layerInitData.Container.Resolve<IAgentManager>();
            var agents = agentManager.Spawn<Agent, Layer>().ToList();
            
            return layerInitialized;
        }
    }
}