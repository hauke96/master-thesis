using Mars.Common;
using Mars.Interfaces.Environments;
using ServiceStack;
using Wavefront.Geometry;

namespace Wavefront;

public class Wavefront
{
    public double FromAngle { get; private set; }
    public double ToAngle { get; private set; }
    public Vertex RootVertex { get; }

    public List<Vertex> RelevantVertices => new(possiblyVisibleVertices);

    /// <summary>
    /// The distance from the source of the routing to the root of this event.
    /// </summary>
    public double DistanceToRootFromSource { get; }

    private Queue<Vertex> possiblyVisibleVertices;

    /// <summary>
    /// Creates a new wavefront if it's valid. A wavefront is *not* valid when there are no events ahead.
    /// </summary>
    public static Wavefront? newIfValid(double fromAngle, double toAngle, Vertex rootVertex, List<Vertex> allVertices,
        double distanceToRootFromSource)
    {
        var wavefront = new Wavefront(fromAngle, toAngle, rootVertex, distanceToRootFromSource);
        wavefront.FilterAndEnqueueVertices(allVertices);

        if (wavefront.possiblyVisibleVertices.Count == 0)
        {
            return null;
        }

        return wavefront;
    }

    private Wavefront(double fromAngle, double toAngle, Vertex rootVertex,
        double distanceToRootFromSource)
    {
        // TODO make sure that the angle area doesn't exceed the 0° line. But 0° to 360° should be allowed.

        FromAngle = fromAngle;
        ToAngle = toAngle;
        RootVertex = rootVertex;
        DistanceToRootFromSource = distanceToRootFromSource;

        // Get the vertices that are possibly visible. There's not collision detection here but all vertices are at
        // least within the range of this wavefront.
        possiblyVisibleVertices = new Queue<Vertex>();
    }

    private void FilterAndEnqueueVertices(List<Vertex> vertices)
    {
        vertices = vertices
            .FindAll(vertex =>
            {
                var bearing = RootVertex.Position.GetBearing(vertex.Position);
                return FromAngle <= bearing && bearing <= ToAngle;
            });
        vertices
            .Sort((v1, v2) =>
            {
                var distanceInMToC1 = RootVertex.Position.DistanceInMTo(Position.CreateGeoPosition(v1.X, v1.Y));
                var distanceInMToC2 = RootVertex.Position.DistanceInMTo(Position.CreateGeoPosition(v2.X, v2.Y));

                return distanceInMToC1 - distanceInMToC2 > 0
                    ? 1
                    : -1;
            });

        possiblyVisibleVertices.Clear();
        vertices.Each(vertex => possiblyVisibleVertices.Enqueue(vertex));
    }

    /// <summary>
    /// Peeks for the next vertex that would cause an VertexEvent. 
    /// </summary>
    public Vertex? GetNextVertex()
    {
        if (possiblyVisibleVertices.Count == 0)
        {
            return null;
        }

        return possiblyVisibleVertices.Peek();
    }

    public void RemoveNextVertex()
    {
        if (GetNextVertex() != null)
        {
            possiblyVisibleVertices.Dequeue();
        }
    }

    public double DistanceToNextVertex()
    {
        var nextVertex = GetNextVertex();
        if (nextVertex == null)
        {
            return 0;
        }

        return RootVertex.Position.DistanceInMTo(nextVertex.Position);
    }

    public void IgnoreVertex(Vertex vertex)
    {
        possiblyVisibleVertices = new Queue<Vertex>(possiblyVisibleVertices.Where(v => v != vertex));
    }

    public void SetAngles(double fromAngle, double toAngle)
    {
        // We want to reuse the Vertices field, therefore the angle area is not allowed to become larger but only smaller.
        if (fromAngle < FromAngle)
        {
            throw new Exception($"New fromAngle (-> {fromAngle}) is smaller than the old fromAngle (-> {FromAngle}.");
        }

        if (toAngle > toAngle)
        {
            throw new Exception($"New toAngle (-> {toAngle}) is larger than the old toAngle (-> {ToAngle}.");
        }

        FromAngle = fromAngle;
        ToAngle = toAngle;

        FilterAndEnqueueVertices(RelevantVertices);
        // TODO handle possiblyVisibleVertices is empty. Maybe return boolean?
    }
}