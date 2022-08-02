using Mars.Numerics;
using Wavefront.Geometry;

namespace Wavefront;

// TODO Tests
public class WavefrontPreprocessor
{
    public static Dictionary<Vertex, List<Vertex>> CalculateKnn(List<Vertex> vertices, int neighborCount)
    {
        var result = new Dictionary<Vertex, List<Vertex>>();

        foreach (var vertex in vertices)
        {
            result[vertex] = GetNeighborsForVertex(new List<Vertex>(vertices), vertex, neighborCount);
        }

        return result;
    }

    public static List<Vertex> GetNeighborsForVertex(List<Vertex> vertices, Vertex vertex, int neighborCount)
    {
        var neighborList = new List<Vertex>();

        var sortedVertices = vertices.OrderBy(v => Distance.Euclidean(vertex.Position.PositionArray, v.Position.PositionArray)).ToList();

        for (var i = 0; i < sortedVertices.Count && neighborList.Count < neighborCount; i++)
        {
            var otherVertex = sortedVertices[i];
            if (Equals(otherVertex, vertex))
            {
                continue;
            }

            neighborList.Add(otherVertex);
        }

        return neighborList;
    }
}