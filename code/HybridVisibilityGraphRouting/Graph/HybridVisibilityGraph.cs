using HybridVisibilityGraphRouting.IO;
using Mars.Common;
using Mars.Common.Collections;
using Mars.Common.Collections.Graph;
using Mars.Common.Collections.Graph.Algorithms;
using Mars.Interfaces.Environments;
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
        var (sourceNode, isSourceNodeTemporary) = AddPositionToGraph(source);
        var (targetNode, isTargetNodeTemporary) = AddPositionToGraph(target);
        Exporter.WriteGraphToFile(Graph, "graph-with-source-target.geojson");

        var routingResult = Graph.AStarAlgorithm(sourceNode, targetNode, heuristic);

        // Remove temporarily created nodes (which automatically removes the edges too) to have a clean graph for
        // further routing requests.
        if (isSourceNodeTemporary)
        {
            Graph.RemoveNode(Graph.NodesMap[sourceNode]);
        }

        if (isTargetNodeTemporary)
        {
            Graph.RemoveNode(Graph.NodesMap[targetNode]);
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
        var existingNodeCandidates = Graph
            .NodesMap
            .Values
            .Where(n => n.Position.DistanceInMTo(positionToAdd) < 0.001)
            .ToList();
        if (existingNodeCandidates.Any())
        {
            return (existingNodeCandidates[0].Key, false);
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
                return nodeCandidates
                    .First(nodeCandidate =>
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

    public List<NodeData> GetNodesByAttribute(string attributeName)
    {
        return Graph.NodesMap
            .Values
            .Where(node => node.Data.ContainsKey(attributeName))
            .ToList();
    }
}