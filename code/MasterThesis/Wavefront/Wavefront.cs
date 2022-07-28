using System.Collections;
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

    public SortedLinkedList<Vertex> RelevantVertices;
    private readonly LinkedList<Position> _visitedVertices;

    /// <summary>
    /// Creates a new wavefront if it's valid. A wavefront is *not* valid when there are no events ahead.
    ///
    /// Precondition: 0 <= fromAngle < toAngle <= 360
    /// <returns>
    /// The new wavefront or null if the new wavefront would not be valid.
    /// </returns>
    /// </summary>
    public static Wavefront? New(double fromAngle, double toAngle, Vertex rootVertex, ICollection<Vertex> allVertices,
        double distanceToRootFromSource)
    {
        // TODO enforce precondition
        var wavefront = new Wavefront(fromAngle, toAngle, rootVertex, distanceToRootFromSource);
        if (allVertices is SortedLinkedList<Vertex>)
        {
            wavefront.FilterAndEnqueueVertices((SortedLinkedList<Vertex>)allVertices);
        }
        else
        {
            wavefront.FilterAndEnqueueVertices(allVertices);
        }

        if (wavefront.RelevantVertices.Count == 0)
        {
            return null;
        }

        return wavefront;
    }

    private Wavefront(double fromAngle, double toAngle, Vertex rootVertex, double distanceToRootFromSource)
    {
        FromAngle = fromAngle;
        ToAngle = toAngle;
        RootVertex = rootVertex;
        DistanceToRootFromSource = distanceToRootFromSource;

        // Get the vertices that are possibly visible. There's not collision detection here but all vertices are at
        // least within the range of this wavefront.
        RelevantVertices = new SortedLinkedList<Vertex>(5);
        _visitedVertices = new LinkedList<Position>();
    }

    private void FilterAndEnqueueVertices(ICollection<Vertex> vertices)
    {
        foreach (var vertex in vertices)
        {
            var bearing = Angle.GetBearing(RootVertex.Position, vertex.Position);
            if (IsRelevant(vertex, bearing))
            {
                RelevantVertices.Add(vertex,
                    Distance.Euclidean(RootVertex.Position.PositionArray, vertex.Position.PositionArray), bearing);
            }
        }

        UpdateDistanceToNextVertex();
    }

    /// <summary>
    /// Same as FilterAndEnqueueVertices(ICollection<Vertex>) but with one very important (!) assumption:
    ///
    /// The given list MUST come from a wavefront with the same root vertex!
    ///
    /// This method here re-uses the bearing and also uses the sorting of the given list. This only works if the
    /// wavefront these vertices came from has the same root vertex. 
    /// </summary>
    private void FilterAndEnqueueVertices(SortedLinkedList<Vertex> vertices)
    {
        var node = vertices.First;
        while (node != null)
        {
            var vertex = node.Value.Value;
            var bearing = node.Value.BearingFromWavefront;
            if (IsRelevant(vertex, bearing))
            {
                RelevantVertices.AddLast(node.Value);
            }

            node = node.Next;
        }

        UpdateDistanceToNextVertex();
    }

    private bool IsRelevant(Vertex vertex, double bearing)
    {
        if (bearing == 0 && Equals(RootVertex, vertex))
        {
            // 0° is an indicator that the vertex might be the current root -> therefore not relevant
            return false;
        }

        return Angle.IsBetweenEqual(FromAngle, bearing, ToAngle);
    }

    /// <summary>
    /// Peeks for the next vertex that would cause an VertexEvent. 
    /// </summary>
    public Vertex? GetNextVertex()
    {
        return RelevantVertices.First?.Value.Value;
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