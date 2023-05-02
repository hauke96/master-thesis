using HybridVisibilityGraphRouting.IO;
using Mars.Common;
using Mars.Common.Collections;
using Mars.Common.Collections.Graph;
using Mars.Common.Collections.Graph.Algorithms;
using Mars.Interfaces.Environments;
using Mars.Numerics;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using ServiceStack;
using Position = Mars.Interfaces.Environments.Position;

namespace HybridVisibilityGraphRouting.Geometry;

public class HybridVisibilityGraph
{
    public static readonly Func<EdgeData, NodeData, double> WeightedHeuristic =
        (edge, _) => edge.Length * (edge.Data.IsEmpty() ? 1 : 0.5);

    public static readonly Func<EdgeData, NodeData, double> ShortestHeuristic = (edge, _) => edge.Length;

    private readonly QuadTree<Obstacle> _obstacles;
    private readonly Dictionary<Vertex, int[]> _vertexToNodes;
    private readonly Dictionary<int, (double, double)> _nodeToAngleArea;
    private readonly QuadTree<int> _edgeIndex;
    private KdTree<NodeData> _nodeIndex;

    public readonly SpatialGraph Graph;
    public BoundingBox BoundingBox => Graph.BoundingBox;

    public HybridVisibilityGraph(SpatialGraph graph,
        QuadTree<Obstacle> obstacles,
        Dictionary<Vertex, int[]> vertexToNodes,
        Dictionary<int, (double, double)> nodeToAngleArea)
    {
        Graph = graph;
        _obstacles = obstacles;
        _vertexToNodes = vertexToNodes;
        _nodeToAngleArea = nodeToAngleArea;

        // Create and fill a spatial index with all edges of the graph
        _edgeIndex = new QuadTree<int>();
        Graph.Edges.Values.Each((i, e) =>
        {
            var envelope = GeometryHelper.GetEnvelope(e.Geometry);
            _edgeIndex.Insert(envelope, i);
        });

        // Create and fill a spatial index with all nodes of the graph
        InitNodeIndex();
    }

    private void InitNodeIndex()
    {
        _nodeIndex = new KdTree<NodeData>(2);
        Graph.NodesMap.Values.Each(node => { _nodeIndex.Add(node.Position, node); });
    }

    public List<Position> WeightedShortestPath(Position source, Position target)
    {
        return OptimalPath(source, target, WeightedHeuristic);
    }

    public List<Position> ShortestPath(Position source, Position target)
    {
        return OptimalPath(source, target, ShortestHeuristic);
    }

    public List<Position> OptimalPath(Position source, Position target, Func<EdgeData, NodeData, double> heuristic)
    {
        // TODO: This is highly inefficient but the KdTree implementation has no ".Remove()" method. Implementing my own method should solve this issue.
        InitNodeIndex();
        
        var (sourceNode, isSourceNodeTemporary) = AddPositionToGraph(source);
        var (targetNode, isTargetNodeTemporary) = AddPositionToGraph(target);
        Exporter.WriteGraphToFile(Graph, "graph-with-source-target.geojson");

        var routingResult = Graph.AStarAlgorithm(sourceNode, targetNode, heuristic);

        // Remove temporarily created nodes (which automatically removes the edges too) to have a clean graph for
        // further routing requests.
        if (isSourceNodeTemporary)
        {
            RemoveNode(sourceNode);
        }

        if (isTargetNodeTemporary)
        {
            RemoveNode(targetNode);
        }

        Exporter.WriteGraphToFile(Graph, "graph-restored.geojson");

        return routingResult
            .Aggregate(new List<Position>(), (list, edge) =>
            {
                var positions = edge.Geometry;

                if (list.Any() && list.Last().Equals(positions[0]))
                {
                    // Skip the first position if the last position of the list equals the first position to avoid duplicates. 
                    positions = positions.Skip(1).ToArray();
                }

                list.AddRange(positions);

                return list;
            })
            .ToList();
    }

    private (int, bool) AddPositionToGraph(Position positionToAdd)
    {
        var existingNodeCandidates = _nodeIndex.Nearest(positionToAdd.PositionArray, 0.000001);
        if (existingNodeCandidates.Any())
        {
            return (existingNodeCandidates[0].Node.Value.Key, false);
        }

        var nodeToAdd = Graph.AddNode(positionToAdd.X, positionToAdd.Y).Key;
        var vertexToAdd = new Vertex(positionToAdd.ToCoordinate());

        // TODO If performance too bad: Pass multiple positions to not calculate certain things twice.
        var allVertices = _vertexToNodes.Keys.ToList();
        var visibilityNeighborVertices =
            VisibilityGraphGenerator.GetVisibilityNeighborsForVertex(_obstacles, allVertices,
                new Dictionary<Coordinate, List<Obstacle>>(),
                vertexToAdd, 36, 10)[0];

        visibilityNeighborVertices
            .Map(v => _vertexToNodes[v])
            .Map(nodeCandidates =>
            {
                // We have all corresponding nodes for the given vertex ("nodeCandidates") but we only want the one node
                // whose angle area includes the vertex to add. So its angle area should include the angle from that
                // node candidate to the vertex.
                return Enumerable.First(nodeCandidates, nodeCandidate =>
                        // The angle area has same "from" and "to" value, which means it covers a range of 360°. In this
                        // case, no further checks are needed since this node candidate is definitely the one we want
                        // to connect to.
                        _nodeToAngleArea[nodeCandidate].Item1 == _nodeToAngleArea[nodeCandidate].Item2
                        ||
                        // In case the angles are not identical, we need to perform a check is the vertex is within the
                        // covered angle area of this node candidate.
                        Angle.IsBetweenEqual(
                            _nodeToAngleArea[nodeCandidate].Item1,
                            Angle.GetBearing(Graph.NodesMap[nodeCandidate].Position, positionToAdd),
                            _nodeToAngleArea[nodeCandidate].Item2
                        ));
            })
            .Each(node =>
            {
                // Create the bi-directional edge between node to add and this visibility node and collect its IDs for
                // later clean up.
                Graph.AddEdge(nodeToAdd, node);
                Graph.AddEdge(node, nodeToAdd);
            });

        return (nodeToAdd, true);
    }

    /// <summary>
    /// Merges the given road segment into the graph.
    /// </summary>
    /// <param name="hybridGraph">The graph with other edges this road segment might intersect. New nodes and edges might be added.</param>
    /// <param name="roadSegment">The road segment to add. This must be a real segment, meaning a line string with exactly two coordinates. Any further coordinates will be ignored.</param>
    public void MergeSegmentIntoGraph(HybridVisibilityGraph hybridGraph, IFeature roadSegment)
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

        // 1. Split visibility edges at the road segment.
        // Get all IDs of visibility edges that truly intersect the road edge. After this, all edges to split at the
        // points where they intersect the road segment new nodes and edges are created to connect everything. 
        hybridGraph.GetEdgesWithin(roadSegment.Geometry.EnvelopeInternal).Each(visibilityEdgeId =>
        {
            var visibilityEdge = hybridGraph.Graph.Edges[visibilityEdgeId];
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

            // 1.1. Add intersection node (the node there the visibility edge and the road edge intersect). A new node
            // is only added when there's no existing node at the intersection points.
            var intersectionNode = hybridGraph.GetOrCreateNodeAt(hybridGraph.Graph, intersectionCoordinate.ToPosition())
                .Key;
            intersectionNodeIds.Add(intersectionNode);

            // Check if any new edges would be created.
            var edge1WouldBeCreated = visibilityEdge.From == intersectionNode ||
                                      hybridGraph.ContainsEdge(visibilityEdge.From, intersectionNode);
            var edge2WouldBeCreated = intersectionNode == visibilityEdge.To ||
                                      hybridGraph.ContainsEdge(intersectionNode, visibilityEdge.To);
            if (edge1WouldBeCreated && edge2WouldBeCreated)
            {
                // In case no new edges would be created, we can skip this step and proceed with the next visibility
                // edge. An edge is only created if the two nodes are unequal and the edge does not already exist.
                return;
            }

            // 1.2. Add two new edges
            hybridGraph.AddEdge(visibilityEdge.From, intersectionNode);
            hybridGraph.AddEdge(intersectionNode, visibilityEdge.To);

            // 1.3. Remove old visibility edge, which was replaced by the two new edges above.
            hybridGraph.RemoveEdge(visibilityEdge);
        });

        // 2. Split road segment at visibility edges.
        // If there are any intersection nodes: Add new line segments between the intersection nodes for the whole
        // road segment
        if (intersectionNodeIds.Any())
        {
            var orderedNodeIds = intersectionNodeIds
                .OrderBy(nodeId => Distance.Euclidean(hybridGraph.Graph.NodesMap[nodeId].Position.PositionArray,
                    roadFeatureFromPosition.PositionArray));

            // Find or create from-node and to-node of the unsplitted road segment
            var fromNode = hybridGraph.GetOrCreateNodeAt(hybridGraph.Graph, roadFeatureFromPosition);
            var toNode = hybridGraph.GetOrCreateNodeAt(hybridGraph.Graph, roadFeatureToPosition);

            // Add the first new segment from the from-node to the first intersection points, then iterate over all
            // intersection points to create new edges between them and finally add the last segment to the to-node.
            using var enumerator = orderedNodeIds.GetEnumerator();
            enumerator.MoveNext();
            var currentNodeId = enumerator.Current;

            hybridGraph.AddEdge(fromNode.Key, currentNodeId, roadSegment.Attributes.ToObjectDictionary());
            hybridGraph.AddEdge(currentNodeId, fromNode.Key, roadSegment.Attributes.ToObjectDictionary());

            while (enumerator.MoveNext())
            {
                var previousNodeId = currentNodeId;
                currentNodeId = enumerator.Current;

                hybridGraph.AddEdge(previousNodeId, currentNodeId, roadSegment.Attributes.ToObjectDictionary());
                hybridGraph.AddEdge(currentNodeId, previousNodeId, roadSegment.Attributes.ToObjectDictionary());
            }

            hybridGraph.AddEdge(currentNodeId, toNode.Key, roadSegment.Attributes.ToObjectDictionary());
            hybridGraph.AddEdge(toNode.Key, currentNodeId, roadSegment.Attributes.ToObjectDictionary());
        }
        else
        {
            var fromNode = hybridGraph.GetOrCreateNodeAt(hybridGraph.Graph, roadFeatureFromPosition);
            var toNode = hybridGraph.GetOrCreateNodeAt(hybridGraph.Graph, roadFeatureToPosition);
            hybridGraph.AddEdge(fromNode.Key, toNode.Key, roadSegment.Attributes.ToObjectDictionary());
        }
    }

    public List<NodeData> GetNodesByAttribute(string attributeName)
    {
        return Graph.NodesMap
            .Values
            .Where(node => node.Data.ContainsKey(attributeName))
            .ToList();
    }

    /// <summary>
    /// Gets the nearest node at the given location. Is no node was found, a new one is created and returned.
    /// </summary>
    private NodeData GetOrCreateNodeAt(SpatialGraph graph, Position position)
    {
        NodeData node;

        var potentialNodes = _nodeIndex.Nearest(position.PositionArray, 0.000001);
        if (potentialNodes.IsEmpty())
        {
            // No nodes found within the radius -> create a new node
            node = graph.AddNode(position.X, position.Y);
            _nodeIndex.Add(position, node);
        }
        else
        {
            // Take one of the nodes, they are all close enough and therefore we can't say for sure which one to take.
            node = potentialNodes[0].Node.Value;
        }

        return node;
    }

    private IList<int> GetEdgesWithin(Envelope envelope)
    {
        return _edgeIndex.Query(envelope);
    }

    private bool ContainsEdge(int fromNode, int toNode)
    {
        return Graph.EdgesMap.ContainsKey((fromNode, toNode));
    }

    private void AddEdge(int fromNode, int toNode)
    {
        AddEdge(fromNode, toNode, new Dictionary<string, object>());
    }

    private void AddEdge(int fromNode, int toNode, IDictionary<string, object> attributes)
    {
        var hasEdge = ContainsEdge(fromNode, toNode);

        if (fromNode != toNode && !hasEdge)
        {
            var edge = Graph.AddEdge(fromNode, toNode, attributes);
            _edgeIndex.Insert(GeometryHelper.GetEnvelope(edge.Geometry), edge.Key);
        }
    }

    private void RemoveNode(int nodeId)
    {
        var node = Graph.NodesMap[nodeId];
        Graph.RemoveNode(node);
    }

    private void RemoveEdge(EdgeData edge)
    {
        Graph.RemoveEdge(edge.Key);
        _edgeIndex.Remove(GeometryHelper.GetEnvelope(edge.Geometry), edge.Key);
    }
}