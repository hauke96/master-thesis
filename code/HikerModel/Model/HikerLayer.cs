using System.Linq;
using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Core.Data;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;

namespace HikerModel.Model
{
    public class HikerLayer : AbstractLayer
    {
        public GeoHashEnvironment<Hiker> Environment {get; private set;}
        
        public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
            UnregisterAgent unregisterAgent = null)
        {
            base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

            Environment = GeoHashEnvironment<Hiker>.BuildEnvironment(55, 47.25, 6, 15, 1);
        
            var agentManager = layerInitData.Container.Resolve<IAgentManager>();
            agentManager.Spawn<Hiker, HikerLayer>().ToList();
        
            return true;
        }
    }
}


