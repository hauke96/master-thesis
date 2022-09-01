using System.Diagnostics;
using Mars.Common.Collections;
using Mars.Numerics;
using NetTopologySuite.Geometries;
using Wavefront.Geometry;
using Wavefront.Index;

namespace Wavefront;

// TODO Tests
public class WavefrontPreprocessor
{
    public class AngleArea
    {
        public double From { get; }
        public double To { get; }
        public double Distance { get; }

        public AngleArea(double from, double to, double distance)
        {
            From = from;
            To = to;
            Distance = distance;
        }

        public override int GetHashCode()
        {
            return (int)(Distance * 7919);
        }
    }

    public static Dictionary<Vertex, List<Vertex>> CalculateVisibleKnn(QuadTree<Obstacle> obstacles,
        List<Vertex> vertices,
        int neighborCount)
    {
        var result = new Dictionary<Vertex, List<Vertex>>();
        Log.I($"Calculate nearest {neighborCount} visible neighbors for each vertex");

        var i = 1;
        var verticesPerPercent = vertices.Count / 100d;
        var nextProcessOutput = verticesPerPercent;
        var stopWatch = new Stopwatch();
        foreach (var vertex in vertices)
        {
            if (i > nextProcessOutput)
            {
                Log.I($"  {(int)(i/verticesPerPercent)}% done ({stopWatch.ElapsedMilliseconds}ms)");
                stopWatch.Restart();
                nextProcessOutput += verticesPerPercent;
            }
            i++;
            
            result[vertex] = GetVisibleNeighborsForVertex(obstacles, new List<Vertex>(vertices), vertex, neighborCount);
        }

        return result;
    }

    public static List<Vertex> GetVisibleNeighborsForVertex(QuadTree<Obstacle> obstacles, List<Vertex> vertices,
        Vertex vertex,
        int neighborCount)
    {
        var neighborList = new List<Vertex>();

        var shadowAreas = new BinIndex<AngleArea>(360);

        // [0] = Angle from
        // [1] = Angle to
        // [2] = Distance
        var obstaclesCastingShadow = new HashSet<Obstacle>();

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
            var distanceToOtherVertex =
                Distance.Euclidean(vertex.Position.PositionArray, otherVertex.Position.PositionArray);
            var isInShadowArea = IsInShadowArea(shadowAreas, angle, distanceToOtherVertex);
            if (isInShadowArea)
            {
                continue;
            }

            var envelope = new Envelope(vertex.Coordinate, otherVertex.Coordinate);
            var intersectsWithObstacle = false;
            obstacles.Query(envelope, (Action<Obstacle>)(obstacle =>
            {
                if (intersectsWithObstacle)
                {
                    return;
                }
                
                if (!obstaclesCastingShadow.Contains(obstacle))
                {
                    var (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(vertex);
                    shadowAreas.Add(angleFrom, angleTo, new AngleArea(angleFrom, angleTo, distance));

                    obstaclesCastingShadow.Add(obstacle);
                }

                if (!intersectsWithObstacle)
                {
                    intersectsWithObstacle |= obstacle.CanIntersect(envelope) &&
                                              obstacle.IntersectsWithLine(vertex.Coordinate, otherVertex.Coordinate);
                }
            }));

            if (!intersectsWithObstacle)
            {
                neighborList.Add(otherVertex);
            }
        }

        return neighborList;
    }

    private static bool IsInShadowArea(BinIndex<AngleArea> shadowAreas, double angle, double distance)
    {
        foreach (var area in shadowAreas.Query(angle))
        {
            if (distance > area.Distance && Angle.IsBetweenEqual(area.From, angle, area.To))
            {
                return true;
            }
        }

        return false;
    }
}