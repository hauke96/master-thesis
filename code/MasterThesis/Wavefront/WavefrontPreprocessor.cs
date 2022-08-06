using Mars.Common.Collections;
using Mars.Numerics;
using NetTopologySuite.Geometries;
using ServiceStack;
using Wavefront.Geometry;

namespace Wavefront;

// TODO Tests
public class WavefrontPreprocessor
{
    public static Dictionary<Vertex, List<Vertex>> CalculateKnn(QuadTree<Obstacle> obstacles, List<Vertex> vertices,
        int neighborCount)
    {
        var result = new Dictionary<Vertex, List<Vertex>>();

        foreach (var vertex in vertices)
        {
            result[vertex] = GetNeighborsForVertex(obstacles, new List<Vertex>(vertices), vertex, neighborCount);
        }

        return result;
    }

    public static List<Vertex> GetNeighborsForVertex(QuadTree<Obstacle> obstacles, List<Vertex> vertices, Vertex vertex,
        int neighborCount)
    {
        var neighborList = new List<Vertex>();

        // [0] = Angle from
        // [1] = Angle to
        // [2] = Distance
        var shadowAreas = new List<double[]>();
        var obstaclesCastingShadow = new List<Obstacle>();

        vertices.Remove(vertex);
        var sortedVertices = vertices
            .OrderBy(v => Distance.Euclidean(vertex.Position.PositionArray, v.Position.PositionArray)).ToList();

        for (var i = 0; i < sortedVertices.Count && neighborList.Count < neighborCount; i++)
        {
            var otherVertex = sortedVertices[i];
            if (Equals(otherVertex, vertex))
            {
                continue;
            }

            var angle = Angle.GetBearing(vertex.Position, otherVertex.Position);
            var distance = Distance.Euclidean(vertex.Position.PositionArray, otherVertex.Position.PositionArray);
            var isInShadowArea = IsInShadowArea(shadowAreas, angle, distance);
            if (isInShadowArea)
            {
                continue;
            }

            var envelope = new Envelope(vertex.Coordinate, otherVertex.Coordinate);
            var possiblyCollidingObstacles = obstacles.Query(envelope);

            var intersectsWithObstacle = false;

            foreach (var obstacle in possiblyCollidingObstacles)
            {
                if (!obstaclesCastingShadow.Contains(obstacle))
                {
                    var (angleFrom, angleTo, maxDistance) = obstacle.GetAngleAreaOfObstacle(vertex);

                    shadowAreas.Add(new[] { angleFrom, angleTo, maxDistance });
                    obstaclesCastingShadow.Add(obstacle);
                }

                intersectsWithObstacle |= obstacle.CanIntersect(envelope) &&
                                          obstacle.IntersectsWithLine(vertex.Coordinate, otherVertex.Coordinate);
            }

            if (!intersectsWithObstacle)
            {
                neighborList.Add(otherVertex);
            }
        }

        return neighborList;
    }

    private static bool IsInShadowArea(List<double[]> shadowAreas, double angle, double distance)
    {
        foreach (var area in shadowAreas)
        {
            if (Angle.IsBetween(area[0], angle, area[1]) && distance > area[2])
            {
                return true;
            }
        }

        return false;
    }
}