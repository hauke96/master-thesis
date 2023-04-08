using Mars.Numerics;
using NetTopologySuite.Geometries;
using ServiceStack;

namespace HybridVisibilityGraphRouting.Geometry
{
    public class Obstacle
    {
        public readonly List<Coordinate> Coordinates;
        public readonly Envelope Envelope;
        public readonly bool IsClosed;

        public List<Vertex> Vertices;

        private readonly int _hash;

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
            IsClosed = GeometryHelper.IsGeometryClosed(geometry);
            if (IsClosed && Coordinates.Count > 4 && (geometry is Polygon || geometry is MultiPolygon))
            {
                throw new Exception(
                    $"Obstacle of type {geometry.GeometryType} is closed but not a triangle (has {geometry.Coordinates.Length - 1} vertices)!");
            }

            Vertices = vertices;
            _hash = (int)geometry.Coordinates.Sum(coordinate => coordinate.X * 7919 + coordinate.Y * 4813);
            Envelope = geometry.EnvelopeInternal;
        }

        public bool CanIntersect(Envelope envelope)
        {
            return Coordinates.Count >= 2 && Envelope.Intersects(envelope);
        }

        /// <summary>
        /// Check if this obstacle intersects with the given line, specified by the two coordinate parameters.
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
            var indexOfStartCoordinate = -1;
            var indexOfEndCoordinate = -1;
            for (var i = 0; i < Coordinates.Count && (indexOfStartCoordinate == -1 || indexOfEndCoordinate == -1); i++)
            {
                if (indexOfStartCoordinate == -1 && Coordinates[i].Equals(coordinateStart))
                {
                    indexOfStartCoordinate = i;
                }
                else if (indexOfEndCoordinate == -1 && Coordinates[i].Equals(coordinateEnd))
                {
                    indexOfEndCoordinate = i;
                }
            }

            if (indexOfStartCoordinate != -1 && indexOfEndCoordinate != -1)
            {
                return IntersectBetweenVerticesOnOpenObstacle(coordinateStart, coordinateEnd, indexOfStartCoordinate,
                    indexOfEndCoordinate, coordinateToObstacles);
            }

            // At most one coordinate is part of this obstacle.
            return IntersectsWithNonObstacleLine(coordinateStart, coordinateEnd);
        }

        /// <summary>
        /// Checks of the line segment, specified by the two given coordinates, intersects with this open obstacle. Both
        /// coordinates are considered to be part of the obstacle, hence the two given indices must point to existing
        /// coordinates.
        /// </summary>
        private bool IntersectBetweenVerticesOnOpenObstacle(Coordinate coordinateStart, Coordinate coordinateEnd,
            int indexOfStartCoordinate, int indexOfEndCoordinate,
            Dictionary<Coordinate, List<Obstacle>> coordinateToObstacles)
        {
            // Check if the line segment defined by the two parameter coordinates is a segment of this obstacle.
            if (
                indexOfStartCoordinate == (indexOfEndCoordinate + 1) % (Coordinates.Count - 1) ||
                indexOfEndCoordinate == (indexOfStartCoordinate + 1) % (Coordinates.Count - 1)
            )
            {
                var commonObstacles = coordinateToObstacles[coordinateStart]
                    .Intersect(coordinateToObstacles[coordinateEnd]).ToList();
                return commonObstacles.Count > 1 &&
                       commonObstacles.All(o => o.HasLineSegment(coordinateStart, coordinateEnd));
            }

            // The two given coordinates to not build a line segment of this open obstacle, so we check if any line
            // segment intersects with the line string defined by the coordinate parameters.
            return IntersectsWithLineString(coordinateStart, coordinateEnd);
        }

        /// <summary>
        /// Check for intersection with the given line, specified by the two given coordinates. It's assumes that one
        /// or none of the coordinates is part of this obstacle. When this obstacle is a polygonal obstacle, a line
        /// being fully within the obstacle counts as an intersection.
        /// </summary>
        private bool IntersectsWithNonObstacleLine(Coordinate coordinateStart, Coordinate coordinateEnd)
        {
            // The IntersectsWithLineString method is used due to its speed. However, a line string could also be fully
            // within a polygon, which also counts as an intersection. Therefore, the IsInTriangle method is
            // additionally used.
            return
                IsClosed &&
                (
                    IsInTriangle(coordinateStart.X, coordinateStart.Y,
                        Coordinates[0].X, Coordinates[0].Y,
                        Coordinates[1].X, Coordinates[1].Y,
                        Coordinates[2].X, Coordinates[2].Y
                    ) ||
                    IsInTriangle(coordinateEnd.X, coordinateEnd.Y,
                        Coordinates[0].X, Coordinates[0].Y,
                        Coordinates[1].X, Coordinates[1].Y,
                        Coordinates[2].X, Coordinates[2].Y
                    )
                ) ||
                IntersectsWithLineString(coordinateStart, coordinateEnd);
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

        /// <summary>
        /// Check is the coordinate specified by "x" and "y"" is inside the given triangle using
        /// the barycentric collision check, which is considered one of the fastest ways to check is a point is inside
        /// a triangle.
        /// If "x" and "y" build a location on an edge or corner of the triangle, it's not
        /// considered "inside" and returns false.
        /// </summary>
        private static bool IsInTriangle(double x, double y, double x1, double y1, double x2, double y2, double x3,
            double y3)
        {
            var d = (x2 - x1) * (y3 - y1) - (x3 - x1) * (y2 - y1);

            var u = ((x2 - x) * (y3 - y) - (x3 - x) * (y2 - y)) / d;
            var v = ((x3 - x) * (y1 - y) - (x1 - x) * (y3 - y)) / d;
            var w = ((x1 - x) * (y2 - y) - (x2 - x) * (y1 - y)) / d;

            return 0 < u && u < 1 &&
                   0 < v && v < 1 &&
                   0 < w && w < 1;
        }

        public bool HasLineSegment(Coordinate coordinateStart, Coordinate coordinateEnd)
        {
            // Initialize start indices far away from each other because the distance matters below.
            var indexStart = -10;
            var indexEnd = -20;
            for (var i = 0; i < Coordinates.Count && (indexStart < 0 || indexEnd < 0); i++)
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
        public ShadowArea GetShadowAreaOfObstacle(Vertex vertex)
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
                    }
                }

                previousAngle = angleToNewCoordinate;
            }

            var distanceToFromCoordinate = Distance.Euclidean(vertex.Position.PositionArray,
                new[] { coordinateFrom[0], coordinateFrom[1] });
            var distanceToToCoordinate = Distance.Euclidean(vertex.Position.PositionArray,
                new[] { coordinateTo[0], coordinateTo[1] });
            var maxDistance = Math.Max(distanceToFromCoordinate, distanceToToCoordinate);

            return new ShadowArea(angleFrom, angleTo, maxDistance);
        }

        public override int GetHashCode()
        {
            return _hash;
        }
    }
}