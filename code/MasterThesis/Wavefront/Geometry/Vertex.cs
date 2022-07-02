using NetTopologySuite.Geometries;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront.Geometry;

public class Vertex
{
    public Position Position { get; }

    public double X => Position.X;
    public double Y => Position.Y;

    /// <summary>
    /// The geometry this vertex belongs to, e.g. a LineString.
    /// </summary>
    public NetTopologySuite.Geometries.Geometry RootGeometry { get; }

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