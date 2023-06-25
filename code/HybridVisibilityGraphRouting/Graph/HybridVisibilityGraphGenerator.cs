using System.Diagnostics;
using HybridVisibilityGraphRouting.Geometry;
using HybridVisibilityGraphRouting.IO;
using Mars.Common;
using Mars.Common.Collections;
using Mars.Common.Collections.Graph;
using Mars.Common.Core.Collections;
using Mars.Interfaces.Environments;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using ServiceStack;

namespace HybridVisibilityGraphRouting.Graph;

public static class HybridVisibilityGraphGenerator
{
    // See method FilterFeaturesByKeys for documentation on filter expression strings
    public static readonly string[] DefaultObstacleExpressions =
    {
        "barrier!=^no$",
        "building!=^(demolished|no|roof)$",
        "natural!=.*grass.*",
        "obstacle",
        "poi",
        "railway",
        "waterway"
    };

    public static readonly string[] DefaultPoiExpressions = { "poi" };

    public static readonly string[] DefaultRoadExpressions =
    {
        "highway!=^(motorway|trunk|motorway_link|trunk_link|bus_guideway|raceway)$"
    };

    /// <summary>
    /// Generates the complete hybrid visibility graph based on the obstacles in the given feature collection. This
    /// method also merges the road and ways within the features correctly with the visibility edges.
    /// </summary>
    public static HybridVisibilityGraph Generate(IEnumerable<IFeature> features,
        int visibilityNeighborBinCount = 36,
        int visibilityNeighborsPerBin = 10,
        string[]? obstacleExpressions = null,
        string[]? poiExpressions = null,
        string[]? roadExpressions = null)
    {
        var watch = Stopwatch.StartNew();

        // Prevent multiple enumerations
        features = features.ToList();

        var obstacles = GetObstacles(features, obstacleExpressions);
        var vertexNeighbors =
            VisibilityGraphGenerator.CalculateVisibleKnn(obstacles, visibilityNeighborBinCount,
                visibilityNeighborsPerBin);
        var (hybridVisibilityGraph, spatialGraph) = AddVisibilityVerticesAndEdges(vertexNeighbors, obstacles);

        MergeRoadsIntoGraph(features, hybridVisibilityGraph, roadExpressions);
        AddAttributesToPoiNodes(features, spatialGraph, 0.001, poiExpressions);

        Log.D($"{nameof(HybridVisibilityGraphGenerator)}: Done after {watch.ElapsedMilliseconds}ms");
        return hybridVisibilityGraph;
    }

    /// <summary>
    /// Takes all obstacle features and calculates for each vertex the visibility neighbors. The obstacles are filtered
    /// by the passed expressions and additionally by their geometry type. Only non-point obstacles (because they
    /// have no spatial size) are considered.
    /// </summary>
    /// <returns>A map from each vertex to the bins of visibility neighbors.</returns>
    public static QuadTree<Obstacle> GetObstacles(IEnumerable<IFeature> features, string[]? obstacleExpressions = null)
    {
        obstacleExpressions ??= DefaultObstacleExpressions;

        var importedObstacles = FeatureHelper.FilterFeaturesByExpressions(features, obstacleExpressions)
            .Where(feature => feature.Geometry.OgcGeometryType != OgcGeometryType.Point);

        var watch = Stopwatch.StartNew();

        var obstacleGeometries = GeometryHelper.UnwrapAndTriangulate(importedObstacles, true);

        var convexHullCoordinates = obstacleGeometries
            .Map(g => g.Value.ConvexHull().Coordinates)
            .SelectMany(x => x)
            .Distinct()
            .ToSet();

        var coordinateToVertex = obstacleGeometries
            .Keys
            .Map(g => g.Coordinates)
            .SelectMany(x => x)
            .Distinct()
            .ToDictionary(c => c, c => new Vertex(c, convexHullCoordinates.Contains(c)));

        var obstacleIndex = new QuadTree<Obstacle>();
        obstacleGeometries.Each(geometry =>
        {
            var vertices = geometry.Key.Coordinates.Map(c => coordinateToVertex[c]).ToList();
            obstacleIndex.Insert(geometry.Key.EnvelopeInternal, new Obstacle(geometry.Key, geometry.Value, vertices));
        });

        Log.D(
            $"{nameof(HybridVisibilityGraphGenerator)}: Splitting obstacles done after {watch.ElapsedMilliseconds}ms");
        return obstacleIndex;
    }

    public static (HybridVisibilityGraph, SpatialGraph) AddVisibilityVerticesAndEdges(
        Dictionary<Vertex, List<List<Vertex>>> vertexNeighbors,
        QuadTree<Obstacle> obstacles)
    {
        var graph = new SpatialGraph();
        var watch = Stopwatch.StartNew();
        var vertexToNode = new Dictionary<Vertex, int[]>();
        var nodeToBinVertices = new Dictionary<int, List<Vertex>>();
        var nodeToAngleArea = new Dictionary<int, (double, double)>();
        var verticesOnConvexHull = vertexNeighbors.Keys;

        // Create a node for every vertex in the dataset. Also store the mapping between node keys and vertices.
        verticesOnConvexHull.Each(vertex =>
        {
            List<List<Vertex>> vertexNeighborBin =
                vertex.IsOnConvexHull ? vertexNeighbors[vertex] : new List<List<Vertex>>();

            vertexToNode[vertex] = new int[vertexNeighborBin.Count];
            vertexNeighborBin.Each((i, bin) =>
            {
                // For debug porposes to see the different nodes in the GeoJSON file.
                // var nodePosition = PositionHelper.CalculatePositionByBearing(vertex.Position.X, vertex.Position.Y,
                //     360 / vertexNeighborBin.Count * i, 0.000005);

                var nodeKey = graph.AddNode(vertex.Coordinate.X, vertex.Coordinate.Y, new Dictionary<string, object>())
                    .Key;
                vertexToNode[vertex][i] = nodeKey;
                nodeToBinVertices[nodeKey] = bin;

                // Determine covered angle area of the current bin
                double binFromAngle = 0;
                double binToAngle = 360;
                if (vertex.ObstacleNeighbors.Any())
                {
                    binFromAngle = Angle.GetBearing(vertex.Coordinate, vertex.ObstacleNeighbors[i].ToCoordinate());
                    binToAngle = Angle.GetBearing(vertex.Coordinate,
                        vertex.ObstacleNeighbors[(i + 1) % vertex.ObstacleNeighbors.Count].ToCoordinate());
                }

                nodeToAngleArea[nodeKey] = (binFromAngle, binToAngle);
            });
        });

        // Create visibility edges in the graph. This is done on a per-bin basis. Each bin got a separate node and this
        // node is then correctly connected to the node of its visibility vertices.
        var nodeNeighbors = new Dictionary<int, List<int>>();
        verticesOnConvexHull.Each(vertex =>
        {
            if (!vertex.IsOnConvexHull)
            {
                return;
            }

            var neighborBins = vertexNeighbors[vertex];
            neighborBins.Each((i, neighborBin) =>
            {
                var vertexNode = vertexToNode[vertex][i];
                nodeNeighbors[vertexNode] = new List<int>();

                // Connect each visibility neighbors to the node of the current vertex.
                neighborBin.Each(otherVertex =>
                {
                    // We only have a vertex but need a node, so we get all nodes for the location of the vertex but
                    // only take the one node that belongs to the current bin.
                    var otherVertexNodes = vertexToNode[otherVertex].Where(potentialOtherVertexNode =>
                    {
                        return nodeToBinVertices[potentialOtherVertexNode].Contains(vertex);
                    }).ToList();

                    otherVertexNodes.Each(otherVertexNode =>
                    {
                        nodeNeighbors[vertexNode].Add(otherVertexNode);
                        graph.AddEdge(vertexNode, otherVertexNode);
                    });
                });
            });
        });

        Log.D($"{nameof(HybridVisibilityGraphGenerator)}: Graph creation done after {watch.ElapsedMilliseconds}ms");
        Log.D($"  Number of nodes: {graph.NodesMap.Count}");
        Log.D($"  Number of edges: {graph.EdgesMap.Count}");

        var hybridVisibilityGraph = new HybridVisibilityGraph(graph, obstacles, vertexToNode, nodeToAngleArea);
        return (hybridVisibilityGraph, graph);
    }

    /// <summary>
    /// This merges all features with a "highway=*" attribute into the given graph. Whenever a road-edge intersects
    /// an existing edge, both edges will be split at the intersection point where a new node is added.
    /// </summary>
    public static void MergeRoadsIntoGraph(IEnumerable<IFeature> features, HybridVisibilityGraph hybridGraph,
        string[]? roadExpressions = null)
    {
        roadExpressions ??= DefaultRoadExpressions;

        var watch = Stopwatch.StartNew();

        var roadFeatures = FeatureHelper.FilterFeaturesByExpressions(features, roadExpressions)
            .Where(f => f.Geometry.OgcGeometryType != OgcGeometryType.Point)
            .ToList();
        var roadSegments = FeatureHelper.SplitFeaturesToSegments(roadFeatures);

        // Determine dead-ends and connect them manually. This enables the routing to better connect to roads.
        var vertexToRoad = new Dictionary<Coordinate, ICollection<IFeature>>();
        roadFeatures.Each(feature =>
        {
            var coordinate = feature.Geometry.Coordinates[0];
            if (!vertexToRoad.ContainsKey(coordinate))
            {
                vertexToRoad[coordinate] = new HashSet<IFeature>();
            }

            vertexToRoad[coordinate].Add(feature);

            coordinate = feature.Geometry.Coordinates[^1];
            if (!vertexToRoad.ContainsKey(coordinate))
            {
                vertexToRoad[coordinate] = new HashSet<IFeature>();
            }

            vertexToRoad[coordinate].Add(feature);
        });

        // Add the dead-ends (coordinates belonging to only one road) to graph
        vertexToRoad
            .Where(pair => pair.Value.Count == 1)
            .Each(pair =>
            {
                var (node, nodeHasBeenCreated) = hybridGraph.AddPositionToGraph(pair.Key.ToPosition());
                if (nodeHasBeenCreated)
                {
                    hybridGraph.ConnectNodeToGraph(node);
                }
            });

        // Merge the segments into the graph
        roadSegments.Each((i, roadSegment) =>
        {
            if (Log.LogLevel == Log.DEBUG && i % (roadSegments.Count / 10) == 0)
            {
                Log.D($"MergeSegmentIntoGraph {i}/{roadSegments.Count} ({Math.Round(i / (float)roadSegments.Count * 100.0)}%)");
            }

            hybridGraph.MergeSegmentIntoGraph(hybridGraph, roadSegment);
        });

        Log.D(
            $"{nameof(HybridVisibilityGraphGenerator)}: Merging road network into graph done after {watch.ElapsedMilliseconds}ms");
        Log.D($"  Number of nodes: {hybridGraph.Graph.NodesMap.Count}");
        Log.D($"  Number of edges: {hybridGraph.Graph.EdgesMap.Count}");
    }

    /// <summary>
    /// Takes each feature with an attribute name from "poiKeys" and adds all attributes of this feature to the closest
    /// node (within the given distance) in the graph.
    /// </summary>
    public static void AddAttributesToPoiNodes(IEnumerable<IFeature> features, ISpatialGraph graph,
        double nodeDistanceTolerance = 0.001, string[]? poiExpressions = null)
    {
        poiExpressions ??= DefaultPoiExpressions;

        FeatureHelper.FilterFeaturesByExpressions(features, poiExpressions)
            .Each(f =>
            {
                var featurePosition = f.Geometry.Coordinates[0].ToPosition();
                var nearestNodes = graph
                    .NodesMap
                    .Values
                    .Where(n => n.Position.DistanceInMTo(featurePosition) < nodeDistanceTolerance)
                    .ToList();

                if (!nearestNodes.IsEmpty())
                {
                    nearestNodes[0].Data.AddRange(f.Attributes.ToObjectDictionary());
                }
            });
    }
}