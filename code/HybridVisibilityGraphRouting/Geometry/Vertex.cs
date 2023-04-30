using NetTopologySuite.Geometries;
using Position = Mars.Interfaces.Environments.Position;

namespace HybridVisibilityGraphRouting.Geometry;

public class Vertex
{
    private static int ID_COUNTER;

    private int Id { get; }

    /// <summary>
    /// The neighbors are neighboring vertices on obstacles but not across open spaces. These are not the visible
    /// vertices one might obtain by running the knn-search to find all n many visible neighbors. This list is sorted by
    /// the bearing of each position.
    /// In other words: There is an edge from this vertex to these neighboring vertices in the (preprocessed) dataset. 
    /// </summary>
    public List<Position> ObstacleNeighbors { get; }

    public Coordinate Coordinate { get; }

    public Vertex(Coordinate coordinate) : this(coordinate, new List<Position>())
    {
    }

    /// <param name="obstacleNeighbors">
    /// The neighbors are neighboring vertices on obstacles but not across open spaces. These are not the visible
    /// vertices one might obtain by running the knn-search to find all n many visible neighbors.
    /// </param>
    // TODO Only used in test code -> Merge with constructor above?
    public Vertex(Coordinate coordinate, IEnumerable<Position> obstacleNeighbors)
    {
        Coordinate = coordinate;
        ObstacleNeighbors = obstacleNeighbors.ToList();
        Id = ID_COUNTER++;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Vertex);
    }

    private bool Equals(Vertex? other)
    {
        return other != null && Coordinate.Equals(other.Coordinate);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Coordinate);
    }

    public override string ToString()
    {
        return "v#" + Id + " : " + Coordinate;
    }
}