using System.Diagnostics;
using HybridVisibilityGraphRouting.Geometry;
using Mars.Common;
using Mars.Common.Collections;
using Mars.Common.Collections.Graph;
using Mars.Common.Core.Collections;
using Mars.Interfaces.Environments;
using Mars.Numerics;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using ServiceStack;
using Position = Mars.Interfaces.Environments.Position;

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

        var obstacles = GetObstacles(features);
        var vertexNeighbors = DetermineVisibilityNeighbors(obstacles);
        var (hybridVisibilityGraph, spatialGraph) = AddVisibilityVerticesAndEdges(vertexNeighbors, obstacles);

        MergeRoadsIntoGraph(features, spatialGraph);
        AddAttributesToPoiNodes(features, spatialGraph);

        Console.WriteLine(
            $"{nameof(HybridVisibilityGraphGenerator)}: Done after {watch.ElapsedMilliseconds}ms");
        return hybridVisibilityGraph;
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

        var coordinateToVertex = obstacleGeometries
            .Map(g => g.Coordinates)
            .SelectMany(x => x)
            .Distinct()
            .ToDictionary(c => c, c => new Vertex(c));

        var obstacleIndex = new QuadTree<Obstacle>();
        obstacleGeometries.Each(geometry =>
        {
            var vertices = geometry.Coordinates.Map(c => coordinateToVertex[c]).ToList();
            obstacleIndex.Insert(geometry.EnvelopeInternal, new Obstacle(geometry, vertices));
        });

        Console.WriteLine(
            $"{nameof(HybridVisibilityGraphGenerator)}: Splitting obstacles done after {watch.ElapsedMilliseconds}ms");
        return obstacleIndex;
    }

    public static Dictionary<Vertex, List<List<Vertex>>> DetermineVisibilityNeighbors(QuadTree<Obstacle> obstacles)
    {
        var watch = Stopwatch.StartNew();

        var vertexNeighbors = VisibilityGraphGenerator.CalculateVisibleKnn(obstacles, 36, 10, true);

        Console.WriteLine(
            $"{nameof(HybridVisibilityGraphGenerator)}: CalculateVisibleKnn done after {watch.ElapsedMilliseconds}ms");
        return vertexNeighbors;
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

                    otherVertexNodes.Each(otherVertexNode =>
                    {
                        nodeNeighbors[vertexNode].Add(otherVertexNode);
                        graph.AddEdge(vertexNode, otherVertexNode);
                    });
                });
            });
        });

        Console.WriteLine(
            $"{nameof(HybridVisibilityGraphGenerator)}: Graph creation done after {watch.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Number of nodes: {graph.NodesMap.Count}");
        Console.WriteLine($"  Number of edges: {graph.EdgesMap.Count}");

        var hybridVisibilityGraph = new HybridVisibilityGraph(graph, obstacles, vertexToNode, nodeToAngleArea);
        return (hybridVisibilityGraph, graph);
    }

    /// <summary>
    /// This merges all features with a "highway=*" attribute into the given graph. Whenever a road-edge intersects
    /// an existing edge, both edges will be split at the intersection point where a new node is added.
    /// </summary>
    public static void MergeRoadsIntoGraph(IEnumerable<IFeature> features, SpatialGraph graph)
    {
        var watch = Stopwatch.StartNew();

        // Create and fill a spatial index with all edges of the graph
        var edgeIndex = new QuadTree<int>();
        graph.Edges.Values.Each((i, e) =>
        {
            var envelope = GeometryHelper.GetEnvelopeOfEdge(e);
            edgeIndex.Insert(envelope, i);
        });

        // Create and fill a spatial index with all nodes of the graph
        var nodeIndex = new KdTree<NodeData>(2);
        graph.NodesMap.Values.Each(node => { nodeIndex.Add(node.Position, node); });

        var roadFeatures = FeatureHelper.FilterFeaturesByKeys(features, "highway");
        var roadSegments = FeatureHelper.SplitFeaturesToSegments(roadFeatures);

        roadSegments.Each((i, roadSegment) =>
        {
            Console.WriteLine($"MergeSegmentIntoGraph {i}/{roadSegments.Count}");
            MergeSegmentIntoGraph(graph, edgeIndex, nodeIndex, roadSegment);
        });

        Console.WriteLine(
            $"{nameof(HybridVisibilityGraphGenerator)}: Merging road network into graph done after {watch.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Number of nodes: {graph.NodesMap.Count}");
        Console.WriteLine($"  Number of edges: {graph.EdgesMap.Count}");
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

    /// <summary>
    /// Merges the given road segment into the graph.
    /// </summary>
    /// <param name="graph">The graph with other edges this road segment might intersect. New nodes and edges might be added.</param>
    /// <param name="edgeIndex">An index to quickly get the edge keys in a certain bounding box.</param>
    /// <param name="nodeIndex">An index to quickly get the nodes in a certain bounding box.</param>
    /// <param name="roadSegment">The road segment to add. This must be a real segment, meaning a line string with exactly two coordinates. Any further coordinates will be ignored.</param>
    private static void MergeSegmentIntoGraph(SpatialGraph graph, QuadTree<int> edgeIndex, KdTree<NodeData> nodeIndex,
        IFeature roadSegment)
    {
        var roadFeatureFrom = roadSegment.Geometry.Coordinates[0];
        var roadFeatureTo = roadSegment.Geometry.Coordinates[1];
        var roadFeatureFromPosition = roadFeatureFrom.ToPosition();
        var roadFeatureToPosition = roadFeatureTo.ToPosition();
        var roadEdgeLineSegment = new LineSegment(
            roadFeatureFrom,
            roadFeatureTo
        );

        // For every edge intersecting the given road segment a new node might be created at the intersection point.
        // Maybe there's already a node there, which will be reused. In both cases, all nodes are collected to correctly
        // subdivide the road segment into smaller edges between all intersection points.
        var intersectionNodeIds = new HashSet<int>();

        // Get all IDs of visibility edges that truly intersect the road edge. After this, all edges to split at the
        // points where they intersect the road segment new nodes and edges are created to connect everything. 
        edgeIndex.Query(roadSegment.Geometry.EnvelopeInternal).Each(visibilityEdgeId =>
        {
            var visibilityEdge = graph.Edges[visibilityEdgeId];
            var edgePositionFrom = visibilityEdge.Geometry[0].ToCoordinate();
            var edgePositionTo = visibilityEdge.Geometry[1].ToCoordinate();
            var roadAndEdgeIntersectOrTouch =
                Intersect.DoIntersectOrTouch(roadFeatureFrom, roadFeatureTo, edgePositionFrom, edgePositionTo);

            if (!roadAndEdgeIntersectOrTouch)
            {
                return;
            }

            var visibilityEdgeLineSegment = new LineSegment(
                visibilityEdge.Geometry[0].X,
                visibilityEdge.Geometry[0].Y,
                visibilityEdge.Geometry[1].X,
                visibilityEdge.Geometry[1].Y
            );

            var intersectionCoordinate = roadEdgeLineSegment.Intersection(visibilityEdgeLineSegment);

            // 1. Add intersection node (the node there the visibility edge and the road edge intersect). A new node
            // is only added when there's no existing node at the intersection points.
            var intersectionNode = GetOrCreateNodeAt(graph, nodeIndex, intersectionCoordinate.ToPosition()).Key;
            intersectionNodeIds.Add(intersectionNode);

            // 2. Add two new edges
            var newEdge = graph.AddEdge(visibilityEdge.From, intersectionNode);
            edgeIndex.Insert(GeometryHelper.GetEnvelopeOfEdge(newEdge), newEdge.Key);

            newEdge = graph.AddEdge(intersectionNode, visibilityEdge.To);
            edgeIndex.Insert(GeometryHelper.GetEnvelopeOfEdge(newEdge), newEdge.Key);

            // 3. Remove old visibility edge
            edgeIndex.Remove(GeometryHelper.GetEnvelopeOfEdge(visibilityEdge), visibilityEdgeId);
            graph.RemoveEdge(visibilityEdgeId);
        });

        // 4. If there are any intersection nodes: Add new line segments between the intersection nodes for the whole
        // road segment
        if (intersectionNodeIds.Any())
        {
            var orderedNodeIds = intersectionNodeIds
                .OrderBy(nodeId => Distance.Euclidean(graph.NodesMap[nodeId].Position.PositionArray,
                    roadFeatureFromPosition.PositionArray));

            // Find or create from-node and to-node of the unsplitted road segment
            var fromNode = GetOrCreateNodeAt(graph, nodeIndex, roadFeatureFromPosition);
            var toNode = GetOrCreateNodeAt(graph, nodeIndex, roadFeatureToPosition);

            // Add the first new segment from the from-node to the first intersection points, then iterate over all
            // intersection points to create new edges between them and finally add the last segment to the to-node.
            using var enumerator = orderedNodeIds.GetEnumerator();
            enumerator.MoveNext();
            var currentNodeId = enumerator.Current;

            graph.AddEdge(fromNode.Key, currentNodeId, roadSegment.Attributes.ToObjectDictionary());
            graph.AddEdge(currentNodeId, fromNode.Key, roadSegment.Attributes.ToObjectDictionary());

            while (enumerator.MoveNext())
            {
                var previousNodeId = currentNodeId;
                currentNodeId = enumerator.Current;

                graph.AddEdge(previousNodeId, currentNodeId, roadSegment.Attributes.ToObjectDictionary());
                graph.AddEdge(currentNodeId, previousNodeId, roadSegment.Attributes.ToObjectDictionary());
            }

            graph.AddEdge(currentNodeId, toNode.Key, roadSegment.Attributes.ToObjectDictionary());
            graph.AddEdge(toNode.Key, currentNodeId, roadSegment.Attributes.ToObjectDictionary());
        }
        else
        {
            var fromNode = GetOrCreateNodeAt(graph, nodeIndex, roadFeatureFromPosition);
            var toNode = GetOrCreateNodeAt(graph, nodeIndex, roadFeatureToPosition);
            graph.AddEdge(fromNode.Key, toNode.Key, roadSegment.Attributes.ToObjectDictionary());
        }
    }

    /// <summary>
    /// Gets the nearest node at the given location. Is no node was found, a new one is created and returned.
    /// </summary>
    private static NodeData GetOrCreateNodeAt(SpatialGraph graph, KdTree<NodeData> nodeIndex, Position position)
    {
        NodeData node;

        var potentialNodes = nodeIndex.Nearest(position.PositionArray, 0.000001);
        if (potentialNodes.IsEmpty())
        {
            // No nodes found within the radius -> create a new node
            node = graph.AddNode(position.X, position.Y);
            nodeIndex.Add(position, node);
        }
        else
        {
            // Take one of the nodes, they are all close enough and therefore we can't say for sure which one to take.
            node = potentialNodes[0].Node.Value;
        }

        return node;
    }
}