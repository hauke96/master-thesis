using NetTopologySuite.Geometries;
using ServiceStack;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront.Geometry;

public class Vertex
{
    private readonly List<Position> _neighbors;
    private readonly Position _position;

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

    public Vertex(Position position, params Position[] neighbors) : this(position, neighbors.ToList())
    {
    }

    public Vertex(Position position, List<Position> neighbors)
    {
        Position = position;
        _neighbors = neighbors;
        _neighbors.Sort((p1, p2) => (int)(Angle.GetBearing(Position, p1) - Angle.GetBearing(Position, p2)));
    }

    /// <summary>
    /// Returns the neighbor that's right (=clockwise) or equal to the angle of the given position.
    /// </summary>
    public Position? RightNeighbor(Position basePosition)
    {
        var vertexToBasePositionAngle = Angle.GetBearing(Position, basePosition);
        var rotatedNeighborAngles = _neighbors
            .Map(n => Angle.Normalize(Angle.GetBearing(Position, n) - vertexToBasePositionAngle));

        var minAngle = double.PositiveInfinity;
        var index = -1;
        rotatedNeighborAngles.Each((i, a) =>
        {
            if (a < minAngle)
            {
                minAngle = a;
                index = i;
            }
        });

        return 0 <= index && index < _neighbors.Count ? _neighbors[index] : null;
    }

    /// <summary>
    /// Returns the neighbor that's left (=counter clockwise) or equal to the angle of the given position.
    /// </summary>
    public Position? LeftNeighbor(Position basePosition)
    {
        var vertexToBasePositionAngle = Angle.GetBearing(Position, basePosition);
        var rotatedNeighborAngles = _neighbors
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

        return 0 <= index && index < _neighbors.Count ? _neighbors[index] : null;
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
        return Position.ToString();
    }
}