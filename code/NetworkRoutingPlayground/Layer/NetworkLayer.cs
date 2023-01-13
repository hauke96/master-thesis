using Mars.Components.Layers;
using Mars.Interfaces.Environments;

namespace NetworkRoutingPlayground.Layer;

public class NetworkLayer : VectorLayer
{
    public ISpatialGraphEnvironment Environment { get; }
}