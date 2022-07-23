using Mars.Common;
using Mars.Interfaces.Environments;
using Mars.Numerics;
using ServiceStack;
using Wavefront.Geometry;

namespace Wavefront;

public class Wavefront
{
    public double FromAngle { get; }
    public double ToAngle { get; }
    public Vertex RootVertex { get; }

    /// <summary>
    /// The distance from the source of the routing to the root of this event.
    /// </summary>
    public double DistanceToRootFromSource { get; }

    public List<Vertex> RelevantVertices;
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
        RelevantVertices = new List<Vertex>();
        _visitedVertices = new List<Position>();
    }

    private void FilterAndEnqueueVertices(Vertex rootVertex, List<Vertex> vertices)
    {
        vertices = vertices
            .FindAll(vertex =>
            {
                var bearing = RootVertex.Position.GetBearing(vertex.Position);
                return !Equals(rootVertex, vertex) && (Angle.IsBetween(FromAngle, bearing, ToAngle) ||
                                                       Angle.AreEqual(FromAngle, bearing) ||
                                                       Angle.AreEqual(ToAngle, bearing));
            });
        vertices
            .Sort((v1, v2) =>
            {
                var distanceInMToC1 = Distance.Euclidean(RootVertex.Position.PositionArray,
                    Position.CreateGeoPosition(v1.X, v1.Y).PositionArray);
                var distanceInMToC2 = Distance.Euclidean(RootVertex.Position.PositionArray,
                    Position.CreateGeoPosition(v2.X, v2.Y).PositionArray);

                return (int)(distanceInMToC2 - distanceInMToC1);
            });

        RelevantVertices.Clear();
        vertices.Each(vertex => RelevantVertices.Add(vertex));
    }

    /// <summary>
    /// Peeks for the next vertex that would cause an VertexEvent. 
    /// </summary>
    public Vertex? GetNextVertex()
    {
        if (RelevantVertices.Count == 0)
        {
            return null;
        }

        return RelevantVertices.Last();
    }

    public void RemoveNextVertex()
    {
        _visitedVertices.Add(RelevantVertices.Last().Position);
        RelevantVertices.RemoveAt(RelevantVertices.Count - 1);
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
        // The euclidean distance is much faster and will probably work for nearly all real world cases.
        return DistanceToRootFromSource + Distance.Euclidean(RootVertex.Position.PositionArray, position.PositionArray);
    }
}