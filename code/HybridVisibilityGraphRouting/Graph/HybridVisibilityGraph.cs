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

    private (int, bool) AddPositionToGraph(Position source)
    {
        var sourceNodeCandidates = Graph
            .NodesMap
            .Values
            .Where(n => n.Position.DistanceInMTo(source) < 0.1)
            .ToList();
        if (sourceNodeCandidates.Any())
        {
            return (sourceNodeCandidates[0].Key, false);
        }

        var sourceNode = Graph.AddNode(new Dictionary<string, object>
        {
            { "x", source.X },
            { "y", source.Y },
        }).Key;

        // TODO If performance too bad: Pass multiple positions to not calculate certain things twice.
        var allVertices = _obstacles.QueryAll().Map(o => o.Vertices).SelectMany(x => x).Distinct().ToList();
        var vertex = new Vertex(source.ToCoordinate());
        var sourceVisibilityNeighborVertices =
            VisibilityGraphGenerator.GetVisibilityNeighborsForVertex(_obstacles, allVertices,
                new Dictionary<Coordinate, List<Obstacle>>(),
                vertex, 36, 10)[0];

        sourceVisibilityNeighborVertices
            .Map(v => _vertexToNodes[v])
            .Map(nodeCandidates =>
            {
                // We have all corresponding nodes for the given vertex ("nodeCandidates") but we only want the one node whose angle area includes the source vertex. So its angle area should include the angle from that node to the source vertex.
                return nodeCandidates
                    .First(nodeCandidate =>
                        Angle.IsBetweenEqual(
                            _nodeToAngleArea[nodeCandidate].Item1,
                            Angle.GetBearing(Graph.NodesMap[nodeCandidate].Position, source),
                            _nodeToAngleArea[nodeCandidate].Item2
                        ));
            })
            .Each(node =>
            {
                // Create the bi-directional edge between source node and this visibility node and collect its IDs.
                Graph.AddEdge(sourceNode, node);
                Graph.AddEdge(node, sourceNode);
            });

        return (sourceNode, true);
    }

    public List<NodeData> GetNodesByAttribute(string attributeName)
    {
        return Graph.NodesMap
            .Values
            .Where(node => node.Data.ContainsKey(attributeName))
            .ToList();
    }
}