using NetTopologySuite.Geometries;

namespace Triangulation;

public static class PolygonTriangulator
{
    public static Geometry[] Triangulate(Polygon polygon)
    {
        return ((GeometryCollection)NetTopologySuite.Triangulate.Polygon.PolygonTriangulator.Triangulate(polygon))
            .Geometries;
    }
}