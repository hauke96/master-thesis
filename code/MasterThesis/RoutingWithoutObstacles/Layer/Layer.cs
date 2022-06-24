using Mars.Components.Environments;
using Mars.Components.Environments.Cartesian;
using Mars.Components.Layers;
using Mars.Core.Data;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using Model;

namespace Layer
{
    public class Layer : VectorLayer
    {
        public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
            UnregisterAgent unregisterAgent = null)
        {
            var layerInitialized = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

            var agentManager = layerInitData.Container.Resolve<IAgentManager>();
            agentManager.Spawn<Character, Layer>().ToList();

            return layerInitialized;
        }
    }
}