using Mars.Common;
using Mars.Numerics;
using NetTopologySuite.Geometries;
using Wavefront.Geometry;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront;

// TODO Tests
public class WavefrontPreprocessor
{
    public static Dictionary<Vertex, List<Vertex>> CalculateKnn(List<Obstacle> obstacles, List<Vertex> vertices,
        int neighborCount)
    {
        var result = new Dictionary<Vertex, List<Vertex>>();

        foreach (var vertex in vertices)
        {
            result[vertex] = GetNeighborsForVertex(obstacles, new List<Vertex>(vertices), vertex, neighborCount);
        }

        return result;
    }

    public static List<Vertex> GetNeighborsForVertex(List<Obstacle> obstacles, List<Vertex> vertices, Vertex vertex,
        int neighborCount)
    {
        var neighborList = new List<Vertex>();

        var sortedObstacles = obstacles
            .OrderBy(o => Distance.Euclidean(vertex.Position.PositionArray, o.Center.PositionArray)).ToList();

        for (var i = 0; i < vertices.Count && neighborList.Count < neighborCount; i++)
        {
            var otherVertex = vertices[i];
            if (Equals(otherVertex, vertex))
            {
                continue;
            }

            if (TrajectoryCollidesWithObstacle(sortedObstacles, vertex.Position, otherVertex.Position))
            {
                continue;
            }

            neighborList.Add(otherVertex);
        }
        
        neighborList = neighborList
            .OrderBy(v => Distance.Euclidean(vertex.Position.PositionArray, v.Position.PositionArray))
            .Take(neighborCount)
            .ToList();

        return neighborList;
    }

    public static bool TrajectoryCollidesWithObstacle(List<Obstacle> obstacles, Position startPosition,
        Position endPosition)
    {
        var envelope = new Envelope(startPosition.ToCoordinate(), endPosition.ToCoordinate());

        var coordinateStart = startPosition.ToCoordinate();
        var coordinateEnd = endPosition.ToCoordinate();

        foreach (var obstacle in obstacles)
        {
            if (!obstacle.CanIntersect(envelope))
            {
                continue;
            }

            if (obstacle.IntersectsWithLine(coordinateStart, coordinateEnd))
            {
                return true;
            }
        }

        return false;
    }
}