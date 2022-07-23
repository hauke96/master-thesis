using Mars.Common;
using NetTopologySuite.Geometries;
using ServiceStack;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront.Geometry;

public class Vertex
{
    private readonly List<Position> _neighbors;
    private Position _position;

    public Position Position
    {
        get => _position;

        private init
        {
            _position = value;
            _coordinate = new Coordinate(_position.X, _position.Y);
        }
    }

    private Coordinate _coordinate;

    public Coordinate Coordinate
    {
        get => _coordinate;
    }

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
        _neighbors.Sort((p1, p2) => (int)(Position.GetBearing(p1) - Position.GetBearing(p2)));
    }

    /// <summary>
    /// Returns the neighbor that's right (=counter clockwise) or equal to the angle of the given position.
    /// </summary>
    public Position? RightNeighbor(Position basePosition)
    {
        var index = _neighbors.FindLastIndex(neighborPosition =>
            Position.GetBearing(neighborPosition) <= Position.GetBearing(basePosition));
        index = index >= 0 ? index : _neighbors.Count - 1;
        return index >= 0 ? _neighbors[index] : null;
    }

    /// <summary>
    /// Returns the neighbor that's left (=clockwise) or equal to the angle of the given position.
    /// </summary>
    public Position? LeftNeighbor(Position basePosition)
    {
        var index = _neighbors.FindIndex(neighborPosition =>
            Position.GetBearing(neighborPosition) > Position.GetBearing(basePosition));
        index = index >= 0 ? index : 0;
        return index < _neighbors.Count ? _neighbors[index] : null;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Vertex);
    }

    public bool Equals(Vertex? other)
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