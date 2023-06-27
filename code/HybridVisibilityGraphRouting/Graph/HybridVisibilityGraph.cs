using HybridVisibilityGraphRouting.Geometry;
using HybridVisibilityGraphRouting.IO;
using Mars.Common;
using Mars.Common.Collections;
using Mars.Common.Collections.Graph;
using Mars.Common.Collections.Graph.Algorithms;
using Mars.Common.Core.Collections;
using Mars.Interfaces.Environments;
using Mars.Numerics;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Distance;
using ServiceStack;
using Feature = NetTopologySuite.Features.Feature;
using Position = Mars.Interfaces.Environments.Position;

namespace HybridVisibilityGraphRouting.Graph;

public class HybridVisibilityGraph
{
    public static readonly Func<EdgeData, NodeData, double> WeightedHeuristic =
        (edge, _) => edge.Length * (edge.Data.IsEmpty() ? 1 : 0.8);

    public static readonly Func<EdgeData, NodeData, double> ShortestHeuristic = (edge, _) => edge.Length;

    private readonly QuadTree<Obstacle> _obstacles;
    private readonly Dictionary<Vertex, int[]> _vertexToNodes;
    private readonly Dictionary<Coordinate, List<Obstacle>> _vertexToObstacleMapping;
    private readonly Dictionary<int, (double, double)> _nodeToAngleArea;
    private readonly QuadTree<int> _edgeIndex;
    private KdTree<NodeData> _nodeIndex;
    private KdTree<Vertex> _vertexIndex;

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

        // Create and store the mapping from vertex to obstacles for later use
        _vertexToObstacleMapping = VisibilityGraphGenerator.GetCoordinateToObstaclesMapping(_obstacles.QueryAll());

        // Create and fill a spatial index with all nodes of the graph
        InitNodeIndex();
    }

    private void InitNodeIndex()
    {
        _nodeIndex = new KdTree<NodeData>(2);
        Graph.NodesMap.Values.Each(node => { _nodeIndex.Add(node.Position, node); });

        _vertexIndex = new KdTree<Vertex>(2);
        _vertexToNodes.Keys.Each(vertex => _vertexIndex.Add(vertex.Coordinate.ToPosition(), vertex));
    }

    public List<Position> WeightedShortestPath(Position source, Position destination)
    {
        return OptimalPath(source, destination, WeightedHeuristic);
    }

    public List<Position> ShortestPath(Position source, Position destination)
    {
        return OptimalPath(source, destination, ShortestHeuristic);
    }

    public List<Position> OptimalPath(Position source, Position destination, Func<EdgeData, NodeData, double> heuristic)
    {
        var (sourceNode, sourceNodeHasBeenCreated) = AddPositionToGraph(source);
        var (destinationNode, destinationNodeHasBeenCreated) = AddPositionToGraph(destination);

        IList<NodeData> newCreatedNodesForSource = new List<NodeData>();
        IList<EdgeData> newEdgedForSource = new List<EdgeData>();
        IList<NodeData> newCreatedNodesForDestination = new List<NodeData>();
        IList<EdgeData> newEdgedForDestination = new List<EdgeData>();
        
        if (sourceNodeHasBeenCreated)
        {
            (newCreatedNodesForSource, newEdgedForSource) = ConnectNodeToGraph(sourceNode, false);
        }
        if (destinationNodeHasBeenCreated)
        {
            (newCreatedNodesForDestination, newEdgedForDestination) = ConnectNodeToGraph(destinationNode, false);
        }

        Exporter.WriteGraphToFile(Graph, "graph-with-source-destination.geojson");

        var routingResult = Graph.AStarAlgorithm(sourceNode.Key, destinationNode.Key, heuristic);

        // Remove temporarily created nodes (which automatically removes the edges too) to have a clean graph for
        // further routing requests.
        newEdgedForSource.Each(RemoveEdge);
        newEdgedForDestination.Each(RemoveEdge);
        newCreatedNodesForSource.Each(RemoveNode);
        newCreatedNodesForDestination.Each(RemoveNode);
        InitNodeIndex();

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

    /// <summary>
    /// Adds the given position to the graph, if not already exists. When a new node was created, visibility edges are
    /// determined and connected to the graph.
    /// </summary>
    /// <returns>The node and a flag which is true when the node is new (false when the node already existed).</returns>
    public (NodeData, bool) AddPositionToGraph(Position positionToAdd)
    {
        var existingNodeCandidates = _nodeIndex.Nearest(positionToAdd.PositionArray, 0.000001);
        if (existingNodeCandidates.Any())
        {
            return (existingNodeCandidates[0].Node.Value, false);
        }

        var addedNode = Graph.AddNode(positionToAdd.X, positionToAdd.Y);
        InitNodeIndex();

        var vertex = new Vertex(addedNode.Position.ToCoordinate(), true);
        _vertexToNodes[vertex] = new[] { addedNode.Key };
        _vertexIndex.Add(vertex.Coordinate.ToPosition(), vertex);

        return (addedNode, true);
    }

    /// <summary>
    /// Determines all visibility edges for the given node and adds them to the graph. The node itself will be part of
    /// the returned result set and therefore might be removed in other steps.
    /// </summary>
    /// <returns>A tuple with a list of all newly added nodes (including the given nodeToConnect) and edges.</returns>
    public (IList<NodeData>, IList<EdgeData>) ConnectNodeToGraph(NodeData nodeToConnect,
        bool onlyConnectToObstacles = true,
        int visibilityNeighborBinCount = 36,
        int visibilityNeighborsPerBin = 10)
    {
        var vertexToConnect = _vertexToNodes.First(pair => pair.Value.Contains(nodeToConnect.Key)).Key;
        var newNodes = new List<NodeData> { nodeToConnect };
        var newEdges = new List<EdgeData>();

        var allVertices = _vertexToNodes.Keys.AsEnumerable();
        if (onlyConnectToObstacles)
        {
            allVertices = allVertices.Where(v => _vertexToObstacleMapping.ContainsKey(v.Coordinate));
        }

        var visibilityNeighborVertices = VisibilityGraphGenerator.GetVisibilityNeighborsForVertex(_obstacles,
            allVertices.ToList(), _vertexToObstacleMapping, vertexToConnect, visibilityNeighborBinCount,
            visibilityNeighborsPerBin)[0];

        visibilityNeighborVertices
            .Map(v => _vertexToNodes[v])
            .Where(nodeCandidates =>
                !nodeCandidates
                    .IsEmpty()) // can happen due to convex-hull filtering so that not every vertex is represented by a node 
            .Each(nodeCandidates =>
            {
                GetNodeForAngle(nodeToConnect.Position, nodeCandidates)
                    .Each(node =>
                    {
                        // Create the bi-directional edge between node to add and this visibility node and collect its IDs for
                        // later clean up.
                        newEdges.Add(Graph.AddEdge(nodeToConnect.Key, node));
                        newEdges.Add(Graph.AddEdge(node, nodeToConnect.Key));
                    });
            });

        newEdges.CreateCopy().ForEach(edge =>
        {
            newEdges.Remove(edge);
            Graph.RemoveEdge(edge.Key);

            var feature = new Feature(
                new LineString(new[] { edge.Geometry[0].ToCoordinate(), edge.Geometry[1].ToCoordinate() }),
                new AttributesTable()
            );
            var (nodes, edges) = MergeSegmentIntoGraph(this, feature, true, false);

            newEdges.AddRange(edges);
            newNodes.AddRange(nodes);
        });

        return (newNodes, newEdges);
    }

    /// <summary>
    /// Merges the given line segment into the graph.
    /// </summary>
    /// <param name="hybridGraph">The graph in which the given segment feature should be merged to. New nodes and edges might be added.</param>
    /// <param name="segmentFeature">The line segment to add. This must have a line geometry, meaning a <code>LineSegment</code> or <code>LineString</code>. Only the first two coordinates are considered, any additional coordinates will be ignored.</param>
    /// <param name="onlyConsiderRoads">Set to true if only intersections with road segments should be considered. Otherwise all intersections of the given segment with any edge will be considered.</param>
    /// <param name="removeVacantEdges">Set to true to remove edges from the graph that have been split into two segments.</param>
    public (IEnumerable<NodeData>, IEnumerable<EdgeData>) MergeSegmentIntoGraph(HybridVisibilityGraph hybridGraph,
        IFeature segmentFeature, bool onlyConsiderRoads = false, bool removeVacantEdges = true)
    {
        var segmentFromCoordinate = segmentFeature.Geometry.Coordinates[0];
        var segmentToCoordinate = segmentFeature.Geometry.Coordinates[1];
        var segmentFromPosition = segmentFromCoordinate.ToPosition();
        var segmentToPosition = segmentToCoordinate.ToPosition();
        var segmentAttributes = segmentFeature.Attributes.ToObjectDictionary();
        var segment = new LineSegment(
            segmentFromCoordinate,
            segmentToCoordinate
        );

        var newNodes = new HashSet<NodeData?>();
        var newEdges = new HashSet<EdgeData?>();

        // For every edge intersecting the given segment a new node might be created at the intersection point. Maybe
        // there's already a node there, which will be reused. In both cases, all nodes are collected to correctly
        // subdivide the segment into smaller edges between all intersection points.
        var intersectionNodeIds = new HashSet<NodeData>();

        // 1. Split existing edges at the given segment.
        // Get all IDs of existing edges that truly intersect the segment. After this, all edges are split at the
        // points where the edge intersects the segment. New nodes and edges are created to connect everything. 
        hybridGraph.GetEdgesWithin(segmentFeature.Geometry.EnvelopeInternal).Each(existingEdgeId =>
        {
            var existingEdge = hybridGraph.Graph.Edges[existingEdgeId];
            if (onlyConsiderRoads && !existingEdge.Data.ContainsKey("highway"))
            {
                // We only want to consider roads and this edge is not a road -> continue with next edge
                return;
            }

            var edgePositionFrom = existingEdge.Geometry[0].ToCoordinate();
            var edgePositionTo = existingEdge.Geometry[1].ToCoordinate();
            var segmentAndEdgeIntersectOrTouch =
                Intersect.DoIntersectOrTouch(segmentFromCoordinate, segmentToCoordinate, edgePositionFrom,
                    edgePositionTo);

            if (!segmentAndEdgeIntersectOrTouch)
            {
                return;
            }

            var existingEdgeLineSegment = new LineSegment(
                existingEdge.Geometry[0].X,
                existingEdge.Geometry[0].Y,
                existingEdge.Geometry[1].X,
                existingEdge.Geometry[1].Y
            );

            // This neighbor is needed to get the correct node in case this edge ends at an existing obstacle node. In
            // such case, the right node needs to be determined relative to this neighbors since each node covers a
            // certain angle area.
            // We use coordinate 0 as intersection neighbor since it's the start coordinate of the segment, but this
            // has no further meaning.
            var intersectionNeighbor = segment.GetCoordinate(0);
            var intersectionCoordinate = segment.Intersection(existingEdgeLineSegment);
            if (intersectionCoordinate == null)
            {
                // We know that the two line segments are intersection or touching. In the touching-case it can happen
                // that the ".Intersection" method returns null due to float inaccuracies.
                var distance = DistanceOp.Distance(
                    existingEdgeLineSegment.ToGeometry(GeometryFactory.Default),
                    new Point(existingEdge.Geometry[0].ToCoordinate())
                );
                if (distance < 0.0001)
                {
                    intersectionCoordinate = existingEdge.Geometry[0].ToCoordinate();
                    intersectionNeighbor = existingEdge.Geometry[1].ToCoordinate();
                }
                else
                {
                    intersectionCoordinate = existingEdge.Geometry[1].ToCoordinate();
                    intersectionNeighbor = existingEdge.Geometry[0].ToCoordinate();
                }
            }

            // 1.1. Add intersection node (the node where the existing edge and the segment intersect). A new node
            // is only added when there's no existing node at the intersection which angle area fits to the
            // intersection neighbor.
            var (intersectionNodes, createdIntersectionNode) =
                hybridGraph.GetOrCreateNodeAt(intersectionCoordinate.ToPosition());
            var intersectionNode = Graph.NodesMap[GetNodeForAngle(intersectionNeighbor, intersectionNodes)];
            intersectionNodeIds.Add(intersectionNode);
            if (createdIntersectionNode)
            {
                newNodes.Add(intersectionNode);
            }

            // Check if any new edges would be created.
            var edge1WouldNotBeCreated = existingEdge.From == intersectionNode.Key ||
                                         hybridGraph.ContainsEdge(existingEdge.From, intersectionNode.Key);
            var edge2WouldNotBeCreated = intersectionNode.Key == existingEdge.To ||
                                         hybridGraph.ContainsEdge(intersectionNode.Key, existingEdge.To);
            if (edge1WouldNotBeCreated && edge2WouldNotBeCreated)
            {
                // In case no new edges would be created, we can skip this step and proceed with the next existing
                // edge. An edge is only created if the two nodes are unequal and the edge does not already exist.
                return;
            }

            // 1.2. Add two new edges
            newEdges.Add(hybridGraph.AddEdge(existingEdge.From, intersectionNode.Key));
            newEdges.Add(hybridGraph.AddEdge(intersectionNode.Key, existingEdge.To));

            // 1.3. Remove old existing edge, which was replaced by the two new edges above.
            if (removeVacantEdges)
            {
                hybridGraph.RemoveEdge(existingEdge);
            }
        });

        // 2. Split the segment at existing edges.

        // Find or create from-node and to-node of the unsplit segment
        var (fromNodes, fromNodeHasBeenCreated) = hybridGraph.GetOrCreateNodeAt(segmentFromPosition);
        var fromNode = Graph.NodesMap[GetNodeForAngle(segmentToCoordinate, fromNodes)];
        if (fromNodeHasBeenCreated)
        {
            newNodes.Add(fromNode);
        }

        var (toNodes, toNodeHasBeenCreated) = hybridGraph.GetOrCreateNodeAt(segmentToPosition);
        var toNode = Graph.NodesMap[GetNodeForAngle(segmentFromCoordinate, toNodes)];
        if (toNodeHasBeenCreated)
        {
            newNodes.Add(toNode);
        }

        // If there are any intersection nodes:
        // Add new line segments between the intersection nodes for the whole segment
        if (intersectionNodeIds.Any())
        {
            var orderedNodes = intersectionNodeIds
                .OrderBy(node =>
                    Distance.Euclidean(node.Position.PositionArray, segmentFromPosition.PositionArray));

            // Add the first new segment from the from-node to the first intersection points, then iterate over all
            // intersection points to create new edges between them and finally add the last segment to the to-node.
            using var enumerator = orderedNodes.GetEnumerator();
            enumerator.MoveNext();
            var currentNodeId = enumerator.Current.Key;

            newEdges.Add(hybridGraph.AddEdge(fromNode.Key, currentNodeId, segmentAttributes));
            newEdges.Add(hybridGraph.AddEdge(currentNodeId, fromNode.Key, segmentAttributes));

            while (enumerator.MoveNext())
            {
                var previousNodeId = currentNodeId;
                currentNodeId = enumerator.Current.Key;

                newEdges.Add(hybridGraph.AddEdge(previousNodeId, currentNodeId,
                    segmentAttributes));
                newEdges.Add(hybridGraph.AddEdge(currentNodeId, previousNodeId,
                    segmentAttributes));
            }

            newEdges.Add(hybridGraph.AddEdge(currentNodeId, toNode.Key, segmentAttributes));
            newEdges.Add(hybridGraph.AddEdge(toNode.Key, currentNodeId, segmentAttributes));
        }
        else
        {
            newEdges.Add(hybridGraph.AddEdge(fromNode.Key, toNode.Key, segmentAttributes));
            newEdges.Add(hybridGraph.AddEdge(toNode.Key, fromNode.Key, segmentAttributes));
        }

        return (
            newNodes.Where(n => n != null).Map(n => (NodeData)n),
            newEdges.Where(e => e != null).Map(e => (EdgeData)e)
        );
    }

    public List<NodeData> GetNodesByAttribute(string attributeName)
    {
        return Graph.NodesMap
            .Values
            .Where(node => node.Data.ContainsKey(attributeName))
            .ToList();
    }

    private int GetNodeForAngle(Coordinate coordinate, IEnumerable<NodeData> nodeCandidates)
    {
        return GetNodeForAngle(coordinate.ToPosition(), nodeCandidates.Map(n => n.Key)).First();
    }

    /// <summary>
    /// Determines the correct node for the given position based on the angle from the position to the node. This method
    /// does *not* find the node *at* this position. Therefore, the <code>position</code> parameter can be seen as a
    /// neighbor of the given node candidates.
    /// </summary>
    public IEnumerable<int> GetNodeForAngle(Position position, IEnumerable<int> nodeCandidates)
    {
        // We have all corresponding nodes for the given position ("nodeCandidates") but we only want the one node
        // whose angle area includes the position to add. So its angle area should include the angle from that
        // node candidate to the position.
        return Enumerable.Where(nodeCandidates, nodeCandidate =>
            // There are some cases:
            // 1. The node does not exist in the dictionary. This happens for nodes that were added during a routing
            // request. We assume they cover a 360° area.
            !_nodeToAngleArea.ContainsKey(nodeCandidate)
            ||
            // 2. The angle area has equal "from" and "to" value, which means it covers a range of 360°. In this case,
            // no further checks are needed since this node candidate is definitely the one we want to connect to.
            _nodeToAngleArea[nodeCandidate].Item1 == _nodeToAngleArea[nodeCandidate].Item2
            ||
            // 3. In case the angles are not identical, we need to perform a check is the position is within the covered
            // angle area of this node candidate.
            Angle.IsBetweenEqual(
                _nodeToAngleArea[nodeCandidate].Item1,
                Angle.GetBearing(Graph.NodesMap[nodeCandidate].Position, position),
                _nodeToAngleArea[nodeCandidate].Item2
            ));
    }

    /// <summary>
    /// Gets the nearest node at the given location. Is no node was found, a new one is created and returned.
    /// </summary>
    private (IEnumerable<NodeData>, bool) GetOrCreateNodeAt(Position position)
    {
        var nodes = new HashSet<NodeData>();
        var createdNewNode = false;

        var potentialNodes = _nodeIndex.Nearest(position.PositionArray, 0.000001);
        if (potentialNodes.IsEmpty())
        {
            // No nodes found within the radius -> create a new node
            var node = Graph.AddNode(position.X, position.Y);
            nodes.Add(node);
            _nodeIndex.Add(position, node);
            createdNewNode = true;
        }
        else
        {
            // Take one of the nodes, they are all close enough and therefore we can't say for sure which one to take.
            nodes.AddRange(potentialNodes.Map(n => n.Node.Value));
        }

        return (nodes, createdNewNode);
    }

    private IList<int> GetEdgesWithin(Envelope envelope)
    {
        return _edgeIndex.Query(envelope);
    }

    private bool ContainsEdge(int fromNode, int toNode)
    {
        return Graph.EdgesMap.ContainsKey((fromNode, toNode));
    }

    /// <returns>The new edge or null if from & to are equal or if the edge already existed.</returns>
    private EdgeData? AddEdge(int fromNode, int toNode)
    {
        return AddEdge(fromNode, toNode, new Dictionary<string, object>());
    }

    /// <returns>The new edge or null if from & to are equal or if the edge already existed.</returns>
    private EdgeData? AddEdge(int fromNode, int toNode, IDictionary<string, object> attributes)
    {
        var hasEdge = ContainsEdge(fromNode, toNode);
        if (fromNode == toNode || hasEdge)
        {
            return null;
        }

        var edge = Graph.AddEdge(fromNode, toNode, attributes);
        _edgeIndex.Insert(GeometryHelper.GetEnvelope(edge.Geometry), edge.Key);
        return edge;
    }

    private void RemoveNode(NodeData node)
    {
        Graph.RemoveNode(node);
        _vertexIndex.Nearest(node.Position.PositionArray, 0.000001)
            .Each(kdNode => _vertexToNodes.Remove(kdNode.Node.Value));
    }

    private void RemoveEdge(EdgeData edge)
    {
        Graph.RemoveEdge(edge.Key);
        _edgeIndex.Remove(GeometryHelper.GetEnvelope(edge.Geometry), edge.Key);
    }
}