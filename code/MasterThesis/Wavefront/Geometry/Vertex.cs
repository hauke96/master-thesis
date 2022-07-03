using NetTopologySuite.Geometries;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront.Geometry;

public class Vertex
{
    private Position _position;

    public Position Position
    {
        get => _position;

        private set
        {
            _position = value;
            Coordinate = new Coordinate(_position.X, _position.Y);
        }
    }

    public Coordinate Coordinate { get; private set; }

    public double X => Position.X;
    public double Y => Position.Y;

    /// <summary>
    /// The geometry this vertex belongs to, e.g. a LineString.
    /// </summary>
    public NetTopologySuite.Geometries.Geometry? RootGeometry { get; }

    /// <summary>
    /// Determines the right neighbor within the Geometry. Think of the geometry as a list of coordinates, this returns
    /// the coordinate which is the next element in this coordinate list.
    /// </summary>
    public Coordinate? RightNeighbor
    {
        get
        {
            if (RootGeometry == null)
            {
                return null;
            }
            
            // TODO handle polygons/closed lines

            var coordinates = RootGeometry.Coordinates;
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
            if (RootGeometry == null)
            {
                return null;
            }
            
            // TODO handle polygons/closed lines

            var coordinates = RootGeometry.Coordinates;
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

    public Vertex(Coordinate coordinate, NetTopologySuite.Geometries.Geometry rootGeometry)
    {
        Position = Position.CreateGeoPosition(coordinate.X, coordinate.Y);
        RootGeometry = rootGeometry;
    }
}