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
using Position = Mars.Interfaces.Environments.Position;

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
        "railway!=^(abandoned)$",
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
            .Map(g =>
            {
                if (g.Value.OgcGeometryType == OgcGeometryType.Polygon)
                {
                    // Only take the convex hull of polygons into account. Line based obstacles might bend around other
                    // obstacles so that the convex hull would prevent the generation of important edges.
                    return g.Value.Coordinates;
                }

                return g.Value.Coordinates;
            })
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
        var nodeToAngleArea = new Dictionary<int, (double, double)>();
        var vertices = vertexNeighbors.Keys;

        // Create a node for every vertex in the dataset. Also store the mapping between node keys and vertices.
        vertices.Each(vertex =>
        {
            List<List<Vertex>> vertexNeighborBin =
                vertex.IsOnConvexHull ? vertexNeighbors[vertex] : new List<List<Vertex>>();

            vertexToNode[vertex] = new int[vertexNeighborBin.Count];
            vertexNeighborBin.Each((i, _) =>
            {
                // For debug porposes to see the different nodes in the GeoJSON file.
                // var nodePosition = PositionHelper.CalculatePositionByBearing(vertex.Position.X, vertex.Position.Y,
                //     360 / vertexNeighborBin.Count * i, 0.000005);

                var nodeKey = graph.AddNode(vertex.Coordinate.X, vertex.Coordinate.Y, new Dictionary<string, object>())
                    .Key;
                vertexToNode[vertex][i] = nodeKey;

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
        vertices.Each(vertex =>
        {
            if (!vertex.IsOnConvexHull)
            {
                return;
            }

            var neighborBins = vertexNeighbors[vertex];
            neighborBins.Each((i, neighborBin) =>
            {
                var vertexNode = vertexToNode[vertex][i];

                // Connect each visibility neighbor to the node of the current vertex.
                neighborBin.Each(targetVertex =>
                {
                    // Get the correct node to connect to.
                    var targetVertexNode = GetNodeForAngle(vertex.Coordinate.ToPosition(), vertexToNode[targetVertex],
                            nodeToAngleArea, graph.NodesMap)
                        .Where(node =>
                            // If "node" is not an obstacle neighbor, then the filtering from GetNodeForAngle was
                            // already sufficient. For obstacle neighbors: Each obstacle neighbor is in two neighbor
                            // bins and we need to find out which one is the correct.
                            !vertex.ObstacleNeighbors.Contains(targetVertex.Coordinate.ToPosition()) ||
                            // If the to- and from-angle of the current vertex are equal (=360° covering area), then
                            // we definitely want to connect to the target, since this is the end of a line.
                            Angle.AreEqual(nodeToAngleArea[vertexNode].Item1, nodeToAngleArea[vertexNode].Item2) ||
                            // If the to- and from-angle of the target vertex are equal (=360° covering area), then
                            // we definitely want to connect to it, since it is the end of a line.
                            Angle.AreEqual(nodeToAngleArea[node].Item1, nodeToAngleArea[node].Item2) ||
                            // Only connect to obstacle neighbors if the from-angle of the current nodes angle area
                            // meets the to-angle of the target nodes angle area (or vice versa). If both to-angle
                            // areas (or from-angles) meet, then we would connect wrong nodes with each other.
                            Angle.AreEqual(nodeToAngleArea[vertexNode].Item1, nodeToAngleArea[node].Item2 - 180) ||
                            Angle.AreEqual(nodeToAngleArea[vertexNode].Item2, nodeToAngleArea[node].Item1 - 180))
                        .ToList();

                    // Due to the valid angle area filtering, it can indeed happen, that a vertex has no
                    // corresponding node (e.g. for a T-shaped crossing where no angle area is >180°).
                    if (targetVertexNode.Any())
                    {
                        if (!graph.EdgesMap.ContainsKey((vertexNode, targetVertexNode.First())))
                        {
                            graph.AddEdge(vertexNode, targetVertexNode.First());
                        }

                        if (!graph.EdgesMap.ContainsKey((targetVertexNode.First(), vertexNode)))
                        {
                            graph.AddEdge(targetVertexNode.First(), vertexNode);
                        }
                    }
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
                Log.D($"MergeSegmentIntoGraph {i}/{roadSegments.Count} ({i / (roadSegments.Count / 100)}%)");
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

    /// <summary>
    /// Determines the correct node for the given position based on the angle from the position to the node. This method
    /// does *not* find the node *at* this position. Therefore, the <code>position</code> parameter can be seen as a
    /// neighbor of the given node candidates.
    /// </summary>
    public static IEnumerable<int> GetNodeForAngle(Position position, IEnumerable<int> nodeCandidates,
        Dictionary<int, (double, double)> nodeToAngleArea, IDictionary<int, NodeData> nodesMap)
    {
        // We have all corresponding nodes for the given position ("nodeCandidates") but we only want the one node
        // whose angle area includes the position to add. So its angle area should include the angle from that
        // node candidate to the position.
        return nodeCandidates.Where(nodeCandidate =>
            // There are some cases:
            // 1. The node does not exist in the dictionary. This happens for nodes that were added during a routing
            // request. We assume they cover a 360° area.
            !nodeToAngleArea.ContainsKey(nodeCandidate)
            ||
            // 2. The angle area has equal "from" and "to" value, which means it covers a range of 360°. In this case,
            // no further checks are needed since this node candidate is definitely the one we want to connect to.
            nodeToAngleArea[nodeCandidate].Item1 == nodeToAngleArea[nodeCandidate].Item2
            ||
            // 3. In case the angles are not identical, we need to perform a check is the position is within the covered
            // angle area of this node candidate.
            Angle.IsBetweenEqual(
                nodeToAngleArea[nodeCandidate].Item1,
                Angle.GetBearing(nodesMap[nodeCandidate].Position, position),
                nodeToAngleArea[nodeCandidate].Item2
            )
        );
    }
}