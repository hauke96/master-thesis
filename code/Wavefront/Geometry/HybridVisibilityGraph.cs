using Mars.Common.Collections;
using Mars.Common.Collections.Graph;
using Mars.Common.Collections.Graph.Algorithms;
using Mars.Interfaces.Environments;
using ServiceStack;

namespace Wavefront.Geometry;

public class HybridVisibilityGraph
{
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

    public IList<EdgeData> ShortestPath(Position source, Position target)
    {
        // TODO Add source and target to graph, determine visibility neighbors, create edges and clean up graph afterwards

        var sourceNode = 0;
        var targetNode = 0;

        return AStar.AStarAlgorithm(_graph, 0, 0,
            (edge, _) => edge.Length * (edge.Data.IsEmpty() ? 1 : 0.1));
    }

    public List<NodeData> GetNodesByAttribute(string attributeName)
    {
        return _graph.NodesMap
            .Values
            .Where(node => node.Data.ContainsKey(attributeName))
            .ToList();
    }
}