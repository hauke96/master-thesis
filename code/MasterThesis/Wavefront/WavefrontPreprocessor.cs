using System.Diagnostics;
using Mars.Common.Core.Collections;
using Mars.Numerics;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index;
using NetTopologySuite.Index.Strtree;
using ServiceStack;
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

    public static double BaseLength = 0.1;

    public static Dictionary<Vertex, List<Vertex>> CalculateVisibleKnn(STRtree<Obstacle> obstacles,
        List<Vertex> vertices, int neighborCount)
    {
        var result = new Dictionary<Vertex, List<Vertex>>();
        Log.I($"Calculate nearest {neighborCount} visible neighbors for each vertex");

        var i = 1;
        var verticesPerPercent = vertices.Count / 100d;
        var nextProcessOutput = verticesPerPercent;
        var watch = Stopwatch.StartNew();

        var vertexTree = new STRtree<Vertex>();
        vertices.Each(v => vertexTree.Insert(new Envelope(v.Coordinate), v));

        foreach (var vertex in vertices)
        {
            if (i > nextProcessOutput)
            {
                Log.I($"  {(int)(i / verticesPerPercent)}% done in {watch.ElapsedMilliseconds}ms");
                watch.Restart();
                nextProcessOutput += verticesPerPercent;
            }

            i++;

            result[vertex] = GetVisibleNeighborsForVertex(obstacles, vertexTree, vertex, neighborCount);
        }

        return result;
    }

    public static List<Vertex> GetVisibleNeighborsForVertex(STRtree<Obstacle> obstacles, STRtree<Vertex> vertices,
        Vertex vertex, int wantedVertexCount)
    {
        var neighborList = new HashSet<Vertex>();
        var visitedVertices = new HashSet<Vertex>();

        var shadowAreas = new BinIndex<AngleArea>(360);

        var obstaclesCastingShadow = new HashSet<Obstacle>();

        var i = 0;
        for (; neighborList.Count < wantedVertexCount && visitedVertices.Count < vertices.Count; i++)
        {
            var newVertices = new LinkedList<Vertex>();
            GetEnvelopesForIndex(vertex.Coordinate, i).Each(
                e => vertices.Query(e)
                    .Where(v => !visitedVertices.Contains(v))
                    .Each(v => newVertices.AddLast(v)));

            if (newVertices.IsEmpty())
            {
                continue;
            }

            visitedVertices.AddRange(newVertices);

            var sortedVertices = newVertices.OrderBy(v =>
            {
                // We don't need to take the square root of this as long as the order is correct.
                var xDiff = v.X - vertex.X;
                var yDiff = v.Y - vertex.Y;
                var diff = xDiff * xDiff + yDiff * yDiff;

                // var diff = Distance.Euclidean(vertex.Position.PositionArray, v2.Position.PositionArray) -
                // Distance.Euclidean(vertex.Position.PositionArray, v1.Position.PositionArray);
                return diff == 0 ? 0 : diff > 0 ? 1 : -1;
            }).ToList();

            FindKnnVertices(obstacles, sortedVertices, vertex, shadowAreas, obstaclesCastingShadow, neighborList,
                wantedVertexCount);
        }

        return neighborList.ToList();
    }

    public static void FindKnnVertices(STRtree<Obstacle> obstacles, ICollection<Vertex> vertices, Vertex vertex,
        BinIndex<AngleArea> shadowAreas, HashSet<Obstacle> obstaclesCastingShadow, ICollection<Vertex> neighborList,
        int wantedVertexCount)
    {
        foreach (var otherVertex in vertices)
        {
            if (Equals(otherVertex, vertex))
            {
                continue;
            }

            var otherVertexVisible =
                IsVertexVisible(obstacles, vertex, shadowAreas, obstaclesCastingShadow, otherVertex);
            if (otherVertexVisible)
            {
                neighborList.Add(otherVertex);
            }

            if (neighborList.Count == wantedVertexCount)
            {
                return;
            }
        }
    }

    public static bool IsVertexVisible(STRtree<Obstacle> obstacles, Vertex vertex, BinIndex<AngleArea> shadowAreas,
        HashSet<Obstacle> obstaclesCastingShadow, Vertex otherVertex)
    {
        var angle = Angle.GetBearing(vertex.Position, otherVertex.Position);
        var distanceToOtherVertex =
            Distance.Euclidean(vertex.Position.PositionArray, otherVertex.Position.PositionArray);

        if (IsInShadowArea(shadowAreas, angle, distanceToOtherVertex))
        {
            return false;
        }

        var envelope = new Envelope(vertex.Coordinate, otherVertex.Coordinate);
        var visitor = new StrTreeObstacleVisitor(obstaclesCastingShadow, shadowAreas, envelope, vertex, otherVertex);

        obstacles.Query(envelope, visitor);

        return !visitor.IntersectsWithObstacle;
    }

    public static LinkedList<Envelope> GetEnvelopesForIndex(Coordinate c, int i)
    {
        var envelopes = new LinkedList<Envelope>();

        var envelopeTop = new Envelope(c.X - (i + 1) * BaseLength, c.X + (i + 1) * BaseLength,
            c.Y + (i + 1) * BaseLength, c.Y + i * BaseLength);
        envelopes.AddLast(envelopeTop);

        var envelopeBottom = new Envelope(c.X - (i + 1) * BaseLength, c.X + (i + 1) * BaseLength, c.Y - i * BaseLength,
            c.Y - (i + 1) * BaseLength);
        envelopes.AddLast(envelopeBottom);

        if (i != 0)
        {
            // For i==0 these envelopes will have a height of 0, so we can ignore them.
            var envelopeRight = new Envelope(c.X + i * BaseLength, c.X + (i + 1) * BaseLength, c.Y + i * BaseLength,
                c.Y - i * BaseLength);
            envelopes.AddLast(envelopeRight);

            var envelopeLeft = new Envelope(c.X - i * BaseLength, c.X - (i + 1) * BaseLength, c.Y + i * BaseLength,
                c.Y - i * BaseLength);
            envelopes.AddLast(envelopeLeft);
        }

        return envelopes;
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

    class StrTreeObstacleVisitor : IItemVisitor<Obstacle>
    {
        private readonly HashSet<Obstacle> _obstaclesCastingShadow;
        private readonly BinIndex<AngleArea> _shadowAreas;
        private readonly Envelope _envelope;
        private readonly Vertex _vertex;
        private readonly Vertex _otherVertex;

        public bool IntersectsWithObstacle { get; private set; }

        public StrTreeObstacleVisitor(HashSet<Obstacle> obstaclesCastingShadow, BinIndex<AngleArea> shadowAreas,
            Envelope envelope, Vertex vertex, Vertex otherVertex)
        {
            _obstaclesCastingShadow = obstaclesCastingShadow;
            _shadowAreas = shadowAreas;
            _envelope = envelope;
            _vertex = vertex;
            _otherVertex = otherVertex;
        }

        public void VisitItem(Obstacle obstacle)
        {
            if (IntersectsWithObstacle)
            {
                return;
            }

            if (!IntersectsWithObstacle)
            {
                double angleFrom;
                double angleTo;
                double distance;
                if (Math.Abs(_vertex.Position.X - 9.9283133) < 0.0001)
                {
                    // TODO returns (NaN, NaN, ?)
                    (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(_vertex);
                }
                else
                {
                    (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(_vertex);
                }

                var obstacleCompletelyInShadow = _shadowAreas.QueryWithin(angleFrom, angleTo)
                    .Any(area => area.Distance <= distance);

                IntersectsWithObstacle |= obstacleCompletelyInShadow ||
                                          obstacle.CanIntersect(_envelope) &&
                                          obstacle.IntersectsWithLine(_vertex.Coordinate, _otherVertex.Coordinate);
            }

            if (!_obstaclesCastingShadow.Contains(obstacle))
            {
                var (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(_vertex);
                _shadowAreas.Add(angleFrom, angleTo, new AngleArea(angleFrom, angleTo, distance));

                _obstaclesCastingShadow.Add(obstacle);
            }
        }
    }
}