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

namespace HybridVisibilityGraphRouting.IO;

public static class Exporter
{
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

    public static async void WriteVertexNeighborsToFile(Dictionary<Coordinate, HashSet<Position>> positionToNeighbors,
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
            coordinates.Add(pair.Key);
            foreach (var position in pair.Value)
            {
                coordinates.Add(position.ToCoordinate());
                coordinates.Add(pair.Key);
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

    public static void WriteGraphToFile(SpatialGraph graph, string fileName = "./graph.geojson")
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