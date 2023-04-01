using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using NetworkRoutingPlayground.Model;
using ServiceStack.Text;
using Wavefront;
using Wavefront.Geometry;

namespace NetworkRoutingPlayground.Layer;

public class NetworkLayer : VectorLayer
{
    public HybridVisibilityGraph Graph { get; set; }

    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
        UnregisterAgent unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

        Graph = HybridVisibilityGraphGenerator.Generate(Features);

        return true;
    }
}