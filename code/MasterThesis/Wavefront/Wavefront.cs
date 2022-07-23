using Mars.Common;
using Mars.Interfaces.Environments;
using Mars.Numerics;
using Wavefront.Geometry;

namespace Wavefront;

public class Wavefront
{
    public double FromAngle { get; }
    public double ToAngle { get; }
    public Vertex RootVertex { get; }
    public double DistanceToNextVertex { get; private set; }

    /// <summary>
    /// The distance from the source of the routing to the root of this event.
    /// </summary>
    public double DistanceToRootFromSource { get; }

    public LinkedList<Vertex> RelevantVertices;
    private readonly LinkedList<Position> _visitedVertices;

    /// <summary>
    /// Creates a new wavefront if it's valid. A wavefront is *not* valid when there are no events ahead.
    /// <returns>
    /// The new wavefront or null if the new wavefront would not be valid.
    /// </returns>
    /// </summary>
    public static Wavefront? New(double fromAngle, double toAngle, Vertex rootVertex, ICollection<Vertex> allVertices,
        double distanceToRootFromSource)
    {
        var wavefront = new Wavefront(fromAngle, toAngle, rootVertex, distanceToRootFromSource);
        wavefront.FilterAndEnqueueVertices(allVertices);

        if (wavefront.RelevantVertices.Count == 0)
        {
            return null;
        }

        return wavefront;
    }

    private Wavefront(double fromAngle, double toAngle, Vertex rootVertex,
        double distanceToRootFromSource)
    {
        FromAngle = fromAngle;
        ToAngle = toAngle;
        RootVertex = rootVertex;
        DistanceToRootFromSource = distanceToRootFromSource;

        // Get the vertices that are possibly visible. There's not collision detection here but all vertices are at
        // least within the range of this wavefront.
        RelevantVertices = new LinkedList<Vertex>();
        _visitedVertices = new LinkedList<Position>();
    }

    private void FilterAndEnqueueVertices(ICollection<Vertex> vertices)
    {
        foreach (var vertex in vertices)
        {
            var bearing = RootVertex.Position.GetBearing(vertex.Position);
            var relevant = !Equals(RootVertex, vertex) && (Angle.IsBetween(FromAngle, bearing, ToAngle) ||
                                                           Angle.AreEqual(FromAngle, bearing) ||
                                                           Angle.AreEqual(ToAngle, bearing));
            if (relevant)
            {
                RelevantVertices.AddFirst(vertex);
            }
        }

        RelevantVertices = new LinkedList<Vertex>(RelevantVertices.OrderBy(vertex =>
            Distance.Euclidean(RootVertex.Position.PositionArray, vertex.Position.PositionArray)));

        UpdateDistanceToNextVertex();
    }

    /// <summary>
    /// Peeks for the next vertex that would cause an VertexEvent. 
    /// </summary>
    public Vertex? GetNextVertex()
    {
        return RelevantVertices.First?.Value;
    }

    public void RemoveNextVertex()
    {
        var nextVertex = GetNextVertex();
        if (nextVertex != null)
        {
            _visitedVertices.AddLast(nextVertex.Position);
            RelevantVertices.RemoveFirst();
            UpdateDistanceToNextVertex();
        }
    }

    public bool HasBeenVisited(Position? position)
    {
        return position != null && _visitedVertices.Contains(position);
    }

    private void UpdateDistanceToNextVertex()
    {
        var nextVertex = GetNextVertex();
        if (nextVertex == null)
        {
            DistanceToNextVertex = 0;
        }
        else
        {
            DistanceToNextVertex = DistanceTo(nextVertex.Position);
        }
    }

    public double DistanceTo(Position position)
    {
        // The euclidean distance is much faster and will probably work for nearly all real world cases.
        return DistanceToRootFromSource + Distance.Euclidean(RootVertex.Position.PositionArray, position.PositionArray);
    }
}