using HybridVisibilityGraphRouting.IO;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using ServiceStack;
using Triangulation;
using Feature = NetTopologySuite.Features.Feature;
using Position = Mars.Interfaces.Environments.Position;

namespace HybridVisibilityGraphRouting.Geometry;

public class GeometryHelper
{
    /// <summary>
    /// Triangulates each geometry after unwrapping it, which means multi-geometries (e.g. MultiPolygon) are split up
    /// into the separate single geometries (Polygon in this case). <br/>
    /// <br/>
    /// Unwrapped polygonal geometries are then triangulated. Non-polygonal geometries are not affected by this.
    /// </summary>
    /// <param name="features">The features whose geometries should be triangulated</param>
    /// <param name="debugModeActive">Set to true to write the result to disk.</param>
    /// <returns>A dict from triangulated geometry to its original one.</returns>
    public static Dictionary<NetTopologySuite.Geometries.Geometry, NetTopologySuite.Geometries.Geometry>
        UnwrapAndTriangulate(IEnumerable<IFeature> features,
            bool debugModeActive = false)
    {
        var geometriesToTriangulate = new LinkedList<Polygon>();
        var geometriesToIgnore =
            new Dictionary<NetTopologySuite.Geometries.Geometry, NetTopologySuite.Geometries.Geometry>();

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
                        geometriesToIgnore[geometry] = geometry;
                        return;
                    }

                    // Non-polygonal but closed geometries (like a closed LineString or LinearRing) can easily be
                    // converted into a valid polygon for triangulation below.
                    geometry = new Polygon(new LinearRing(geometry.Coordinates));
                }

                geometriesToTriangulate.AddLast((Polygon)geometry);
            });

        var vertexCount = geometriesToTriangulate.Sum(o => o.Coordinates.Length) +
                          geometriesToIgnore.Sum(o => o.Value.Coordinates.Length);
        if (PerformanceMeasurement.CurrentRun != null)
        {
            PerformanceMeasurement.CurrentRun.ObstacleVertices = vertexCount;
            PerformanceMeasurement.CurrentRun.ObstacleCountInput = geometriesToTriangulate.Count + geometriesToIgnore.Count;
        }

        Log.D($"Amount of features before triangulating: {geometriesToTriangulate.Count + geometriesToIgnore.Count}");
        Log.D($"Amount of vertices before triangulating: {vertexCount}");

        var triangulatedGeometries =
            new Dictionary<NetTopologySuite.Geometries.Geometry, NetTopologySuite.Geometries.Geometry>();
        geometriesToTriangulate
            .Each(geometry =>
            {
                PolygonTriangulator.Triangulate(geometry)
                    .Each(g => triangulatedGeometries[g] = geometry);
            });

        vertexCount = triangulatedGeometries.Sum(o => o.Key.Coordinates.Length) +
                      geometriesToIgnore.Sum(o => o.Key.Coordinates.Length);
        if (PerformanceMeasurement.CurrentRun != null)
        {
            PerformanceMeasurement.CurrentRun.ObstacleVerticesAfterPreprocessing = vertexCount;
            PerformanceMeasurement.CurrentRun.ObstacleCountAfterUnwrapping = triangulatedGeometries.Count + geometriesToIgnore.Count;
        }

        Log.D($"Amount of features after triangulating: {triangulatedGeometries.Count + geometriesToIgnore.Count}");
        Log.D($"Amount of vertices after triangulating: {vertexCount}");

        if (!PerformanceMeasurement.IsActive && debugModeActive)
        {
            var featureCollection = new FeatureCollection();
            triangulatedGeometries.Each(o => featureCollection.Add(new Feature(o.Key, new AttributesTable())));
            Exporter.WriteFeaturesToFile(featureCollection, "features-splitted.geojson").Wait();
        }

        return triangulatedGeometries.Merge(geometriesToIgnore);
    }

    /// <summary>
    /// Unwraps the given geometry. This means unpacking all geometry collections (multi-geometries are geometry
    /// collections internally) into the set of separate geometries as well as unpacking polygon rings into separate
    /// polygons. All other geometry types will be returned unchanged.
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
    /// Returns the envelope of the given coordinates interpreting them as line segment coordinates. I.E. it is
    /// assumed that there are only two coordinates.
    /// </summary>
    public static Envelope GetEnvelopeOfLineSegment(Position[] coordinates)
    {
        var minX = coordinates.Min(c => c.X);
        var maxX = coordinates.Max(c => c.X);
        var minY = coordinates.Min(c => c.Y);
        var maxY = coordinates.Max(c => c.Y);

        return new Envelope(minX, maxX, minY, maxY);
    }
}