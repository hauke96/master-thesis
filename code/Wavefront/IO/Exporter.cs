using System.Diagnostics;
using Mars.Common;
using Mars.Common.Collections.Graph;
using Mars.Numerics;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NetTopologySuite.IO;
using NetTopologySuite.IO.Converters;
using Newtonsoft.Json;
using ServiceStack;
using Feature = NetTopologySuite.Features.Feature;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront.IO;

public static class Exporter
{
    public static async void WriteRoutesToFile(List<List<Waypoint>> routes)
    {
        var features = RoutesToGeometryCollection(routes);
        await WriteFeaturesToFile(features, "agent-routes.geojson");
    }

    public static async void WriteRouteToFile(List<Position> route)
    {
        var features = PositionsToGeometryCollection(route);
        await WriteFeaturesToFile(features, "agent-route.geojson");
    }

    public static async void WriteVisitedPositionsToFile(List<List<Waypoint>> routes)
    {
        var waypoints = routes.SelectMany(l => l) // Flatten list of lists
            .GroupBy(w => w.Position) // Waypoint may have been visited multiple times
            .Map(g => g.OrderBy(w => w.Order).First()) // Get the first visited waypoint
            .ToList();
        var features = new FeatureCollection();
        waypoints.Each(w =>
        {
            var pointGeometry = (NetTopologySuite.Geometries.Geometry)new Point(w.Position.ToCoordinate());
            var attributes = new AttributesTable
            {
                { "order", w.Order },
                { "time", w.Time }
            };
            features.Add(new Feature(pointGeometry, attributes));
        });

        await WriteFeaturesToFile(features, "agent-points.geojson");
    }

    public static async Task WriteFeaturesToFile(FeatureCollection features, string filename)
    {
        var serializer = GeoJsonSerializer.Create();
        foreach (var converter in serializer.Converters
                     .Where(c => c is CoordinateConverter || c is GeometryConverter)
                     .ToList())
        {
            serializer.Converters.Remove(converter);
        }

        serializer.Converters.Add(new CoordinateZMConverter());
        serializer.Converters.Add(new GeometryZMConverter());

        await using var stringWriter = new StringWriter();
        using var jsonWriter = new JsonTextWriter(stringWriter);

        serializer.Serialize(jsonWriter, features);
        var geoJson = stringWriter.ToString();

        await File.WriteAllTextAsync(filename, geoJson);
    }

    private static FeatureCollection PositionsToGeometryCollection(List<Position> route)
    {
        var baseDate = new DateTime(2010, 1, 1);
        var unixZero = new DateTime(1970, 1, 1);
        var distanceFromStart = 0d;
        var waypoints = new List<Waypoint>();

        route.Each((i, position) =>
        {
            distanceFromStart += Distance.Euclidean(route[Math.Max(i - 1, 0)].PositionArray, position.PositionArray);
            waypoints.Add(new Waypoint(position, i, baseDate.AddSeconds(i).Subtract(unixZero).TotalSeconds,
                distanceFromStart));
        });

        return RoutesToGeometryCollection(new List<List<Waypoint>> { waypoints });
    }

    private static FeatureCollection RoutesToGeometryCollection(List<List<Waypoint>> routes)
    {
        var featureCollection = new FeatureCollection();
        routes.Each((i, r) => featureCollection.Add(
            new Feature(RouteToLineString(r),
                new AttributesTable(
                    new Dictionary<string, object>
                    {
                        { "id", i }
                    }
                )
            )
        ));
        return featureCollection;
    }

    private static LineString RouteToLineString(List<Waypoint> route)
    {
        var baseDate = new DateTime(2010, 1, 1);
        var unixZero = new DateTime(1970, 1, 1);
        var coordinateSequence = CoordinateArraySequenceFactory.Instance.Create(route.Count, 3, 1);
        route.Each((i, w) =>
        {
            coordinateSequence.SetX(i, w.Position.X);
            coordinateSequence.SetY(i, w.Position.Y);
            coordinateSequence.SetM(i, baseDate.AddSeconds(w.Time).Subtract(unixZero).TotalSeconds);
        });
        var geometryFactory = new GeometryFactory(CoordinateArraySequenceFactory.Instance);
        return new LineString(coordinateSequence, geometryFactory);
    }

    public static async void WriteVertexNeighborsToFile(Dictionary<Position, HashSet<Position>> positionToNeighbors,
        string filename = "vertex-neighbors.geojson")
    {
        var geometries = new List<NetTopologySuite.Geometries.Geometry>();
        foreach (var pair in positionToNeighbors)
        {
            if (pair.Value.IsEmpty())
            {
                continue;
            }

            var coordinates = new List<Coordinate>();
            coordinates.Add(pair.Key.ToCoordinate());
            foreach (var position in pair.Value)
            {
                coordinates.Add(position.ToCoordinate());
                coordinates.Add(pair.Key.ToCoordinate());
            }

            geometries.Add(new LineString(coordinates.ToArray()));
        }

        var geometry = new GeometryCollection(geometries.ToArray());

        var serializer = GeoJsonSerializer.Create();
        await using var stringWriter = new StringWriter();
        using var jsonWriter = new JsonTextWriter(stringWriter);

        serializer.Serialize(jsonWriter, geometry);
        var geoJson = stringWriter.ToString();

        await File.WriteAllTextAsync(filename, geoJson);
    }

    public static void WriteGraphToFile(SpatialGraph graph, string fileName = "./NetworkLayer.geojson")
    {
        var watch = Stopwatch.StartNew();

        try
        {
            var graphFeatures = new FeatureCollection();
            graph.NodesMap.Each((key, nodeData) =>
            {
                graphFeatures.Add(new Feature(new Point(nodeData.Position.X, nodeData.Position.Y),
                    new AttributesTable(new Dictionary<string, object>()
                    {
                        { "node_id", key },
                        // nodeNeighbors are not up to date anymore due to splitting of the graph
                        // { "neighbors", nodeNeighbors[key] }
                    })));
            });
            graph.Edges.Values.Each((key, edgeData) =>
            {
                graphFeatures.Add(new Feature(
                    new LineString(edgeData.Geometry.Map(p => p.ToCoordinate()).ToArray()),
                    new AttributesTable(new Dictionary<string, object>()
                    {
                        { "edge_id", key }
                    })));
            });
            WriteFeatures(graphFeatures, fileName);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        Console.WriteLine($"WriteGraphToFile: Store layer as GeoJSON done after {watch.ElapsedMilliseconds}ms");
    }

    public static void WriteFeatures(FeatureCollection vectorFeatures, string fileName = "./NetworkLayer.geojson")
    {
        if (File.Exists(fileName))
        {
            File.Delete(fileName);
        }

        var file = File.Open(fileName, FileMode.Append, FileAccess.Write);
        var streamWriter = new StreamWriter(file);
        var geoJsonWriter = new GeoJsonWriter();

        streamWriter.Write(@"{""type"":""FeatureCollection"",""features"":[");
        streamWriter.Write(string.Join(",", vectorFeatures.Select(feature => geoJsonWriter.Write(feature))));
        streamWriter.Write("]}");
        streamWriter.Close();
        streamWriter.Dispose();
    }
}