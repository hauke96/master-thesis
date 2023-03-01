using Mars.Interfaces.Environments;
using Mars.Numerics;
using Wavefront.Geometry;

namespace Wavefront;

public class Wavelet
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
    private readonly double _fromAngleNormalized;
    private readonly double _toAngleNormalized;

    // ReSharper disable InvalidXmlDocComment
    /// <summary>
    /// Creates a new wavelet if it's valid. A wavelet is *not* valid when there are no events ahead.
    ///
    /// Precondition: 0 <= fromAngle < toAngle <= 360
    /// <returns>
    /// The new wavelet or null if the new wavelet would not be valid.
    /// </returns>
    /// </summary>
    public static Wavelet? New(double fromAngle, double toAngle, Vertex rootVertex, ICollection<Vertex> allVertices,
        double distanceToRootFromSource, bool verticesFromWaveletWithSameRoot)
    {
        // TODO enforce precondition
        var wavelet = new Wavelet(fromAngle, toAngle, rootVertex, distanceToRootFromSource);
        wavelet.FilterAndEnqueueVertices(allVertices, verticesFromWaveletWithSameRoot);

        if (wavelet.RelevantVertices.Count == 0)
        {
            return null;
        }

        return wavelet;
    }

    private Wavelet(double fromAngle, double toAngle, Vertex rootVertex,
        double distanceToRootFromSource)
    {
        FromAngle = fromAngle;
        _fromAngleNormalized = Angle.StrictNormalize(FromAngle);
        ToAngle = toAngle;
        _toAngleNormalized = Angle.StrictNormalize(ToAngle);
        RootVertex = rootVertex;
        DistanceToRootFromSource = distanceToRootFromSource;

        // Get the vertices that are possibly visible. There's not collision detection here but all vertices are at
        // least within the range of this wavelet.
        RelevantVertices = new LinkedList<Vertex>();
        _visitedVertices = new LinkedList<Position>();
    }

    private void FilterAndEnqueueVertices(ICollection<Vertex> vertices, bool verticesFromWaveletWithSameRoot)
    {
        vertices = vertices.Distinct().ToList();

        foreach (var vertex in vertices)
        {
            var bearing = Angle.GetBearing(RootVertex.Position, vertex.Position);
            if (IsRelevant(vertex, bearing))
            {
                RelevantVertices.AddLast(vertex);
            }
        }

        if (!verticesFromWaveletWithSameRoot)
        {
            // If the wavelet come from a wavelet with the same root vertex, we don't need to sort anything because
            // the list is already sorted. But here we have vertices from a different source and therefore we must
            // sort them.
            RelevantVertices = new LinkedList<Vertex>(RelevantVertices.OrderBy(vertex =>
                Distance.Euclidean(RootVertex.Position.PositionArray, vertex.Position.PositionArray)));
        }

        UpdateDistanceToNextVertex();
    }

    private bool IsRelevant(Vertex vertex, double bearing)
    {
        if (Equals(RootVertex, vertex))
        {
            return false;
        }

        // The Angle.IsBetweenEqual assumes normalized angles. If the from-angle is 0° and/or the to-angle is 360°, this
        // might cause problems with vertices at these angles.
        return Angle.IsBetweenEqual(FromAngle, bearing, ToAngle) ||
               Angle.IsBetweenEqual(_fromAngleNormalized, bearing, _toAngleNormalized);
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
        }

        UpdateDistanceToNextVertex();
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

    public override String ToString()
    {
        return $"(root={RootVertex}, from={FromAngle}, to={ToAngle}, vertices={RelevantVertices.Count})";
    }
}