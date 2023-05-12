using System.Diagnostics;
using HybridVisibilityGraphRouting.Geometry;
using Mars.Common;
using Mars.Common.Collections;
using Mars.Common.Collections.Graph;
using Mars.Common.Core.Collections;
using Mars.Interfaces.Environments;
using NetTopologySuite.Features;
using ServiceStack;

namespace HybridVisibilityGraphRouting;

public static class HybridVisibilityGraphGenerator
{
    /// <summary>
    /// Generates the complete hybrid visibility graph based on the obstacles in the given feature collection. This
    /// method also merges the road and ways within the features correctly with the visibility edges.
    /// </summary>
    public static HybridVisibilityGraph Generate(IEnumerable<IFeature> features)
    {
        var watch = Stopwatch.StartNew();

        // Prevent multiple enumerations
        features = features.ToList();

        QuadTree<Obstacle>? obstacles = null;
        Dictionary<Vertex, List<List<Vertex>>>? vertexNeighbors = null;
        HybridVisibilityGraph? hybridVisibilityGraph = null;
        SpatialGraph? spatialGraph = null;
        double time;

        // GetObstacles
        time = PerformanceMeasurement.AddFunctionDurationToCurrentRun(
            () => { obstacles = GetObstacles(features); },
            "get_obstacle_time"
        );
        Log.D($"{nameof(HybridVisibilityGraphGenerator)}: get_obstacle_time done after {time}ms");
        ArgumentNullException.ThrowIfNull(obstacles);

        // DetermineVisibilityNeighbors
        time = PerformanceMeasurement.AddFunctionDurationToCurrentRun(
            () => { vertexNeighbors = DetermineVisibilityNeighbors(obstacles); },
            "get_knn_search_time"
        );
        Log.D($"{nameof(HybridVisibilityGraphGenerator)}: get_knn_search_time done after {time}ms");
        ArgumentNullException.ThrowIfNull(vertexNeighbors);

        // AddVisibilityVerticesAndEdges
        time = PerformanceMeasurement.AddFunctionDurationToCurrentRun(
            () =>
            {
                (hybridVisibilityGraph, spatialGraph) = AddVisibilityVerticesAndEdges(vertexNeighbors, obstacles);
            },
            "get_create_graph_time"
        );
        Log.D($"{nameof(HybridVisibilityGraphGenerator)}: get_knn_search_time done after {time}ms");
        ArgumentNullException.ThrowIfNull(hybridVisibilityGraph);
        ArgumentNullException.ThrowIfNull(spatialGraph);

        // MergeRoadsIntoGraph
        time = PerformanceMeasurement.AddFunctionDurationToCurrentRun(
            () => { MergeRoadsIntoGraph(features, hybridVisibilityGraph); },
            "merge_road_graph_time"
        );
        Log.D($"{nameof(HybridVisibilityGraphGenerator)}: merge_road_graph_time done after {time}ms");

        // AddAttributesToPoiNodes
        time = PerformanceMeasurement.AddFunctionDurationToCurrentRun(
            () => { AddAttributesToPoiNodes(features, spatialGraph); },
            "add_poi_attributes_time"
        );
        Log.D($"{nameof(HybridVisibilityGraphGenerator)}: add_poi_attributes_time done after {time}ms");

        Log.D($"{nameof(HybridVisibilityGraphGenerator)}: Done after {watch.ElapsedMilliseconds}ms");
        return hybridVisibilityGraph!;
    }

    /// <summary>
    /// Takes all obstacle features and calculates for each vertex the visibility neighbors.
    /// </summary>
    /// <returns>A map from each vertex to the bins of visibility neighbors.</returns>
    public static QuadTree<Obstacle> GetObstacles(IEnumerable<IFeature> features)
    {
        var wantedKeys = new[] { "building", "barrier", "natural", "poi", "obstacle" };
        var importedObstacles = FeatureHelper.FilterFeaturesByKeys(features, wantedKeys);

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

    public static Dictionary<Vertex, List<List<Vertex>>> DetermineVisibilityNeighbors(QuadTree<Obstacle> obstacles)
    {
        return VisibilityGraphGenerator.CalculateVisibleKnn(obstacles, 36, 10, true);
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

                var nodeKey = graph.AddNode(new Dictionary<string, object>
                {
                    { "x", vertex.Coordinate.X },
                    { "y", vertex.Coordinate.Y },
                }).Key;
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
    public static void MergeRoadsIntoGraph(IEnumerable<IFeature> features, HybridVisibilityGraph hybridGraph)
    {
        var watch = Stopwatch.StartNew();

        var roadFeatures = FeatureHelper.FilterFeaturesByKeys(features, "highway");
        var roadSegments = FeatureHelper.SplitFeaturesToSegments(roadFeatures);

        roadSegments.Each((i, roadSegment) =>
        {
            Log.D($"MergeSegmentIntoGraph {i}/{roadSegments.Count}");
            hybridGraph.MergeSegmentIntoGraph(hybridGraph, roadSegment);
        });

        Log.D(
            $"{nameof(HybridVisibilityGraphGenerator)}: Merging road network into graph done after {watch.ElapsedMilliseconds}ms");
        Log.D($"  Number of nodes: {hybridGraph.Graph.NodesMap.Count}");
        Log.D($"  Number of edges: {hybridGraph.Graph.EdgesMap.Count}");
    }

    /// <summary>
    /// Takes each feature with an attribute name "poi" and adds all attributes of this feature to the closest node
    /// (within the given distance) in the graph.
    /// </summary>
    public static void AddAttributesToPoiNodes(IEnumerable<IFeature> features, ISpatialGraph graph,
        double nodeDistanceTolerance = 0.001)
    {
        features.Where(f => f.Attributes.Exists("poi"))
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