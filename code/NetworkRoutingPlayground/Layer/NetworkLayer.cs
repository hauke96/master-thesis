using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Core.Data;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Interfaces.Model.Options;
using NetworkRoutingPlayground.Model;

namespace NetworkRoutingPlayground.Layer;

public class NetworkLayer : AbstractLayer, IDataLayer
{
    public ISpatialGraphEnvironment Environment { get; private set; }

    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
        UnregisterAgent unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

        var inputs = layerInitData.LayerInitConfig.Inputs;

        if (inputs != null && inputs.Any())
        {
            Environment = new SpatialGraphEnvironment(new SpatialGraphOptions
            {
                GraphImports = inputs
            });
        }
        
        // var agentManager = layerInitData.Container.Resolve<IAgentManager>();
        // var agents = agentManager.Spawn<Agent, NetworkLayer>().ToList();

        return true;
    }
}