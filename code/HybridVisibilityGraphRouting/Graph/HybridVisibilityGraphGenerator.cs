using System.Diagnostics;
using HybridVisibilityGraphRouting.Geometry;
using HybridVisibilityGraphRouting.IO;
using Mars.Common;
using Mars.Common.Collections;
using Mars.Common.Collections.Graph;
using Mars.Common.Core.Collections;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Quadtree;
using ServiceStack;
using Feature = NetTopologySuite.Features.Feature;
using Position = Mars.Interfaces.Environments.Position;

namespace HybridVisibilityGraphRouting;

public static class HybridVisibilityGraphGenerator
{
    /// <summary>
    /// Generates the complete hybrid visibility graph based on the obstacles in the given feature collection. This
    /// method also merges the road and ways within the features correctly with the visibility edges.
    /// </summary>
    public static HybridVisibilityGraph Generate(ICollection<IVectorFeature> features)
    {
        var graph = new SpatialGraph();

        var obstacles = GetObstacleNeighbors(features);
        var vertexNeighbors = DetermineVisibilityNeighbors(obstacles);
        var hybridVisibilityGraph = AddVisibilityVerticesAndEdges(vertexNeighbors, graph, obstacles);

        MergeRoadsIntoGraph(features, graph);
        AddAttributesToPOIs(features, graph);
        Exporter.WriteGraphToFile(graph);

        return hybridVisibilityGraph;
    }

    /// <summary>
    /// Takes all obstacle features and calculates for each vertex the visibility neighbors.
    /// </summary>
    /// <returns>A map from each vertex to the bins of visibility neighbors.</returns>
    private static QuadTree<Obstacle> GetObstacleNeighbors(ICollection<IVectorFeature> features)
    {
        var importedObstacles = features.Where(f =>
            {
                return f.VectorStructured.Attributes.GetNames().Any(name =>
                {
                    var lowerName = name.ToLower();
                    return lowerName.Contains("building") || lowerName.Contains("barrier") ||
                           lowerName.Contains("natural") || lowerName.Contains("poi");
                });
            })
            .Map(f => f.VectorStructured as IFeature);

        var watch = Stopwatch.StartNew();

        var obstacles = GeometryHelper.UnwrapAndTriangulate(importedObstacles, true);

        var obstacleIndex = new QuadTree<Obstacle>();
        obstacles.Each(obstacle => obstacleIndex.Insert(obstacle.EnvelopeInternal, new Obstacle(obstacle)));

        Console.WriteLine(
            $"{nameof(HybridVisibilityGraphGenerator)}: Splitting obstacles done after {watch.ElapsedMilliseconds}ms");
        return obstacleIndex;
    }

    private static Dictionary<Vertex, List<List<Vertex>>> DetermineVisibilityNeighbors(QuadTree<Obstacle> obstacles)
    {
        var watch = Stopwatch.StartNew();

        var vertexNeighbors = VisibilityGraphGenerator.CalculateVisibleKnn(obstacles, 36, 10, true);

        Console.WriteLine(
            $"{nameof(HybridVisibilityGraphGenerator)}: CalculateVisibleKnn done after {watch.ElapsedMilliseconds}ms");
        return vertexNeighbors;
    }

    private static Geometry.HybridVisibilityGraph AddVisibilityVerticesAndEdges(
        Dictionary<Vertex, List<List<Vertex>>> vertexNeighbors,
        SpatialGraph graph,
        QuadTree<Obstacle> obstacles)
    {
        var watch = Stopwatch.StartNew();
        var vertexToNode = new Dictionary<Vertex, int[]>();
        var nodeToBinVertices = new Dictionary<int, List<Vertex>>();
        var nodeToAngleArea = new Dictionary<int, (double, double)>();
        var allVertices = vertexNeighbors.Keys;

        // Create a node for every vertex in the dataset. Also store the mapping between node keys and vertices.
        allVertices.Each(vertex =>
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
        allVertices.Each(vertex =>
        {
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

        Console.WriteLine(
            $"{nameof(HybridVisibilityGraphGenerator)}: Graph creation done after {watch.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Number of nodes: {graph.NodesMap.Count}");
        Console.WriteLine($"  Number of edges: {graph.EdgesMap.Count}");

        return new Geometry.HybridVisibilityGraph(graph, obstacles, vertexToNode, nodeToAngleArea);
    }

    private static void MergeRoadsIntoGraph(ICollection<IVectorFeature> features, SpatialGraph graph)
    {
        var watch = Stopwatch.StartNew();

        AddRoadsToGraph(graph, features);

        Console.WriteLine(
            $"{nameof(HybridVisibilityGraphGenerator)}: Merging road network into graph done after {watch.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Number of nodes: {graph.NodesMap.Count}");
        Console.WriteLine($"  Number of edges: {graph.EdgesMap.Count}");
    }

    /// <summary>
    /// Searches for each feature with an attribute name "poi" and adds all attributes of this feature to the according
    /// node in the given graph.
    /// </summary>
    private static void AddAttributesToPOIs(IEnumerable<IVectorFeature> features, ISpatialGraph graph,
        double nodeDistanceTolerance = 0.1)
    {
        features.Where(f => f.VectorStructured.Attributes.Exists("poi"))
            .Each(f =>
            {
                var featurePosition = f.VectorStructured.Geometry.Coordinates[0].ToPosition();
                var nearestNodes = graph
                    .NodesMap
                    .Values
                    .Where(n => n.Position.DistanceInMTo(featurePosition) < nodeDistanceTolerance)
                    .ToList();

                if (!nearestNodes.IsEmpty())
                {
                    nearestNodes[0].Data.AddRange(f.VectorStructured.Attributes.ToObjectDictionary());
                }
            });
    }

    /// <summary>
    /// Adds the road and way features to the graph. In fact they are merges into existing visibility edges, which means
    /// that edges are cut at intersection points and new nodes are added and connected.
    /// </summary>
    private static void AddRoadsToGraph(SpatialGraph graph, ICollection<IVectorFeature> features)
    {
        // Create and fill a spatial index with all edges of the graph
        var edgeIndex = new Quadtree<int>();
        graph.Edges.Values.Each((i, e) =>
        {
            var envelope = GeometryHelper.GetEnvelopeOfEdge(e);
            edgeIndex.Insert(envelope, i);
        });

        var roadFeatures = GetAllRoadFeatures(features);
        var roadSegments = SplitFeaturesToSegments(roadFeatures);

        roadSegments.Each(roadSegment => { MergeSegmentIntoGraph(graph, edgeIndex, roadSegment); });
    }

    /// <summary>
    /// Merges the given road segment into the graph.
    /// </summary>
    /// <param name="graph">The graph with other edges this road segment might intersect. New nodes and edges might be added.</param>
    /// <param name="edgeIndex">An index to quickly get the edge keys in a certain bounding box.</param>
    /// <param name="roadSegment">The road segment to add. This must be a real segment, meaning a line string with exactly two coordinates. Any further coordinates will be ignored.</param>
    private static void MergeSegmentIntoGraph(SpatialGraph graph, Quadtree<int> edgeIndex, Feature roadSegment)
    {
        var roadFeatureFrom = roadSegment.Geometry.Coordinates[0];
        var roadFeatureTo = roadSegment.Geometry.Coordinates[1];
        var roadFeatureFromPosition = roadFeatureFrom.ToPosition();
        var roadFeatureToPosition = roadFeatureTo.ToPosition();

        // Get all IDs of visibility edges that truly intersect the road edge. 
        var edgeIds = edgeIndex.Query(roadSegment.Geometry.EnvelopeInternal).Where(id =>
        {
            var edgePositions = graph.Edges[id].Geometry.Map(p => p.ToCoordinate());
            return Intersect.DoIntersect(roadFeatureFrom, roadFeatureTo, edgePositions[0], edgePositions[1]);
        }).ToList();

        if (edgeIds.Any())
        {
            // We have edges that intersect the given road segment. This means we have to split the edge and the
            // road segment at all intersection points and create new nodes and edges. 

            var roadEdgeLineSegment = new LineSegment(
                roadSegment.Geometry.Coordinates[0],
                roadSegment.Geometry.Coordinates[1]
            );

            // Every edge from the edgeIds list is definitely intersecting our road segment. This means for each
            // edge a new node will be created. To remember what node was created at what location (to connect
            // the nodes), this map stores this information. It also prevent creating duplicate node since all
            // visibility edges were added twice (forward and backward direction).
            var intersectionNodeMap = new Dictionary<Coordinate, int>();

            edgeIds.Each(visibilityEdgeId =>
            {
                var visibilityEdgeLineSegment = new LineSegment(
                    graph.Edges[visibilityEdgeId].Geometry[0].ToCoordinate(),
                    graph.Edges[visibilityEdgeId].Geometry[1].ToCoordinate()
                );
                var visibilityEdge = graph.Edges[visibilityEdgeId];

                var intersectionCoordinate = roadEdgeLineSegment.Intersection(visibilityEdgeLineSegment);

                // 1. Add intersection node
                var intersectionNode = -1;
                if (intersectionNodeMap.ContainsKey(intersectionCoordinate))
                {
                    intersectionNode = intersectionNodeMap[intersectionCoordinate];
                }
                else
                {
                    intersectionNode = graph.AddNode(new Dictionary<string, object>
                    {
                        { "x", intersectionCoordinate.X },
                        { "y", intersectionCoordinate.Y },
                    }).Key;
                    intersectionNodeMap.Add(intersectionCoordinate, intersectionNode);
                }

                // 2. Remove old visibility edge
                edgeIndex.Remove(GeometryHelper.GetEnvelopeOfEdge(graph.Edges[visibilityEdgeId]), visibilityEdgeId);
                graph.RemoveEdge(visibilityEdgeId);

                // 3. Add two new edges
                var newEdge = graph.AddEdge(visibilityEdge.From, intersectionNode);
                edgeIndex.Insert(GeometryHelper.GetEnvelopeOfEdge(newEdge), newEdge.Key);

                newEdge = graph.AddEdge(intersectionNode, visibilityEdge.To);
                edgeIndex.Insert(GeometryHelper.GetEnvelopeOfEdge(newEdge), newEdge.Key);
            });

            // 4. Add new line segments for our whole road segment
            var orderedNodeIds = intersectionNodeMap.Values
                .OrderBy(nodeId => graph.NodesMap[nodeId].Position.DistanceInMTo(roadFeatureFromPosition))
                .ToList();

            // Find or create from-node and to-node of unsplitted road segment
            var fromNode = GetOrCreateNodeAt(graph, roadFeatureFromPosition);
            var toNode = GetOrCreateNodeAt(graph, roadFeatureToPosition);

            // Add first new segment, then iterate over all pieces from the intersection checks above and
            // finally add the last segment to the to-node.
            graph.AddEdge(fromNode.Key, orderedNodeIds[0], roadSegment.Attributes.ToObjectDictionary());
            graph.AddEdge(orderedNodeIds[0], fromNode.Key, roadSegment.Attributes.ToObjectDictionary());

            for (int i = 0; i < orderedNodeIds.Count - 1; i++)
            {
                graph.AddEdge(orderedNodeIds[i], orderedNodeIds[i + 1],
                    roadSegment.Attributes.ToObjectDictionary());
                graph.AddEdge(orderedNodeIds[i + 1], orderedNodeIds[i],
                    roadSegment.Attributes.ToObjectDictionary());
            }

            graph.AddEdge(toNode.Key, orderedNodeIds[^1], roadSegment.Attributes.ToObjectDictionary());
            graph.AddEdge(orderedNodeIds[^1], toNode.Key, roadSegment.Attributes.ToObjectDictionary());
        }
        else
        {
            var fromNode = GetOrCreateNodeAt(graph, roadFeatureFromPosition);
            var toNode = GetOrCreateNodeAt(graph, roadFeatureToPosition);
            graph.AddEdge(fromNode.Key, toNode.Key, roadSegment.Attributes.ToObjectDictionary());
        }
    }

    /// <summary>
    /// Gets the nearest node at the given location. Is no node was found, a new one is created and returned.
    /// </summary>
    private static NodeData GetOrCreateNodeAt(SpatialGraph graph, Position position)
    {
        var node = graph.NodesMap.Values.MinBy(node => node.Position.DistanceInMTo(position));

        if (node.Position.DistanceInMTo(position) > 0.1)
        {
            node = graph.AddNode(position.X, position.Y);
        }

        return node;
    }

    /// <summary>
    /// Determines a list of all segments from the given features. That means, each of the returned line strings has
    /// exactly two coordinates.
    /// </summary>
    private static List<Feature> SplitFeaturesToSegments(IEnumerable<IVectorFeature> roadFeatures)
    {
        return roadFeatures
            // Turn each highway edge into a list of line strings with only two coordinates. This makes it easier to
            // split them at intersection points with visibility edges.
            .Map(f =>
            {
                var features = new List<Feature>();
                var coordinates = f.VectorStructured.Geometry.Coordinates;
                for (int i = 0; i < coordinates.Length - 1; i++)
                {
                    features.Add(new Feature(new LineString(new[] { coordinates[i], coordinates[i + 1] }),
                        f.VectorStructured.Attributes));
                }

                return features;
            })
            .SelectMany(x => x)
            .ToList();
    }

    private static IEnumerable<IVectorFeature> GetAllRoadFeatures(ICollection<IVectorFeature> features)
    {
        return features
            .Where(f =>
            {
                return f.VectorStructured.Attributes.GetNames().Any(name =>
                {
                    var lowerName = name.ToLower();
                    return f.VectorStructured.Geometry is LineString ||
                           lowerName.Contains("highway");
                });
            });
    }
}