using System.Diagnostics;
using Mars.Common;
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

namespace NetworkRoutingPlayground.Layer;

public class NetworkLayer : VectorLayer
{
    public ISpatialGraphEnvironment Environment { get; private set; }

    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
        UnregisterAgent unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

        var obstacleGeometries = Features.Where(f =>
        {
            return f.VectorStructured.Attributes.GetNames().Any(name =>
            {
                var lowerName = name.ToLower();
                return lowerName.Contains("building") || lowerName.Contains("natural");
            });
        }).Map(f => new Obstacle(f.VectorStructured.Geometry));
        var watch = Stopwatch.StartNew();

        var obstacles = WavefrontPreprocessor.SplitObstacles(obstacleGeometries);
        Console.WriteLine($"WavefrontPreprocessor: Splitting obstacles done after {watch.ElapsedMilliseconds}ms");

        var vertexNeighbors = WavefrontPreprocessor.CalculateVisibleKnn(obstacles, 36, 10, true);

        var graph = new SpatialGraph();
        var vertexToNode = new Dictionary<Vertex, int[]>();
        var nodeToBinVertices = new Dictionary<int, List<Vertex>>();

        // Create a node for every vertex in the dataset. Also store the mapping between node keys and vertices.
        vertexNeighbors.Keys.Each(vertex =>
        {
            var vertexNeighborBin = vertexNeighbors[vertex];
            vertexToNode[vertex] = new int[vertexNeighborBin.Count];
            vertexNeighborBin.Each((i, bin) =>
            {
                // For debug porposes to see the different nodes in the GeoJSON file.
                // var nodePosition = PositionHelper.CalculatePositionByBearing(vertex.Position.X, vertex.Position.Y,
                //     360 / vertexNeighborBin.Count * i, 0.000005);
                var nodePosition = vertex.Position;
                
                var nodeKey = graph.AddNode(new Dictionary<string, object>
                {
                    { "x", nodePosition.X },
                    { "y", nodePosition.Y },
                }).Key;
                vertexToNode[vertex][i] = nodeKey;
                nodeToBinVertices[nodeKey] = bin;
            });
        });

        // Create visibility edges in the graph.
        var nodeNeighbors = new Dictionary<int, List<int>>();
        vertexNeighbors.Keys.Each(vertex =>
        {
            vertexNeighbors[vertex].Each((i, neighborBin) =>
            {
                var vertexNode = vertexToNode[vertex][i];
                nodeNeighbors[vertexNode] = new List<int>();

                neighborBin.Each(otherVertex =>
                {
                    var otherVertexNodes = vertexToNode[otherVertex].Where(potentialOtherVertexNode =>
                    {
                        return nodeToBinVertices[potentialOtherVertexNode].Contains(vertex);
                    }).ToList();

                    // Due to the bins, it can happen that visibility edges only exist in one direction. In this case,
                    // it would be really difficult to determine the node of the target vertex to connect the edge to.
                    if (otherVertexNodes.IsEmpty())
                    {
                        Log.I("WARN: No visibility edge found from " + vertex + " to " + otherVertex);
                        return;
                    }

                    var otherVertexNode = otherVertexNodes[0];
                    nodeNeighbors[vertexNode].Add(otherVertexNode);
                    graph.AddEdge(vertexNode, otherVertexNode);
                });
            });
        });

        Environment = new SpatialGraphEnvironment(graph);

        Console.WriteLine($"WavefrontPreprocessor: CalculateVisibleKnn done after {watch.ElapsedMilliseconds}ms");

        watch.Restart();
        Console.WriteLine($"Store layer as GeoJSON");
        try
        {
            var graphFeatures = new FeatureCollection();
            graph.NodesMap.Each((key, nodeData) =>
            {
                graphFeatures.Add(new Feature(new Point(nodeData.Position.X, nodeData.Position.Y),
                    new AttributesTable(new Dictionary<string, object>()
                    {
                        { "node_id", key },
                        { "neighbors", nodeNeighbors[key] }
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
            WriteFeatures(graphFeatures);
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