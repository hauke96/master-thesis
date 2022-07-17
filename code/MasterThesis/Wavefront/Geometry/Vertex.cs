using NetTopologySuite.Geometries;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront.Geometry;

public class Vertex
{
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

    /// <summary>
    /// The geometry this vertex belongs to, e.g. a LineString.
    /// </summary>
    private readonly NetTopologySuite.Geometries.Geometry? _rootGeometry;

    /// <summary>
    /// Determines the right neighbor within the Geometry. Think of the geometry as a list of coordinates, this returns
    /// the coordinate which is the next element in this coordinate list.
    /// </summary>
    public Coordinate? RightNeighbor
    {
        get
        {
            if (_rootGeometry == null)
            {
                return null;
            }

            // TODO handle polygons/closed lines

            var coordinates = _rootGeometry.Coordinates;
            var indexOfThisVertex = Array.IndexOf(coordinates, Coordinate);
            if (indexOfThisVertex + 1 >= coordinates.Length)
            {
                // This is already the last coordinate
                return null;
            }

            return coordinates[indexOfThisVertex + 1];
        }
    }

    /// <summary>
    /// Determines the left neighbor within the Geometry. Think of the geometry as a list of coordinates, this returns
    /// the coordinate which is the previous element in this coordinate list.
    /// </summary>
    public Coordinate? LeftNeighbor
    {
        get
        {
            if (_rootGeometry == null)
            {
                return null;
            }

            // TODO handle polygons/closed lines

            var coordinates = _rootGeometry.Coordinates;
            var indexOfThisVertex = Array.IndexOf(coordinates, Coordinate);
            if (indexOfThisVertex == 0)
            {
                // This is already the first coordinate
                return null;
            }

            return coordinates[indexOfThisVertex - 1];
        }
    }

    public Vertex(Position position)
    {
        Position = position;
    }

    public Vertex(double x, double y)
    {
        Position = Position.CreateGeoPosition(x, y);
    }

    public Vertex(Coordinate coordinate, NetTopologySuite.Geometries.Geometry rootGeometry)
    {
        Position = Position.CreateGeoPosition(coordinate.X, coordinate.Y);
        _rootGeometry = rootGeometry;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Vertex);
    }

    public bool Equals(Vertex? other)
    {
        return other != null && Position.Equals(other.Position) && Equals(_rootGeometry, other._rootGeometry);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Position, _rootGeometry);
    }

    public override string ToString()
    {
        return Position.ToString();
    }
}