using Mars.Common;
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
        Log.Init();
        var result = new Dictionary<Vertex, List<Vertex>>();

        var i = 1;
        foreach (var vertex in vertices)
        {
            Log.I($"Vertex {i}/{vertices.Count} : {vertex}");
            i++;
            result[vertex] = GetNeighborsForVertex(obstacles, new List<Vertex>(vertices), vertex, neighborCount);
            // Log.I($"============================");
            // Thread.Sleep(2000);
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
        var shadowAreas = new LinkedList<double[]>();
        var obstaclesCastingShadow = new List<Obstacle>();

        vertices.Remove(vertex);
        var sortedVertices = vertices
            .OrderBy(v => Distance.Euclidean(vertex.Position.PositionArray, v.Position.PositionArray)).ToList();

        for (var i = 0; i < sortedVertices.Count && neighborList.Count < neighborCount; i++)
        {
            var otherVertex = sortedVertices[i];
            // Log.I($"otherVertex {otherVertex}");
            if (Equals(otherVertex, vertex))
            {
                continue;
            }

            var angle = Angle.GetBearing(vertex.Position, otherVertex.Position);
            var distance = Distance.Euclidean(vertex.Position.PositionArray, otherVertex.Position.PositionArray);
            var isInShadowArea = IsInShadowArea(shadowAreas, angle, distance);
            if (isInShadowArea)
            {
                // Log.I($"in shadow area");
                continue;
            }

            var envelope = new Envelope(vertex.Coordinate, otherVertex.Coordinate);
            var possiblyCollidingObstacles = new LinkedList<Obstacle>();
            obstacles.Query(envelope, (Action<Obstacle>)(obj => possiblyCollidingObstacles.AddLast(obj)));
            // .OrderBy(o =>
            // {
            //     var minDistance = o.Coordinates.Map(c =>
            //         Distance.Euclidean(vertex.Position.PositionArray, c.ToPosition().PositionArray)).Min();
            //     return minDistance;
            // })
            // .ToList();
            // Log.I($"possiblyCollidingObstacles {possiblyCollidingObstacles.Count} many");

            var intersectsWithObstacle = false;

            foreach (var obstacle in possiblyCollidingObstacles)
            {
                // Log.I($"obstacle {obstacle.Envelope.Centre}");
                if (!obstaclesCastingShadow.Contains(obstacle))
                {
                    var (newAreaAngleFrom, newAreaAngleTo, newAreaDistance) = obstacle.GetAngleAreaOfObstacle(vertex);

                    if (newAreaAngleFrom == newAreaAngleTo)
                    {
                        continue;
                    }

                    LinkedListNode<double[]>? insertAfter = null;

                    double mergedAreaFrom = newAreaAngleFrom;
                    double mergedAreaTo = newAreaAngleTo;
                    double mergedAreaDistance = newAreaDistance;
                    var newAngleAreaNeeded = true;
                    var mergeNeeded = false;

                    var shadowAreaNode = shadowAreas.First;
                    while (shadowAreaNode != null)
                    {
                        var nextNode = shadowAreaNode.Next;
                        var shadowArea = shadowAreaNode.Value;

                        var newFromInShadowArea = Angle.IsBetweenEqual(shadowArea[0], mergedAreaFrom, shadowArea[1]);
                        var newToInShadowArea = Angle.IsBetweenEqual(shadowArea[0], mergedAreaTo, shadowArea[1]);
                        var currentAreaIntersectsNew =
                            Angle.IsBetweenEqual(mergedAreaFrom, shadowArea[0], mergedAreaTo) ||
                            Angle.IsBetweenEqual(mergedAreaFrom, shadowArea[1], mergedAreaTo);

                        if (newFromInShadowArea && newToInShadowArea)
                        {
                            // New shadow area completely within existing one -> nothing to do
                            newAngleAreaNeeded = false;
                            break;
                        }

                        if (currentAreaIntersectsNew)
                        {
                            mergedAreaFrom = Math.Min(mergedAreaFrom, shadowArea[0]);
                            mergedAreaTo = Math.Max(mergedAreaTo, shadowArea[1]);
                            mergedAreaDistance = Math.Max(newAreaDistance, shadowArea[2]);
                            shadowAreas.Remove(shadowArea);
                            mergeNeeded = true;
                        }

                        if (!mergeNeeded)
                        {
                            // Push further forward until last node before the new area
                            insertAfter = shadowAreaNode;
                        }

                        shadowAreaNode = nextNode;
                    }

                    if (!newAngleAreaNeeded)
                    {
                        continue;
                    }

                    var newShadowArea = new[]
                    {
                        mergedAreaFrom,
                        mergedAreaTo,
                        mergedAreaDistance
                    };

                    if (insertAfter == null)
                    {
                        shadowAreas.AddFirst(newShadowArea);
                    }
                    else
                    {
                        shadowAreas.AddAfter(insertAfter, newShadowArea);
                    }

                    // Log.I($"shadowAreas {shadowAreas.Map(v => "(" + v[0] + "-" + v[1] + ")").Join(", ")}");
                    // Log.I($"shadowAreas {shadowAreas.Count}");

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

    private static bool IsInShadowArea(ICollection<double[]> shadowAreas, double angle, double distance)
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