using System.Diagnostics;
using Mars.Common.Collections.Graph;
using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using ServiceStack;
using Wavefront;
using Wavefront.Geometry;
using Feature = NetTopologySuite.Features.Feature;
using Position = Mars.Interfaces.Environments.Position;

namespace NetworkRoutingPlayground.Layer;

public class NetworkLayer : VectorLayer
{
    public ISpatialGraphEnvironment Environment { get; private set; }

    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
        UnregisterAgent unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

        // To load the data from an input GeoJSON file:
        // var inputs = layerInitData.LayerInitConfig.Inputs;
        //
        // if (inputs != null && inputs.Any())
        // {
        //     Environment = new SpatialGraphEnvironment(new SpatialGraphOptions
        //     {
        //         GraphImports = inputs
        //     });
        // }

        var obstacleGeometries = Features.Map(f => new Obstacle(f.VectorStructured.Geometry));
        var watch = Stopwatch.StartNew();

        var obstacles = WavefrontPreprocessor.SplitObstacles(obstacleGeometries);
        Console.WriteLine($"WavefrontPreprocessor: Splitting obstacles done after {watch.ElapsedMilliseconds}ms");

        var vertexNeighbors = WavefrontPreprocessor.CalculateVisibleKnn(obstacles, 36, 10, false);
        var graph = new SpatialGraph();
        var nodes = new Dictionary<Position, int>();
        var features = new FeatureCollection();
        vertexNeighbors.Keys.Each(vertex =>
        {
            var nodeKey = graph.AddNode(new Dictionary<string, object>
            {
                { "x", vertex.Position.X },
                { "y", vertex.Position.X },
            }).Key;
            nodes[vertex.Position] = nodeKey;
        });
        vertexNeighbors.ForEach((vertex, otherVertices) =>
        {
            var vertexNode = nodes[vertex.Position];
            otherVertices.Each(otherVertex =>
            {
                var otherVertexNode = nodes[otherVertex.Position];
                
                var edgeForwardAlreadyExists =
                    graph.Edges.Values.Any(edge => vertexNode == edge.From && otherVertexNode == edge.To);
                if (!edgeForwardAlreadyExists)
                {
                    graph.AddEdge(vertexNode, otherVertexNode);
                    features.Add(new Feature(new LineString(new[] { vertex.Coordinate, otherVertex.Coordinate }),
                        new AttributesTable()));
                }
                
                var edgeBackwardAlreadyExists =
                    graph.Edges.Values.Any(edge => otherVertexNode == edge.From && vertexNode == edge.To);
                if (!edgeBackwardAlreadyExists)
                {
                    graph.AddEdge(otherVertexNode, vertexNode);
                    features.Add(new Feature(new LineString(new[] { otherVertex.Coordinate, vertex.Coordinate }),
                        new AttributesTable()));
                }
            });
        });

        Environment = new SpatialGraphEnvironment(graph);

        Console.WriteLine($"WavefrontPreprocessor: CalculateVisibleKnn done after {watch.ElapsedMilliseconds}ms");

        watch.Restart();
        Console.WriteLine($"Store layer as GeoJSON");
        try
        {
            WriteFeatures(features);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        Console.WriteLine($"Store layer as GeoJSON done after {watch.ElapsedMilliseconds}ms");

        return true;
    }

    private void WriteFeatures(FeatureCollection vectorFeatures)
    {
        var fileName = "./NetworkLayer.geojson";

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