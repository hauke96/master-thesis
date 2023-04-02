using HybridVisibilityGraphRouting.IO;
using Mars.Common.Collections;
using Mars.Common.Collections.Graph;
using Mars.Common.Collections.Graph.Algorithms;
using Mars.Interfaces.Environments;
using ServiceStack;

namespace HybridVisibilityGraphRouting.Geometry;

public class HybridVisibilityGraph
{
    public static readonly Func<EdgeData, NodeData, double> WeightedHeuristic =
        (edge, _) => edge.Length * (edge.Data.IsEmpty() ? 1 : 0.5);

    public static readonly Func<EdgeData, NodeData, double> ShortestHeuristic = (edge, _) => edge.Length;

    private readonly SpatialGraph _graph;
    private readonly QuadTree<Obstacle> _obstacles;
    private readonly Dictionary<Vertex, int[]> _vertexToNodes;
    private readonly Dictionary<int, (double, double)> _nodeToAngleArea;

    public BoundingBox BoundingBox => _graph.BoundingBox;

    public HybridVisibilityGraph(SpatialGraph graph,
        QuadTree<Obstacle> obstacles,
        Dictionary<Vertex, int[]> vertexToNodes,
        Dictionary<int, (double, double)> nodeToAngleArea)
    {
        _graph = graph;
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
        Exporter.WriteGraphToFile(_graph, "graph-with-source-target.geojson");

        var routingResult = _graph.AStarAlgorithm(sourceNode, targetNode, heuristic);

        // Remove temporarily created nodes (which automatically removes the edges too) to have a clean graph for
        // further routing requests.
        if (isSourceNodeTemporary)
        {
            _graph.RemoveNode(_graph.NodesMap[sourceNode]);
        }

        if (isTargetNodeTemporary)
        {
            _graph.RemoveNode(_graph.NodesMap[targetNode]);
        }

        Exporter.WriteGraphToFile(_graph, "graph-restored.geojson");

        return routingResult.Map(e => e.Geometry).SelectMany(x => x).ToList();
    }

    private (int, bool) AddPositionToGraph(Position source)
    {
        var sourceNodeCandidates = _graph
            .NodesMap
            .Values
            .Where(n => n.Position.DistanceInMTo(source) < 0.1)
            .ToList();
        if (sourceNodeCandidates.Any())
        {
            return (sourceNodeCandidates[0].Key, false);
        }

        var sourceNode = _graph.AddNode(new Dictionary<string, object>
        {
            { "x", source.X },
            { "y", source.Y },
        }).Key;

        // TODO If performance too bad: Pass multiple positions to not calculate certain things twice.
        var sourceVisibilityNeighborVertices =
            VisibilityGraphGenerator.GetVisibilityNeighborsForPosition(_obstacles, source)[0];
        var sourceVisibilityNeighborNodes = sourceVisibilityNeighborVertices
            .Map(v => _vertexToNodes[v])
            .Map(nodeCandidates =>
            {
                // We have all corresponding nodes for the given vertex ("nodeCandidates") but we only want the one node whose angle area includes the source vertex. So its angle area should include the angle from that node to the source vertex.
                return nodeCandidates
                    .First(nodeCandidate =>
                        Angle.IsBetweenEqual(
                            _nodeToAngleArea[nodeCandidate].Item1,
                            Angle.GetBearing(_graph.NodesMap[nodeCandidate].Position, source),
                            _nodeToAngleArea[nodeCandidate].Item2
                        ));
            })
            .Map(node =>
            {
                // Create the bi-directional edge between source node and this visibility node and collect its IDs.
                return new[]
                {
                    _graph.AddEdge(sourceNode, node),
                    _graph.AddEdge(node, sourceNode)
                };
            })
            .SelectMany(x => x)
            .ToList();

        return (sourceNode, true);
    }

    public List<NodeData> GetNodesByAttribute(string attributeName)
    {
        return _graph.NodesMap
            .Values
            .Where(node => node.Data.ContainsKey(attributeName))
            .ToList();
    }
}