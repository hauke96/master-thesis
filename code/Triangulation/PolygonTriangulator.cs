using NetTopologySuite.Geometries;

namespace Triangulation;

public static class PolygonTriangulator
{
    public static Geometry[] Triangulate(Polygon polygon)
    {
        try
        {
            return ((GeometryCollection)NetTopologySuite.Triangulate.Polygon.PolygonTriangulator.Triangulate(polygon))
                .Geometries;
        }
        catch (Exception e)
        {
            Console.WriteLine(polygon);
            throw;
        }
    }
}