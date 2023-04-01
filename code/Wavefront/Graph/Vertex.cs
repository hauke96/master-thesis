using NetTopologySuite.Geometries;
using ServiceStack;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront.Geometry;

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

    /// <summary>
    /// Should only be used to create a clone of a vertex.
    /// </summary>
    private Vertex(Position position, List<Position> obstacleNeighbors, int id)
    {
        Position = position;
        ObstacleNeighbors = obstacleNeighbors;
        Id = id;
    }

    public Vertex clone()
    {
        return new Vertex(Position, ObstacleNeighbors, Id);
    }

    /// <summary>
    /// Returns the neighbor that's right (=clockwise) or equal to the angle of the given position.
    /// </summary>
    /// <param name="basePosition">The reference position used to determine the right neighbor in clockwise direction.</param>
    /// <param name="basePositionIsRightMostNeighbor">Pass true when the given basePosition is maybe itself the right
    /// most neighbor of all. This is important to know when the wavelet root is one of the neighbors of this vertex:
    /// The right neighbor depends on the fact if the angle area of that wavelet starts at this vertex or not.</param>
    public Position? RightNeighbor(Position basePosition, bool basePositionIsRightMostNeighbor = false)
    {
        var basePositionIsANeighbor = ObstacleNeighbors.Contains(basePosition);
        if (basePositionIsANeighbor && basePositionIsRightMostNeighbor)
        {
            return basePosition;
        }

        var vertexToBasePositionAngle = Angle.GetBearing(Position, basePosition);
        var rotatedNeighborAngles = ObstacleNeighbors
            .Map(n => Angle.Normalize(Angle.GetBearing(Position, n) - vertexToBasePositionAngle));

        var minAngle = double.PositiveInfinity;
        var index = -1;
        rotatedNeighborAngles.Each((i, a) =>
        {
            // Treat 0° as 360°. If the basePosition is a neighbor, then "a" is 0° for that neighbor. A part of this
            // situation is already handled above but this ensures that the real right neighbor is returned and not just
            // the given basePosition just because its angle of 0° is the lowest.
            a = a == 0 ? 360 : a;
            if (a < minAngle)
            {
                minAngle = a;
                index = i;
            }
        });

        return 0 <= index && index < ObstacleNeighbors.Count ? ObstacleNeighbors[index] : null;
    }


    /// <summary>
    /// Returns the neighbor that's left (=counter clockwise) or equal to the angle of the given position.
    /// </summary>
    /// <param name="basePosition">The reference position used to determine the left neighbor in counter clockwise
    /// direction.</param>
    /// <param name="basePositionIsLeftMostNeighbor">Pass true when the given basePosition is maybe itself the left
    /// most neighbor of all. This is important to know when the wavelet root is one of the neighbors of this vertex:
    /// The left neighbor depends on the fact if the angle area of that wavelet ends at this vertex or not.</param>
    public Position? LeftNeighbor(Position basePosition, bool basePositionIsLeftMostNeighbor = false)
    {
        var basePositionIsANeighbor = ObstacleNeighbors.Contains(basePosition);
        if (basePositionIsANeighbor && basePositionIsLeftMostNeighbor)
        {
            return basePosition;
        }

        var vertexToBasePositionAngle = Angle.GetBearing(Position, basePosition);
        var rotatedNeighborAngles = ObstacleNeighbors
            .Map(n => Angle.Normalize(Angle.GetBearing(Position, n) - vertexToBasePositionAngle));

        var maxAngle = double.NegativeInfinity;
        var index = -1;
        rotatedNeighborAngles.Each((i, a) =>
        {
            if (a > maxAngle)
            {
                maxAngle = a;
                index = i;
            }
        });

        return 0 <= index && index < ObstacleNeighbors.Count ? ObstacleNeighbors[index] : null;
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