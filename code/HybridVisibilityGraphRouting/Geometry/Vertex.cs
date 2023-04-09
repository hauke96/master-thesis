using NetTopologySuite.Geometries;
using ServiceStack;
using Position = Mars.Interfaces.Environments.Position;

namespace HybridVisibilityGraphRouting.Geometry;

public class Vertex
{
    private static int ID_COUNTER;

    /// <summary>
    /// The neighbors are neighboring vertices on obstacles but not across open spaces. These are not the visible
    /// vertices one might obtain by running the knn-search to find all n many visible neighbors. This list is sorted by
    /// the bearing of each position.
    /// In other words: There is an edge from this vertex to these neighboring vertices in the (preprocessed) dataset. 
    /// </summary>
    public List<Position> ObstacleNeighbors { get; }

    private readonly Position _position;
    public int Id { get; }

    public Position Position
    {
        get => _position;

        private init
        {
            _position = value;
            Coordinate = new Coordinate(_position.X, _position.Y);
        }
    }

    public Coordinate Coordinate { get; private init; }

    public double X => Position.X;
    public double Y => Position.Y;

    public Vertex(double x, double y) : this(Position.CreateGeoPosition(x, y), new List<Position>())
    {
    }

    public Vertex(Position position) : this(position, new List<Position>())
    {
    }

    /// <param name="obstacleNeighbors">
    /// The neighbors are neighboring vertices on obstacles but not across open spaces. These are not the visible
    /// vertices one might obtain by running the knn-search to find all n many visible neighbors.
    /// </param>
    public Vertex(Position position, params Position[] obstacleNeighbors) : this(position, obstacleNeighbors.ToList())
    {
    }

    /// <param name="obstacleNeighbors">
    /// The neighbors are neighboring vertices on obstacles but not across open spaces. These are not the visible
    /// vertices one might obtain by running the knn-search to find all n many visible neighbors.
    /// </param>
    public Vertex(Position position, List<Position> obstacleNeighbors)
    {
        Position = position;
        obstacleNeighbors.ForEach(neighbor => neighbor.Bearing = Angle.GetBearing(Position, neighbor));
        ObstacleNeighbors = obstacleNeighbors.OrderBy(neighbor => neighbor.Bearing).ToList();
        Id = ID_COUNTER++;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Vertex);
    }

    private bool Equals(Vertex? other)
    {
        return other != null && Position.Equals(other.Position);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Position);
    }

    public override string ToString()
    {
        return "v#" + Id + " : " + Position;
    }
}