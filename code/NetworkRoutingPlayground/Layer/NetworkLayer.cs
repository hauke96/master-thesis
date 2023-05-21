using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using NetworkRoutingPlayground.Model;
using ServiceStack.Text;
using HybridVisibilityGraphRouting;
using HybridVisibilityGraphRouting.Geometry;
using HybridVisibilityGraphRouting.Graph;
using HybridVisibilityGraphRouting.IO;
using ServiceStack;

namespace NetworkRoutingPlayground.Layer;

public class NetworkLayer : VectorLayer
{
    public HybridVisibilityGraph Graph { get; set; }

    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
        UnregisterAgent unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

        Graph = HybridVisibilityGraphGenerator.Generate(Features.Map(f => f.VectorStructured));
        
        Exporter.WriteGraphToFile(Graph.Graph);

        return true;
    }
}