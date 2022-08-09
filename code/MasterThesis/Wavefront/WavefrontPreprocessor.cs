using Mars.Common.Collections;
using Mars.Numerics;
using NetTopologySuite.Geometries;
using Wavefront.Geometry;

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
    }

    public static Dictionary<Vertex, List<Vertex>> CalculateVisibleKnn(QuadTree<Obstacle> obstacles, List<Vertex> vertices,
        int neighborCount)
    {
        var result = new Dictionary<Vertex, List<Vertex>>();

        var i = 1;
        foreach (var vertex in vertices)
        {
            Log.I($"Vertex {i}/{vertices.Count} : {vertex}");
            i++;
            result[vertex] = GetVisibleNeighborsForVertex(obstacles, new List<Vertex>(vertices), vertex, neighborCount);
            // Log.I($"============================");
            // Thread.Sleep(2000);
        }

        return result;
    }

    public static List<Vertex> GetVisibleNeighborsForVertex(QuadTree<Obstacle> obstacles, List<Vertex> vertices, Vertex vertex,
        int neighborCount)
    {
        var neighborList = new List<Vertex>();

        var shadowAreas = new LinkedList<AngleArea>();

        // [0] = Angle from
        // [1] = Angle to
        // [2] = Distance
        // var shadowAreas = new List<double[]>();
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
            var possiblyCollidingObstacles = new List<Obstacle>(100);
            obstacles.Query(envelope, (Action<Obstacle>)(obj => possiblyCollidingObstacles.Add(obj)));

            var intersectsWithObstacle = false;

            foreach(Obstacle obstacle in possiblyCollidingObstacles)
            {
                if (!obstaclesCastingShadow.Contains(obstacle))
                {
                    var (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(vertex);
                    shadowAreas.AddLast(new AngleArea(angleFrom, angleTo, distance));

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

    private static bool IsInShadowArea(ICollection<AngleArea> shadowAreas, double angle, double distance)
    {
        foreach (var area in shadowAreas)
        {
            if (distance > area.Distance && Angle.IsBetweenEqual(area.From, angle, area.To))
            {
                return true;
            }
        }

        return false;
    }
}