using Mars.Common;
using Mars.Interfaces.Environments;
using ServiceStack;
using Wavefront.Geometry;

namespace Wavefront;

public class Wavefront
{
    public double AngleFrom { get; }
    public double AngleTo { get; }
    public Vertex RootVertex { get; }

    /// <summary>
    /// The distance from the source of the routing to the root of this event.
    /// </summary>
    public double DistanceToRootFromSource { get; }

    private Queue<Vertex> possiblyVisibleVertices;

    public Wavefront(double angleFrom, double angleTo, Vertex rootVertex, List<Vertex> vertices,
        double distanceToRootFromSource)
    {
        AngleFrom = angleFrom;
        AngleTo = angleTo;
        RootVertex = rootVertex;
        DistanceToRootFromSource = distanceToRootFromSource;

        // Get the vertices that are possibly visible. There's not collision detection here but all vertices are at
        // least within the range of this wavefront.
        possiblyVisibleVertices = new Queue<Vertex>();
        vertices
            .FindAll(vertex =>
            {
                var bearing = RootVertex.Position.GetBearing(vertex.Position);
                return AngleFrom <= bearing && bearing <= AngleTo;
            })
            .Sort((v1, v2) =>
            {
                var distanceInMToC1 = RootVertex.Position.DistanceInMTo(Position.CreateGeoPosition(v1.X, v1.Y));
                var distanceInMToC2 = RootVertex.Position.DistanceInMTo(Position.CreateGeoPosition(v2.X, v2.Y));

                return distanceInMToC1 - distanceInMToC2 > 0
                    ? 1
                    : -1;
            });
        vertices.Each(vertex => possiblyVisibleVertices.Enqueue(vertex));
    }

    /// <summary>
    /// Peeks for the next vertex that would cause an VertexEvent. 
    /// </summary>
    public Vertex GetNextVertex()
    {
        return possiblyVisibleVertices.Peek();
    }

    public double DistanceToNextVertex()
    {
        return RootVertex.Position.DistanceInMTo(GetNextVertex().Position);
    }

    public void IgnoreVertex(Vertex vertex)
    {
        possiblyVisibleVertices = new Queue<Vertex>(possiblyVisibleVertices.Where(v => v != vertex));
    }
}