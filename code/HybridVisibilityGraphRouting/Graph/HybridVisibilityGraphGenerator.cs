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

        QuadTree<Obstacle>? obstacles = null;
        Dictionary<Vertex, List<List<Vertex>>>? vertexNeighbors = null;
        HybridVisibilityGraph? hybridVisibilityGraph = null;
        SpatialGraph? spatialGraph = null;
        double time;

        if (PerformanceMeasurement.CurrentRun != null)
        {
            PerformanceMeasurement.CurrentRun.AllInputVertices =
                features.SelectMany(f => f.Geometry.Coordinates).Distinct().Count();
        }

        // GetObstacles
        PerformanceMeasurement.TimestampGraphGenerationGetObstacleStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        time = PerformanceMeasurement.AddFunctionDurationToCurrentRun(
            () => { obstacles = GetObstacles(features, obstacleExpressions); },
            "get_obstacle_time"
        );
        Log.D($"{nameof(HybridVisibilityGraphGenerator)}: get_obstacle_time done after {time}ms");
        ArgumentNullException.ThrowIfNull(obstacles);

        // DetermineVisibilityNeighbors
        time = PerformanceMeasurement.AddFunctionDurationToCurrentRun(
            () =>
            {
                vertexNeighbors = VisibilityGraphGenerator.CalculateVisibleKnn(obstacles, visibilityNeighborBinCount,
                    visibilityNeighborsPerBin);
            },
            "knn_search_time"
        );
        Log.D($"{nameof(HybridVisibilityGraphGenerator)}: get_knn_search_time done after {time}ms");
        ArgumentNullException.ThrowIfNull(vertexNeighbors);

        // AddVisibilityVerticesAndEdges
        PerformanceMeasurement.TimestampGraphGenerationCreateGraphStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        time = PerformanceMeasurement.AddFunctionDurationToCurrentRun(
            () =>
            {
                (hybridVisibilityGraph, spatialGraph) = AddVisibilityVerticesAndEdges(vertexNeighbors, obstacles);
            },
            "build_graph_time"
        );
        Log.D($"{nameof(HybridVisibilityGraphGenerator)}: get_knn_search_time done after {time}ms");
        ArgumentNullException.ThrowIfNull(hybridVisibilityGraph);
        ArgumentNullException.ThrowIfNull(spatialGraph);

        if (PerformanceMeasurement.CurrentRun != null)
        {
            PerformanceMeasurement.CurrentRun.VisibilityEdgesBeforeMerging =
                spatialGraph.Edges.Values.Count(e => e.Data.IsEmpty());
        }

        // MergeRoadsIntoGraph
        if (PerformanceMeasurement.CurrentRun != null)
        {
            var roadFeatures = FeatureHelper.FilterFeaturesByExpressions(features, roadExpressions!).ToList();
            var roadSegments = FeatureHelper.SplitFeaturesToSegments(roadFeatures);

            PerformanceMeasurement.CurrentRun.RoadEdges = roadFeatures.Count;
            PerformanceMeasurement.CurrentRun.RoadVertices =
                roadSegments.SelectMany(s => s.Geometry.Coordinates).Distinct().Count();
        }

        time = PerformanceMeasurement.AddFunctionDurationToCurrentRun(
            () => { MergeRoadsIntoGraph(features, hybridVisibilityGraph, roadExpressions); },
            "merge_road_graph_time"
        );
        Log.D($"{nameof(HybridVisibilityGraphGenerator)}: merge_road_graph_time done after {time}ms");

        if (PerformanceMeasurement.CurrentRun != null)
        {
            PerformanceMeasurement.CurrentRun.VisibilityEdgesAfterMerging =
                spatialGraph.Edges.Values.Count(e => e.Data.IsEmpty());
            PerformanceMeasurement.CurrentRun.RoadEdgesAfterMerging =
                spatialGraph.Edges.Values.Count(e => !e.Data.IsEmpty());
            PerformanceMeasurement.CurrentRun.RoadVerticesAfterMerging =
                spatialGraph.Edges.Values.Where(e => !e.Data.IsEmpty()).SelectMany(e => e.Geometry).Distinct().Count();
        }

        // AddAttributesToPoiNodes
        time = PerformanceMeasurement.AddFunctionDurationToCurrentRun(
            () => { AddAttributesToPoiNodes(features, spatialGraph, 0.001, poiExpressions); },
            "add_poi_attributes_time"
        );
        Log.D($"{nameof(HybridVisibilityGraphGenerator)}: add_poi_attributes_time done after {time}ms");

        Log.D($"{nameof(HybridVisibilityGraphGenerator)}: Done after {watch.ElapsedMilliseconds}ms");
        return hybridVisibilityGraph!;
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
                    return g.Value.ConvexHull().Coordinates;
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
                if (vertex.ObstacleNeighbors.Count > 1)
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
                    // Check for both vertices if the generated edge would be in valid angle areas for both
                    // vertices. As soon as there is one vertex where the line would *not* be in any valid angle
                    // area, this means that this edge will never be part of any shortest path. Therefore we can
                    // therefore abort here.
                    var angleFromSourceToTarget = Angle.GetBearing(vertex.Coordinate, targetVertex.Coordinate);
                    if (!vertex.ValidAngleAreas.Any(area =>
                            Angle.IsBetweenEqual(area.Item1, angleFromSourceToTarget, area.Item2)))
                    {
                        return;
                    }

                    var angleFromTargetToSource = Angle.Normalize(angleFromSourceToTarget - 180);
                    if (!targetVertex.ValidAngleAreas.Any(area =>
                            Angle.IsBetweenEqual(area.Item1, angleFromTargetToSource, area.Item2)))
                    {
                        return;
                    }

                    // Get the correct node to connect to.
                    var targetVertexNodes = GetNodeForAngle(vertex.Coordinate.ToPosition(), vertexToNode[targetVertex],
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
                    if (targetVertexNodes.Any())
                    {
                        var targetVertexNode = targetVertexNodes.First();

                        // Check if the node's angle area is >=180°, in other words, check if this is a convex corner.
                        var isSourceVertexConvex = Angle.Difference(nodeToAngleArea[vertexNode].Item1,
                            nodeToAngleArea[vertexNode].Item2) >= 180;
                        var isTargetVertexConvex = Angle.Difference(nodeToAngleArea[targetVertexNode].Item1,
                            nodeToAngleArea[targetVertexNode].Item2) >= 180;
                        if (!isSourceVertexConvex || !isTargetVertexConvex)
                        {
                            return;
                        }

                        // Actually create bidirectional edges, if not already exist.
                        if (!graph.EdgesMap.ContainsKey((vertexNode, targetVertexNode)))
                        {
                            graph.AddEdge(vertexNode, targetVertexNode);
                        }

                        if (!graph.EdgesMap.ContainsKey((targetVertexNode, vertexNode)))
                        {
                            graph.AddEdge(targetVertexNode, vertexNode);
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
        PerformanceMeasurement.TimestampGraphGenerationMergePrepareStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        roadExpressions ??= DefaultRoadExpressions;

        var watch = Stopwatch.StartNew();

        var roadFeatures = FeatureHelper.FilterFeaturesByExpressions(features, roadExpressions)
            .Where(f => f.Geometry.OgcGeometryType != OgcGeometryType.Point)
            .SelectMany(f =>
            {
                return GeometryHelper.UnwrapMultiGeometries(f.Geometry)
                    .Map(g => new NetTopologySuite.Features.Feature(g, f.Attributes));
            })
            .ToList();
        var roadSegments = FeatureHelper.SplitFeaturesToSegments(roadFeatures);

        // To determine dead-ends and to connect them to the graph (enables the routing to better connect to roads),
        // a mapping from coordinate to features with this coordinate is created and used below to detect dead-ends.
        var coordinateToFeatures = new Dictionary<Coordinate, ICollection<IFeature>>();
        roadFeatures.Each(feature =>
        {
            // Store the feature mapping for each coordinate. This allows the detection of e.g. T-shaped crossings
            // where the end of one linestring has only one neighbor but is no dead-end. 
            feature
                .Geometry
                .Coordinates
                .Each(coordinate =>
                {
                    if (!coordinateToFeatures.ContainsKey(coordinate))
                    {
                        coordinateToFeatures[coordinate] = new HashSet<IFeature>();
                    }

                    coordinateToFeatures[coordinate].Add(feature);
                });
        });

        // Add the dead-ends (coordinates belonging to only one road) to graph
        coordinateToFeatures
            .Where(pair => pair.Value.Count == 1)
            .Each(pair =>
            {
                // Only one feature exists (-> above .Where clause)
                var roadFeature = pair.Value.First();
                
                // Check if the coordinate is really the dead-end. 
                var isDeadEndCoordinate = pair.Key.Equals(roadFeature.Geometry.Coordinates[0]) ||
                                          pair.Key.Equals(roadFeature.Geometry.Coordinates[^1]);
                if (!isDeadEndCoordinate)
                {
                    return;
                }
                
                // We ignore valid angle areas for dead ends, since e.g. building passages might end at a building and
                // therefore this dead-end would never be connected to anything, which is not the wanted behavior.
                // Also the fact whether or not the vertex was created or not is ignored because of the same reason:
                // The end-vertex of a building passage ending at a building already exists but we still want to
                // connect the dead-end.
                var (node, _) = hybridGraph.AddPositionToGraph(pair.Key.ToPosition());
                hybridGraph.ConnectNodeToGraph(node, true, false);
            });

        PerformanceMeasurement.TimestampGraphGenerationMergeInsertStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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
    /// Filters the given node candidates and keeps each node for which the given position is within its angle area.
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