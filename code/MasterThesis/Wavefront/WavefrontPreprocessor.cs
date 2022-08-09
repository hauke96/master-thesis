using Mars.Common.Collections;
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
    private class LinkedListItemVisitor : IItemVisitor<Obstacle>
    {
        public readonly LinkedList<Obstacle> _items = new();

        public void VisitItem(Obstacle item)
        {
            _items.AddLast(item);
        }
    }

    public class AngleArea
    {
        public double From { get; set; }
        public double To { get; set; }
        public double Distance { get; set; }

        /*
         * From and to values but ensures that
         *   OrderedFrom < OrderedTo AND
         *   OrderedFrom <= From AND
         *   OrderedTo >= To
         */
        public double OrderedFrom => From <= To ? From : From - 360;
        public double OrderedTo => To >= From ? To : To + 360;

        public AngleArea(double from, double to, double distance)
        {
            From = from;
            To = to;
            Distance = distance;
        }
    }

    public static Dictionary<Vertex, List<Vertex>> CalculateKnn(STRtree<Obstacle> obstacles, List<Vertex> vertices,
        int neighborCount)
    {
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

    public static List<Vertex> GetNeighborsForVertex(STRtree<Obstacle> obstacles, List<Vertex> vertices, Vertex vertex,
        int neighborCount)
    {
        var neighborList = new List<Vertex>();

        var shadowAreas = new CITree<AngleArea>();

        // [0] = Angle from
        // [1] = Angle to
        // [2] = Distance
        // var shadowAreas = new List<double[]>();
        var obstaclesCastingShadow = new HashSet<double>();

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
                // Log.I("Is in shadow");
                continue;
            }

            RelaxShadowAreas(shadowAreas, distanceToOtherVertex);

            var visitor = new LinkedListItemVisitor();
            var envelopeCount = 1d;
            var xDiff = (otherVertex.Coordinate.X - vertex.Coordinate.X) / envelopeCount;
            var yDiff = (otherVertex.Coordinate.Y - vertex.Coordinate.Y) / envelopeCount;
            for (var j = 1; j <= envelopeCount; j++)
            {
                var envelope = new Envelope(vertex.Coordinate,
                    new Coordinate(vertex.Coordinate.X + xDiff * j, vertex.Coordinate.Y + yDiff * j));
                obstacles.Query(envelope, visitor);
            }

            // obstacles.Query(new Envelope(vertex.Coordinate,otherVertex.Coordinate), visitor);
            var possiblyCollidingObstacles = visitor._items.Distinct().ToList();

            // Log.I($"Found {possiblyCollidingObstacles.Count} possible obstacles for distance {distanceToOtherVertex}");

            var intersectsWithObstacle = false;

            foreach (var obstacle in possiblyCollidingObstacles)
            {
                if (!obstaclesCastingShadow.Contains(obstacle.Hash))
                {
                    var (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(vertex);

                    var existingAreas = shadowAreas.Query(angleFrom, angleTo);
                    (angleFrom, angleTo, distance) = Merge(existingAreas, angleFrom, angleTo, distance);

                    existingAreas.Each(area => shadowAreas.Remove(area));
                    shadowAreas.Insert(angleFrom, angleTo, new AngleArea(angleFrom, angleTo, distance));

                    obstaclesCastingShadow.Add(obstacle.Hash);
                }

                intersectsWithObstacle |= obstacle.IntersectsWithLine(vertex.Coordinate, otherVertex.Coordinate);
            }

            if (!intersectsWithObstacle)
            {
                neighborList.Add(otherVertex);
            }
        }

        // Log.I($"Shadows: {shadowAreas.Count}");

        return neighborList;
    }

    /// <summary>
    /// Merges all areas below a maximum distance if possible.
    /// </summary>
    public static void RelaxShadowAreas(CITree<AngleArea> shadowAreas, double maxDistance)
    {
        var areasToRelax = new LinkedList<CITreeNode<AngleArea>>();
        var areas = shadowAreas.QueryAll();

        var node = areas.First;
        while (node != null)
        {
            if (node.Value.Value.Distance < maxDistance)
            {
                areasToRelax.AddLast(node.Value);
                shadowAreas.Remove(node.Value);
            }

            node = node.Next;
        }

        while (areasToRelax.Count > 0)
        {
            var areaNode = areasToRelax.First;
            var area = areaNode.Value;
            var foundAreaToRelax = false;

            var otherAreaNode = areaNode.Next;
            while (otherAreaNode != null)
            {
                var otherArea = otherAreaNode.Value;
                var areaToNormalized = Angle.StrictNormalize(area.To);
                var otherAreaToNormalized = Angle.StrictNormalize(otherArea.To);

                var otherFromWithinArea = Angle.IsBetweenEqual(area.From, otherArea.From, areaToNormalized);
                var otherToWithinArea = Angle.IsBetweenEqual(area.From, otherAreaToNormalized, areaToNormalized);
                var areOverlapping = otherFromWithinArea ||
                                     otherToWithinArea ||
                                     Angle.IsBetweenEqual(otherArea.From, area.From, otherAreaToNormalized) ||
                                     Angle.IsBetweenEqual(otherArea.From, areaToNormalized, otherAreaToNormalized);
                foundAreaToRelax |= areOverlapping;

                var next = otherAreaNode.Next;
                if (areOverlapping)
                {
                    area.From = otherFromWithinArea ? area.From : otherArea.From;
                    area.To = otherToWithinArea ? area.To : otherArea.To;

                    areasToRelax.Remove(otherAreaNode);
                }

                otherAreaNode = next;
            }

            if (!foundAreaToRelax)
            {
                areasToRelax.Remove(area);

                area.Value.From = area.From;
                area.Value.To = area.To;
                area.Value.Distance = maxDistance;
                shadowAreas.Insert(area.From, area.To, area.Value);
            }
        }
    }

    private static bool IsInShadowArea(CITree<AngleArea> shadowAreas, double angle, double distance)
    {
        var angleAreas = shadowAreas.Query(angle);
        foreach (var area in angleAreas)
        {
            if (distance > area.Value.Distance)
            {
                return true;
            }
        }

        return false;
    }

    public static (double, double, double) Merge(ICollection<CITreeNode<AngleArea>> angleAreas, double from, double to,
        double distance)
    {
        if (angleAreas.IsEmpty())
        {
            return (from, to, distance);
        }

        from = Angle.StrictNormalize(from);
        to = Angle.StrictNormalize(to);

        var areaOverlappingFrom =
            angleAreas.FirstOrDefault(area => Angle.GreaterEqual(area.From, from) && area.Value.Distance >= distance);
        var areaOverlappingTo =
            angleAreas.FirstOrDefault(area => Angle.LowerEqual(to, area.To) && area.Value.Distance >= distance);
        var areaDistance = angleAreas.Min(area => area.Value.Distance);

        var resultFrom = Angle.Normalize(Math.Min(areaOverlappingFrom?.Value.OrderedFrom ?? from, from));
        var resultTo = Angle.Normalize(Math.Max(areaOverlappingTo?.Value.OrderedTo ?? to, to));
        var resultDistance = Math.Min(distance, areaDistance);

        return (resultFrom, resultTo, resultDistance);
    }
}