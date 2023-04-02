using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Triangulate.Polygon;
using Wavefront.IO;
using ServiceStack;
using Feature = NetTopologySuite.Features.Feature;

namespace Wavefront.Geometry;

public class GeometryHelper
{
    /// <summary>
    /// Triangulates each geometry after unwrapping it, which means multi-geometries (e.g. MultiPolygon) are split up
    /// into the separate single geometries (e.g. Polygon). These simple geometries are then triangulated.
    /// </summary>
    /// <param name="features">The features whose geometries should be triangulated</param>
    /// <param name="debugModeActive">Set to true to write the result to disk.</param>
    /// <returns>A list of simple triangulated geometries.</returns>
    public static List<NetTopologySuite.Geometries.Geometry> UnwrapAndTriangulate(List<IFeature> features,
        bool debugModeActive = false)
    {
        var vertexCount = features.Sum(o => o.Geometry.Coordinates.Length);
        PerformanceMeasurement.TOTAL_VERTICES = vertexCount;
        Log.D($"Amount of features before splitting: {features.Count}");
        Log.D($"Amount of vertices before splitting: {vertexCount}");

        var geometries = features.Map(g => UnwrapMultiGeometries(g.Geometry)).SelectMany(x => x).ToList();

        var triangulatedGeometries = geometries.Map(geometry =>
            {
                if (geometry is not Polygon && geometry is not MultiPolygon)
                {
                    if (geometry.Coordinates.Length == 1)
                    {
                        return new[] { geometry };
                    }

                    if (geometry.Coordinates[0].Equals(geometry.Coordinates[^1]))
                    {
                        // Non-polygonal geometries (like a closed LineString or LinearRing) can easily be converted
                        // into a valid polygon for triangulation below.
                        geometry = new Polygon(new LinearRing(geometry.Coordinates));
                    }
                    else
                    {
                        return new[] { geometry };
                    }
                }

                return ((GeometryCollection)PolygonTriangulator.Triangulate(geometry)).Geometries;
            })
            .SelectMany(x => x)
            .ToList();

        vertexCount = triangulatedGeometries.Sum(o => o.Coordinates.Length);
        PerformanceMeasurement.TOTAL_VERTICES_AFTER_PREPROCESSING = vertexCount;
        Log.D($"Amount of features after splitting: {triangulatedGeometries.Count}");
        Log.D($"Amount of vertices after splitting: {vertexCount}");

        if (debugModeActive)
        {
            var featureCollection = new FeatureCollection();
            triangulatedGeometries.Each(o => featureCollection.Add(new Feature(o, new AttributesTable())));
            Exporter.WriteFeaturesToFile(featureCollection, "features-splitted.geojson").Wait();
        }

        return triangulatedGeometries;
    }

    /// <summary>
    /// Unwraps the given geometry, which means resolving multi-geometry relations.
    ///
    /// However, only MultiPolygon geometries are actually unwrapped. For each polygon in a multi-polygon, their
    /// exterior rings are collected and returned.
    ///
    /// All other geometry types will be returned unchanged.
    /// </summary>
    public static List<NetTopologySuite.Geometries.Geometry> UnwrapMultiGeometries(
        NetTopologySuite.Geometries.Geometry geometry)
    {
        var unwrappedGeometries = new List<NetTopologySuite.Geometries.Geometry>();

        if (geometry is MultiPolygon multiPolygon)
        {
            multiPolygon.Each(polygon =>
            {
                var simplePolygon = new Polygon((LinearRing)((Polygon)polygon.GetGeometryN(0)).ExteriorRing);
                unwrappedGeometries.Add(simplePolygon);
            });
        }
        else
        {
            unwrappedGeometries.Add(geometry);
        }

        return unwrappedGeometries;
    }

    public static bool IsGeometryClosed(NetTopologySuite.Geometries.Geometry geometry)
    {
        return geometry.Coordinates.Length > 2 && Equals(geometry.Coordinates.First(), geometry.Coordinates.Last());
    }
}