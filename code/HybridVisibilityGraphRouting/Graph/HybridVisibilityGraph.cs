using HybridVisibilityGraphRouting.Geometry;
using HybridVisibilityGraphRouting.IO;
using Mars.Common;
using Mars.Common.Collections;
using Mars.Common.Collections.Graph;
using Mars.Common.Collections.Graph.Algorithms;
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
        int sourceNodeKey = -1;
        IList<NodeData>? newCreatedNodesForSource = null;
        IList<EdgeData>? newEdgedForSource = null;
        int destinationNodeKey = -1;
        IList<NodeData>? newCreatedNodesForDestination = null;
        IList<EdgeData>? newEdgedForDestination = null;

        var time = PerformanceMeasurement.AddFunctionDurationToCurrentRun(
            () =>
            {
                var (sourceNode, sourceNodeHasBeenCreated) = AddPositionToGraph(source);
                var (destinationNode, destinationNodeHasBeenCreated) = AddPositionToGraph(destination);

                if (sourceNodeHasBeenCreated)
                {
                    (newCreatedNodesForSource, newEdgedForSource) = ConnectNodeToGraph(sourceNode, false);
                }

                if (destinationNodeHasBeenCreated)
                {
                    (newCreatedNodesForDestination, newEdgedForDestination) =
                        ConnectNodeToGraph(destinationNode, false);
                }

                sourceNodeKey = sourceNode.Key;
                destinationNodeKey = destinationNode.Key;
            },
            "add_positions_to_graph_time"
        );
        Log.D($"{nameof(HybridVisibilityGraph)}: add_positions_to_graph_time done after {time}ms");

        if (!PerformanceMeasurement.IsActive)
        {
            Exporter.WriteGraphToFile(Graph, $"graph-with-source-destination_{source}-to-{destination}.geojson");
        }

        // AddVisibilityVerticesAndEdges
        IList<EdgeData> routingResult = new List<EdgeData>();
        time = PerformanceMeasurement.AddFunctionDurationToCurrentRun(
            () => { routingResult = Graph.AStarAlgorithm(sourceNodeKey, destinationNodeKey, heuristic); },
            "astar_time"
        );
        Log.D($"{nameof(HybridVisibilityGraph)}: astar_time done after {time}ms");

        // Remove temporarily created nodes (which automatically removes the edges too) to have a clean graph for
        // further routing requests.
        time = PerformanceMeasurement.AddFunctionDurationToCurrentRun(
            () =>
            {
                newEdgedForSource.Each(RemoveEdge);
                newEdgedForDestination.Each(RemoveEdge);
                newCreatedNodesForSource.Each(RemoveNode);
                newCreatedNodesForDestination.Each(RemoveNode);
                InitNodeIndex();
            },
            "restore_graph"
        );
        Log.D($"{nameof(HybridVisibilityGraph)}: restore_graph done after {time}ms");

        if (routingResult.IsEmpty())
        {
            return new List<Position>();
        }

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
    /// </summary>
    /// <returns>A tuple with a list of all newly added nodes (including the given nodeToConnect) and edges.</returns>
    /// <summary>
    /// Determines all visibility edges for the given node and adds them to the graph. The node itself will be part of
    /// the returned result set and therefore might be removed in other steps.
    /// </summary>
    /// <param name="nodeToConnect">The node from with edges to other visible nodes should be created.</param>
    /// <param name="onlyConnectToObstacles">When true (default), then only edges to vertices which belong to an obstacle are created.</param>
    /// <param name="respectValidAngleAreas">When true (default), then valid angle areas are respected.</param>
    /// <returns></returns>
    public (IList<NodeData>, IList<EdgeData>) ConnectNodeToGraph(NodeData nodeToConnect,
        bool onlyConnectToObstacles = true,
        bool respectValidAngleAreas = true,
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
            visibilityNeighborsPerBin, respectValidAngleAreas)[0];

        visibilityNeighborVertices
            .Each(vertex =>
            {
                if (_vertexToNodes[vertex].IsEmpty())
                {
                    // might happen due to convex-hull filtering so that not every vertex is represented by a node 
                    return;
                }

                HybridVisibilityGraphGenerator
                    .GetNodeForAngle(nodeToConnect.Position, _vertexToNodes[vertex], _nodeToAngleArea, Graph.NodesMap)
                    .Each(node =>
                    {
                        // Create the bi-directional edge between node to add and this visibility node and collect its IDs for
                        // later clean up.
                        if (!Graph.EdgesMap.ContainsKey((nodeToConnect.Key, node)))
                        {
                            newEdges.Add(Graph.AddEdge(nodeToConnect.Key, node));
                        }

                        if (!Graph.EdgesMap.ContainsKey((node, nodeToConnect.Key)))
                        {
                            newEdges.Add(Graph.AddEdge(node, nodeToConnect.Key));
                        }
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
            var (intersectionNodeEnumerable, createdIntersectionNode) =
                hybridGraph.GetOrCreateNodeAt(intersectionCoordinate.ToPosition());
            var intersectionNodes = intersectionNodeEnumerable.ToList();
            var nodesForAngle = GetNodeForAngle(intersectionNeighbor, intersectionNodes).ToList();
            NodeData intersectionNode;
            if (!onlyConsiderRoads && nodesForAngle.IsEmpty())
            {
                // We're in the mode of adding road segments to the graph AND we have not found any valid intersection
                // nodes. This is fine when adding roads because if the road segment, which should be added, intersects
                // with an obstacle-vertex here, then this just means that the obstacle is passable at this location
                // (for example via a gate in a fence). Because no node is clearly better than the other, the first one
                // is taken.
                intersectionNode = intersectionNodes[0];
                // The "intersectionNode" is definitely an existing one because new intersection nodes always cover a
                // 360° angle and are therefore always valid (which was not the case here). Therefore, the node here is
                // not added to the list of new nodes.
            }
            else
            {
                intersectionNode = Graph.NodesMap[nodesForAngle.First()];
                if (createdIntersectionNode)
                {
                    newNodes.Add(intersectionNode);
                }
            }

            intersectionNodeIds.Add(intersectionNode);

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

        // Find or create from-node of the unsplit segment
        var (fromNodesEnumerable, fromNodeHasBeenCreated) = hybridGraph.GetOrCreateNodeAt(segmentFromPosition);
        var fromNodes = fromNodesEnumerable.ToList();
        var fromNodesForAngle = GetNodeForAngle(segmentToCoordinate, fromNodes).ToList();
        NodeData fromNode;
        if (!onlyConsiderRoads && fromNodesForAngle.IsEmpty())
        {
            // We're in the mode of adding road segments to the graph AND we have not found any valid neighbors. This
            // is fine when adding roads because then we're taking the road segment then as-is. For example if two road
            // segments meet at a line-obstacle, this obstacle is passable, because a road leads right through it. Same
            // goes for building passages.
            fromNode = fromNodes[0];
            // The "intersectionNode" is definitely an existing one because new intersection nodes always cover a
            // 360° angle and are therefore always valid (which was not the case here). Therefore, the node here is
            // not added to the list of new nodes.
        }
        else
        {
            fromNode = Graph.NodesMap[fromNodesForAngle[0]];
            if (fromNodeHasBeenCreated)
            {
                newNodes.Add(fromNode);
            }
        }

        // Find or create to-node of the unsplit segment
        var (toNodesEnumerable, toNodeHasBeenCreated) = hybridGraph.GetOrCreateNodeAt(segmentToPosition);
        var toNodes = toNodesEnumerable.ToList();
        var toNodesForAngle = GetNodeForAngle(segmentFromCoordinate, toNodes).ToList();
        NodeData toNode;
        if (!onlyConsiderRoads && toNodesForAngle.IsEmpty())
        {
            // We're in the mode of adding road segments to the graph AND we have not found any valid neighbors. This
            // is fine when adding roads because then we're taking the road segment then as-is. For example if two road
            // segments meet at a line-obstacle, this obstacle is passable, because a road leads right through it. Same
            // goes for building passages.
            toNode = toNodes[0];
            // The "intersectionNode" is definitely an existing one because new intersection nodes always cover a
            // 360° angle and are therefore always valid (which was not the case here). Therefore, the node here is
            // not added to the list of new nodes.
        }
        else
        {
            toNode = Graph.NodesMap[toNodesForAngle[0]];
            if (toNodeHasBeenCreated)
            {
                newNodes.Add(toNode);
            }
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

    public IEnumerable<int> GetNodeForAngle(Coordinate coordinate, IEnumerable<NodeData> nodeCandidates)
    {
        return HybridVisibilityGraphGenerator.GetNodeForAngle(coordinate.ToPosition(), nodeCandidates.Map(n => n.Key),
            _nodeToAngleArea, Graph.NodesMap);
    }

    /// <summary>
    /// Gets the nearest node at the given location. If no node was found, a new one is created, added to the node
    /// index and returned. This means whenever the returned boolean is "true", the enumerable has exactly one element.
    /// </summary>
    private (IEnumerable<NodeData>, bool) GetOrCreateNodeAt(Position position)
    {
        var potentialNodes = _nodeIndex.Nearest(position.PositionArray, 0.000001);
        if (!potentialNodes.IsEmpty())
        {
            return (potentialNodes.Map(n => n.Node.Value), false);
        }

        // No nodes found within the radius -> create a new node
        var node = Graph.AddNode(position.X, position.Y);
        _nodeIndex.Add(position, node);
        return (new[] { node }, true);
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