using Mars.Common;
using Mars.Interfaces.Environments;
using ServiceStack;
using Wavefront.Geometry;

namespace Wavefront;

public class Wavefront
{
    public double FromAngle { get; }
    public double ToAngle { get; }
    public Vertex RootVertex { get; }

    public List<Vertex> RelevantVertices => new(_possiblyVisibleVertices);

    /// <summary>
    /// The distance from the source of the routing to the root of this event.
    /// </summary>
    public double DistanceToRootFromSource { get; }

    private Queue<Vertex> _possiblyVisibleVertices;
    private readonly List<Position> _visitedVertices;

    /// <summary>
    /// Creates a new wavefront if it's valid. A wavefront is *not* valid when there are no events ahead.
    /// <returns>
    /// The new wavefront or null if the new wavefront would not be valid.
    /// </returns>
    /// </summary>
    public static Wavefront? New(double fromAngle, double toAngle, Vertex rootVertex, List<Vertex> allVertices,
        double distanceToRootFromSource)
    {
        var wavefront = new Wavefront(fromAngle, toAngle, rootVertex, distanceToRootFromSource);
        wavefront.FilterAndEnqueueVertices(rootVertex, allVertices);

        if (wavefront._possiblyVisibleVertices.Count == 0)
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
        _possiblyVisibleVertices = new Queue<Vertex>();
        _visitedVertices = new List<Position>();
    }

    private void FilterAndEnqueueVertices(Vertex rootVertex, List<Vertex> vertices)
    {
        vertices = vertices
            .FindAll(vertex =>
            {
                var bearing = RootVertex.Position.GetBearing(vertex.Position);
                return !Equals(rootVertex, vertex) && Angle.LowerEqual(FromAngle, bearing) && Angle.LowerEqual(bearing, ToAngle);
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

        _possiblyVisibleVertices.Clear();
        vertices.Each(vertex => _possiblyVisibleVertices.Enqueue(vertex));
    }

    /// <summary>
    /// Peeks for the next vertex that would cause an VertexEvent. 
    /// </summary>
    public Vertex? GetNextVertex()
    {
        if (_possiblyVisibleVertices.Count == 0)
        {
            return null;
        }

        return _possiblyVisibleVertices.Peek();
    }

    public void RemoveNextVertex()
    {
        if (GetNextVertex() != null)
        {
            var vertex = _possiblyVisibleVertices.Dequeue();
            _visitedVertices.Add(vertex.Position);
        }
    }

    public bool HasBeenVisited(Position? position)
    {
        return position != null && _visitedVertices.Contains(position);
    }

    public double DistanceToNextVertex()
    {
        var nextVertex = GetNextVertex();
        if (nextVertex == null)
        {
            return 0;
        }

        return DistanceTo(nextVertex.Position);
    }

    public double DistanceTo(Position position)
    {
        return DistanceToRootFromSource + RootVertex.Position.DistanceInMTo(position);
    }

    public void IgnoreVertex(Vertex vertex)
    {
        _possiblyVisibleVertices = new Queue<Vertex>(_possiblyVisibleVertices.Where(v => v != vertex));
    }
}