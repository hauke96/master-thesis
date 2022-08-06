using Mars.Common;
using Mars.Common.Collections;
using Mars.Numerics;
using NetTopologySuite.Geometries;
using ServiceStack;
using Wavefront.Geometry;
using Position = Mars.Interfaces.Environments.Position;

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
        // Console.WriteLine($"Process {vertex}");

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
                    // Calculate the shadow area of this obstacle. The problem: It can exceed the 0° border an we have to manually 
                    var angleFrom = double.NaN;
                    var angleTo = double.NaN;
                    var previousAngle = double.NaN;
                    for (var j = 0; j < obstacle.Coordinates.Count - 1; j++)
                    {
                        var c1 = obstacle.Coordinates[j];

                        var a1 = Angle.GetBearing(vertex.Position.X, vertex.Position.Y, c1.X, c1.Y);
                        var a2 = double.IsNaN(previousAngle) ? a1 : previousAngle;
                        previousAngle = a1;
                        
                        // Make sure a1=from and a2=to
                        Angle.GetEnclosingAngles(a1, a2, out a1, out a2);

                        if (double.IsNaN(angleFrom) && double.IsNaN(angleTo))
                        {
                            angleFrom = a1;
                            angleTo = a2;
                        }
                        else
                        {
                            // We definitely don't have complete overlaps, that (angleFrom, angleTo) is completely in
                            // (a1, a2) and vice versa. Always exactly one side (from or to) is touching the current region.
                            if (angleFrom == a2)
                            {
                                // angle area goes  a1 -> a2 == angleFrom -> angleTo
                                angleFrom = a1;
                            }
                            else if (angleTo == a1)
                            {
                                // angle area goes  angleFrom -> a1 == angleTo -> a2
                                angleTo = a2;
                            }
                        }
                    }

                    var maxDistance = obstacle.Coordinates.Map(coordinate =>
                        Distance.Euclidean(vertex.Position.PositionArray, new[] { coordinate.X, coordinate.Y })).Max();

                    obstaclesCastingShadow.Add(obstacle);
                    shadowAreas.Add(new[] { angleFrom, angleTo, maxDistance });
                }

                intersectsWithObstacle |= obstacle.CanIntersect(envelope) &&
                                          obstacle.IntersectsWithLine(vertex.Coordinate, otherVertex.Coordinate);
            }


            // if (TrajectoryCollidesWithObstacle(possiblyCollidingObstacles, vertex.Coordinate, otherVertex.Coordinate))
            // {
            // continue;
            // }

            if (!intersectsWithObstacle)
            {
                // Console.WriteLine($"  Add {otherVertex} with distance {distance}");
                neighborList.Add(otherVertex);
            }
        }
        //
        // neighborList = neighborList
        //     .OrderBy(v => Distance.Euclidean(vertex.Position.PositionArray, v.Position.PositionArray))
        //     .Take(neighborCount)
        //     .ToList();

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

    public static bool TrajectoryCollidesWithObstacle(IList<Obstacle> obstacles, Coordinate start, Coordinate end)
    {
        var envelope = new Envelope(start, end);

        foreach (var obstacle in obstacles)
        {
            if (!obstacle.CanIntersect(envelope))
            {
                continue;
            }

            if (obstacle.IntersectsWithLine(start, end))
            {
                return true;
            }
        }

        return false;
    }
}