using Mars.Numerics;
using NetTopologySuite.Geometries;
using ServiceStack;

namespace Wavefront.Geometry
{
    public class Obstacle
    {
        public readonly List<Coordinate> Coordinates;
        public List<Vertex> Vertices;
        public readonly Envelope Envelope;
        public readonly bool IsClosed;
        public readonly int Hash;
        public readonly NetTopologySuite.Geometries.Geometry Geometry;

        public static List<Obstacle> Create(NetTopologySuite.Geometries.Geometry geometry)
        {
            var obstacles = new List<Obstacle>();
            if (geometry is MultiPolygon multiPolygon)
            {
                multiPolygon.Each(polygon =>
                {
                    var simplePolygon = new Polygon((LinearRing)((Polygon)polygon.GetGeometryN(0)).ExteriorRing);
                    obstacles.Add(new Obstacle(simplePolygon));
                });
            }
            else
            {
                obstacles.Add(new Obstacle(geometry));
            }

            return obstacles;
        }

        public Obstacle(NetTopologySuite.Geometries.Geometry geometry) : this(geometry,
            geometry.Coordinates.Map(c => new Vertex(c.X, c.Y)))
        {
        }

        private Obstacle(NetTopologySuite.Geometries.Geometry geometry, List<Vertex> vertices)
        {
            if (geometry is not Polygon && geometry is not LineString && geometry is not Point)
            {
                throw new Exception("The obstacle geometry must be of type Polygon, LineString or Point!");
            }

            Coordinates = geometry.Coordinates.ToList();
            Vertices = vertices;
            Hash = (int)geometry.Coordinates.Sum(coordinate => coordinate.X * 7919 + coordinate.Y * 4813);
            Envelope = geometry.EnvelopeInternal;
            IsClosed = Coordinates.Count > 2 && Equals(Coordinates.First(), Coordinates.Last());

            // Ensure it's a polygon when closed.
            Geometry = IsClosed ? new Polygon(new LinearRing(geometry.Coordinates)) : geometry;
        }

        public bool CanIntersect(Envelope envelope)
        {
            return Coordinates.Count >= 2 && Envelope.Intersects(envelope);
        }

        /// <summary>
        /// Check is this obstacle intersects with the given line, specified by the two coordinate parameters.
        ///
        /// A few remarks and edge cases on what's considered an intersection:
        /// <ul>
        ///   <li>A line touching the obstacle (i.e. having at least one coordinate in common) is <i>not</i> considered an intersection.</li>
        ///   <li>A line fully within the obstacle (in case of a closed polygon) is considered as intersecting this obstacle.</li>
        /// </ul>
        /// </summary>
        public bool IntersectsWithLine(Coordinate coordinateStart, Coordinate coordinateEnd,
            Dictionary<Coordinate, List<Obstacle>> coordinateToObstacles)
        {
            var indexOfStartCoordinate = Coordinates.IndexOf(coordinateStart);
            var indexOfEndCoordinate = Coordinates.IndexOf(coordinateEnd);

            if (indexOfStartCoordinate != -1 && indexOfEndCoordinate != -1)
            {
                // Both coordinates are part of the obstacle -> Check if the line is out- or inside the polygon or in
                // case of a line-obstacle if the coordinates intersect any other line segment.
                return IntersectBetweenObstacleVertices(coordinateStart, coordinateEnd, indexOfStartCoordinate,
                    indexOfEndCoordinate, coordinateToObstacles);
            }

            // At most one coordinate is part of this obstacle.
            return IntersectsWithNonObstacleLine(coordinateStart, coordinateEnd);
        }

        /// <summary>
        /// Checks of the line segment, specified by the two given coordinates, intersects with this obstacle. Both
        /// coordinates are considered to be part of the obstacle, hence the two indices must be valid.
        /// </summary>
        private bool IntersectBetweenObstacleVertices(Coordinate coordinateStart, Coordinate coordinateEnd,
            int indexOfStartCoordinate, int indexOfEndCoordinate,
            Dictionary<Coordinate, List<Obstacle>> coordinateToObstacles)
        {
            // Check if the line segment defined by the two parameter coordinates is a segment of this obstacle.
            if (
                indexOfStartCoordinate == (indexOfEndCoordinate + 1) % (Coordinates.Count - 1) ||
                indexOfEndCoordinate == (indexOfStartCoordinate + 1) % (Coordinates.Count - 1)
            )
            {
                // The two parameter coordinates build a line segment, which is part of this obstacle. Check if this
                // segment is shared between two obstacles, which are touching each other on this segment. Only if this
                // obstacle is closed, this check needs to be performed. Open obstacles can touch too, but all shared
                // segments can be correctly be used in a route (e.g. when the two parameter coordinates are start and
                // target of a routing query).

                if (!IsClosed)
                {
                    return false;
                }

                var commonObstacles = coordinateToObstacles[coordinateStart]
                    .Intersect(coordinateToObstacles[coordinateEnd]).ToList();
                return commonObstacles.Count > 1 &&
                       commonObstacles.All(o => o.HasLineSegment(coordinateStart, coordinateEnd));
            }

            // The two parameter coordinates are actually not building a line segment of this obstacle. This means the
            // line could be inside or outside the obstacle.

            if (IsClosed)
            {
                return Geometry.Intersects(new LineString(new[] { coordinateStart, coordinateEnd }));
            }

            // This obstacle is open, so we check if any line segment intersects with the segment defined by the
            // coordinate parameters.
            var intersectsWithSegment = IntersectsWithLineString(coordinateStart, coordinateEnd);

            return intersectsWithSegment;
        }

        /// <summary>
        /// Check for intersection with the given line, specified by the two given coordinates. It's assumes that one
        /// or none of the coordinates is part of this obstacle. When this obstacle is a polygonal obstacle, a line
        /// being fully within the obstacle counts as an intersection.
        /// </summary>
        private bool IntersectsWithNonObstacleLine(Coordinate coordinateStart, Coordinate coordinateEnd)
        {
            // The IntersectsWithLineString method is used due to its speed. However, a line string could also be fully
            // within a polygon, which also counts as an intersection. Therefore, the Geometry.Contains methods are
            // additionally used.
            return
            (
                IntersectsWithLineString(coordinateStart, coordinateEnd) ||
                (
                    IsClosed &&
                    (
                        ((Polygon)Geometry).Contains(new Point(coordinateStart)) ||
                        ((Polygon)Geometry).Contains(new Point(coordinateEnd))
                    )
                )
            );
        }

        /// <summary>
        /// Checks if the given line string defined by the coordinate parameters, intersects with any line segment of
        /// the obstacle.
        /// </summary>
        private bool IntersectsWithLineString(Coordinate coordinateStart, Coordinate coordinateEnd)
        {
            var intersectsWithSegment = false;
            for (var i = 1; !intersectsWithSegment && i < Coordinates.Count; i++)
            {
                var coordinate = Coordinates[i - 1];

                intersectsWithSegment |=
                    Intersect.DoIntersect(coordinateStart, coordinateEnd, coordinate, Coordinates[i]);
            }

            return intersectsWithSegment;
        }

        public bool HasLineSegment(Coordinate coordinateStart, Coordinate coordinateEnd)
        {
            // Initialize start indices far away from each other because the distance matters below.
            var indexStart = -10;
            var indexEnd = -20;
            for (int i = 0; i < Coordinates.Count && (indexStart < 0 || indexEnd < 0); i++)
            {
                if (Coordinates[i].Equals(coordinateStart))
                {
                    indexStart = i;
                }
                else if (Coordinates[i].Equals(coordinateEnd))
                {
                    indexEnd = i;
                }
            }

            // Start and end coordinates are right next to each other, no other vertex is in between them. This has
            // also to be checked if the first and last coordinates are found.
            return Math.Abs(indexStart - indexEnd) == 1 ||
                   indexStart == 0 && indexEnd == Coordinates.Count - 2 ||
                   indexEnd == 0 && indexStart == Coordinates.Count - 2;
        }

        /// <summary>
        /// Calculates the shadow area seen from a given vertex.
        /// </summary>
        public (double, double, double) GetAngleAreaOfObstacle(Vertex vertex)
        {
            var angleFrom = double.NaN;
            var angleTo = double.NaN;
            var previousAngle = double.NaN;

            // Remember the coordinate defining the angleFrom and angleTo area to determine the distance of the shadow
            // area once it has been established. 
            Coordinate coordinateFrom = null;
            Coordinate coordinateTo = null;

            // We have to manually go through all coordinates to build the shadow area. This is because the shadow might
            // exceed the 0° border and therefore simple min and max values cannot be used.
            foreach (var coordinate in Coordinates)
            {
                if (Equals(coordinate, vertex.Coordinate))
                {
                    continue;
                }

                var angleToNewCoordinate =
                    Angle.GetBearing(vertex.Position.X, vertex.Position.Y, coordinate.X, coordinate.Y);

                if (Double.IsNaN(previousAngle))
                {
                    // At the first coordinate, there's no previous angle, so we set it here.
                    previousAngle = angleToNewCoordinate;
                }

                if (!Angle.IsBetweenEqual(angleFrom, angleToNewCoordinate, angleTo))
                {
                    // Make sure the two angles are in the right order
                    Angle.GetEnclosingAngles(angleToNewCoordinate, previousAngle, out var newSegmentAngleFrom,
                        out var newSegmentAngleTo);

                    if (double.IsNaN(angleFrom) && double.IsNaN(angleTo))
                    {
                        // At the first coordinate, there's no angle area yet, so we set it here.
                        angleFrom = newSegmentAngleFrom;
                        angleTo = newSegmentAngleTo;
                        coordinateFrom = coordinate;
                        coordinateTo = coordinate;
                    }
                    else
                    {
                        var (mergedAngleFrom, mergedAngleTo) =
                            Angle.Merge(angleFrom, angleTo, newSegmentAngleFrom, newSegmentAngleTo);

                        // If the "from" or "to" angle changed to the angle of the new coordinate, then store the coordinate. 
                        if (mergedAngleFrom == newSegmentAngleFrom && mergedAngleFrom != angleFrom)
                        {
                            coordinateFrom = coordinate;
                        }

                        if (mergedAngleTo == newSegmentAngleTo && mergedAngleTo != angleTo)
                        {
                            coordinateTo = coordinate;
                        }

                        angleFrom = mergedAngleFrom;
                        angleTo = mergedAngleTo;

                        // We definitely don't have complete overlaps, that (angleFrom, angleTo) is completely in
                        // (a1, a2) and vice versa. Always exactly one side (from or to) is touching the current region.
                        // if (angleFrom == newSegmentAngleTo)
                        // {
                        //     // angle area goes  a1 -> a2 == angleFrom -> angleTo
                        //     angleFrom = newSegmentAngleFrom;
                        //     coordinateFrom = coordinate;
                        // }
                        // else if (angleTo == newSegmentAngleFrom)
                        // {
                        //     // angle area goes  angleFrom -> a1 == angleTo -> a2
                        //     angleTo = newSegmentAngleTo;
                        //     coordinateTo = coordinate;
                        // }
                    }
                }

                previousAngle = angleToNewCoordinate;
            }

            var distanceToFromCoordinate = Distance.Euclidean(vertex.Position.PositionArray,
                new[] { coordinateFrom[0], coordinateFrom[1] });
            var distanceToToCoordinate = Distance.Euclidean(vertex.Position.PositionArray,
                new[] { coordinateTo[0], coordinateTo[1] });
            var maxDistance = Math.Max(distanceToFromCoordinate, distanceToToCoordinate);

            return (angleFrom, angleTo, maxDistance);
        }

        public override int GetHashCode()
        {
            return Hash;
        }
    }
}