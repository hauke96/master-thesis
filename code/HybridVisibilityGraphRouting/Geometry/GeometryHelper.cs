using HybridVisibilityGraphRouting.IO;
using Mars.Common.Collections.Graph;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Triangulate.Polygon;
using ServiceStack;
using Feature = NetTopologySuite.Features.Feature;

namespace HybridVisibilityGraphRouting.Geometry;

public class GeometryHelper
{
    /// <summary>
    /// Triangulates each geometry after unwrapping it, which means multi-geometries (e.g. MultiPolygon) are split up
    /// into the separate single geometries (e.g. Polygon). These simple geometries are then triangulated.
    /// </summary>
    /// <param name="features">The features whose geometries should be triangulated</param>
    /// <param name="debugModeActive">Set to true to write the result to disk.</param>
    /// <returns>A list of simple triangulated geometries.</returns>
    public static List<NetTopologySuite.Geometries.Geometry> UnwrapAndTriangulate(IEnumerable<IFeature> features,
        bool debugModeActive = false)
    {
        var geometriesToTriangulate = new LinkedList<NetTopologySuite.Geometries.Geometry>();
        var geometriesToIgnore = new LinkedList<NetTopologySuite.Geometries.Geometry>();

        // Unwrap geometries and sort them into a list of geometries to triangulate and into a list of geometries to
        // keep unchanged.
        features
            .Map(g => UnwrapMultiGeometries(g.Geometry))
            .SelectMany(x => x)
            .Each(geometry =>
            {
                if (geometry is not Polygon)
                {
                    if (geometry.Coordinates.Length == 1 || !IsGeometryClosed(geometry))
                    {
                        // Ignore point and non-closed geometries for triangulation and just keep them as they are.
                        geometriesToIgnore.AddLast(geometry);
                        return;
                    }

                    // Non-polygonal but closed geometries (like a closed LineString or LinearRing) can easily be
                    // converted into a valid polygon for triangulation below.
                    geometry = new Polygon(new LinearRing(geometry.Coordinates));
                }

                geometriesToTriangulate.AddLast(geometry);
            });

        var vertexCount = geometriesToTriangulate.Sum(o => o.Coordinates.Length) +
                          geometriesToIgnore.Sum(o => o.Coordinates.Length);
        PerformanceMeasurement.TOTAL_VERTICES = vertexCount;
        Log.D($"Amount of features before triangulating: {geometriesToTriangulate.Count + geometriesToIgnore.Count}");
        Log.D($"Amount of vertices before triangulating: {vertexCount}");

        var triangulatedGeometries = geometriesToTriangulate
            .Map(geometry => ((GeometryCollection)PolygonTriangulator.Triangulate(geometry)).Geometries)
            .SelectMany(x => x)
            .ToList();

        vertexCount = triangulatedGeometries.Sum(o => o.Coordinates.Length) +
                      geometriesToIgnore.Sum(o => o.Coordinates.Length);
        PerformanceMeasurement.TOTAL_VERTICES_AFTER_PREPROCESSING = vertexCount;
        Log.D($"Amount of features after triangulating: {triangulatedGeometries.Count + geometriesToIgnore.Count}");
        Log.D($"Amount of vertices after triangulating: {vertexCount}");

        if (debugModeActive)
        {
            var featureCollection = new FeatureCollection();
            triangulatedGeometries.Each(o => featureCollection.Add(new Feature(o, new AttributesTable())));
            Exporter.WriteFeaturesToFile(featureCollection, "features-splitted.geojson").Wait();
        }

        return geometriesToIgnore.Concat(triangulatedGeometries).ToList();
    }

    /// <summary>
    /// Unwraps the given geometry, which means resolving multi-geometry relations.
    ///
    /// However, only MultiPolygon and MultiLineString geometries are currently unwrapped. For each polygon in a
    /// multi-polygon, their exterior rings are collected and returned.
    ///
    /// All other geometry types will be returned unchanged.
    /// </summary>
    public static List<NetTopologySuite.Geometries.Geometry> UnwrapMultiGeometries(
        NetTopologySuite.Geometries.Geometry inputGeometry)
    {
        var unwrappedGeometries = new List<NetTopologySuite.Geometries.Geometry> { inputGeometry };

        // Unwrap all geometry collections, which also includes multi-geometries, and also all polygon with interior
        // ring, which are very similar to multi-polygons.
        while (unwrappedGeometries.Any(g => g is GeometryCollection || g is Polygon p && !p.InteriorRings.IsEmpty()))
        {
            unwrappedGeometries = unwrappedGeometries
                .Map(geometry =>
                {
                    switch (geometry)
                    {
                        case GeometryCollection geometryCollection:
                            return geometryCollection.Geometries;
                        case Polygon polygon when !polygon.InteriorRings.IsEmpty():
                            return new[] { new Polygon((LinearRing)polygon.ExteriorRing) };
                        default:
                            return new[] { geometry };
                    }
                })
                .SelectMany(x => x)
                .ToList();
        }

        return unwrappedGeometries;
    }

    public static bool IsGeometryClosed(NetTopologySuite.Geometries.Geometry geometry)
    {
        return geometry.Coordinates.Length > 2 && Equals(geometry.Coordinates.First(), geometry.Coordinates.Last());
    }

    /// <summary>
    /// This assumes that the edge only consists of two coordinates.
    /// </summary>
    public static Envelope GetEnvelopeOfEdge(EdgeData e)
    {
        var coordinates = e.Geometry;

        var minX = coordinates.Min(c => c.X);
        var maxX = coordinates.Max(c => c.X);
        var minY = coordinates.Min(c => c.Y);
        var maxY = coordinates.Max(c => c.Y);

        return new Envelope(minX, maxX, minY, maxY);
    }
}