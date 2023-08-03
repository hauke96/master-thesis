using NetTopologySuite.Geometries;

namespace HybridVisibilityGraphRouting.Geometry
{
    /// <summary>
    /// This class represents an obstacle, which is not passable by visibility edges.
    /// 
    /// The geometry of an obstacle is either a Point, LineString or triangle (Polygon/MultiPolygon). Closed geometries
    /// with a vertex count of !=4 are not permitted due to assumptions on the collision checks.
    /// 
    /// This class not only wraps a geometry, it also contains the respective vertices, which hold information about
    /// neighboring/touching obstacles.
    /// </summary>
    public class Obstacle
    {
        public readonly List<Coordinate> Coordinates;
        public readonly Envelope Envelope;
        public readonly bool IsClosed;
        public readonly List<Vertex> Vertices;
        public readonly NetTopologySuite.Geometries.Geometry OriginalGeometry;

        private readonly int _hash;

        /// <summary>
        /// Creates a new obstacle. The geometry but either be a Point, LineString or a triangle (Multi)Polygon. 
        /// </summary>
        public Obstacle(NetTopologySuite.Geometries.Geometry geometry,
            NetTopologySuite.Geometries.Geometry originalGeometry, List<Vertex> vertices)
        {
            if (geometry is not Polygon && geometry is not LineString && geometry is not Point)
            {
                throw new Exception("The obstacle geometry must be of type Polygon, LineString or Point!");
            }

            // If not all coordinates have any corresponding vertex (meaning if any coordinate has no corresponding
            // vertex), then throw an exception. We always need vertices for each coordinate. 
            if (!geometry.Coordinates.All(c => vertices.Any(v => v.Coordinate.Equals(c))))
            {
                throw new Exception("The obstacle geometry must have a vertex for each coordinate!");
            }

            Coordinates = geometry.Coordinates.ToList();
            IsClosed = GeometryHelper.IsGeometryClosed(geometry);
            if (IsClosed && Coordinates.Count > 4 && (geometry is Polygon || geometry is MultiPolygon))
            {
                throw new Exception(
                    $"Obstacle of type {geometry.GeometryType} is closed but not a triangle (has {geometry.Coordinates.Length - 1} vertices)!");
            }

            var isCounterClockwiseTriangle = IsClosed && Intersect.Orientation(Coordinates[0], Coordinates[1], Coordinates[2]) == -1;
            if (isCounterClockwiseTriangle)
            {
                // Triangle coordinates must be in clockwise rotation for "isInTriangle" checks.
                Coordinates.Reverse();
            }

            Vertices = vertices;
            Envelope = geometry.EnvelopeInternal;
            OriginalGeometry = originalGeometry;
            _hash = (Coordinates, IsClosed, Vertices, Envelope, OriginalGeometry).GetHashCode();
        }

        public bool CanIntersect(Envelope envelope)
        {
            return Coordinates.Count >= 2 && Envelope.Intersects(envelope);
        }

        /// <summary>
        /// Check if this obstacle intersects with the given line, specified by the two coordinate parameters. <br/>
        /// <br/>
        /// A few remarks and edge cases on what's considered an intersection:
        /// <ul>
        ///   <li>A line touching the obstacle (i.e. having at least one coordinate in common) does <i>not</i> intersect this obstacle.</li>
        ///   <li>A line fully within the obstacle (in case of a closed polygon) intersects this obstacle.</li>
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
        /// Checks if the line segment, specified by the two given coordinates, intersects with this open obstacle. Both
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
        /// Check for intersection with the given line segment, specified by the two coordinates. It's assumes that one
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
                    IsInTriangle(
                        coordinateStart,
                        Coordinates[0],
                        Coordinates[1],
                        Coordinates[2]
                    ) ||
                    IsInTriangle(
                        coordinateEnd,
                        Coordinates[0],
                        Coordinates[1],
                        Coordinates[2]
                    )
                ) ||
                IntersectsWithLineString(coordinateStart, coordinateEnd);
        }

        /// <summary>
        /// Checks if the given line segment, specified by the coordinates, intersects with any line segment of the
        /// obstacle. It is assumes that the given line segment is NOT part of this obstacle.
        /// </summary>
        private bool IntersectsWithLineString(Coordinate coordinateStart, Coordinate coordinateEnd)
        {
            var intersectsWithSegment = false;
            for (var i = 1; !intersectsWithSegment && i < Coordinates.Count; i++)
            {
                var coordinate = Coordinates[i - 1];

                var coordinateInCommon = coordinateStart.Equals(coordinate) || coordinateStart.Equals(Coordinates[i]) ||
                                         coordinateEnd.Equals(coordinate) || coordinateEnd.Equals(Coordinates[i]);

                // If there's a coordinate in common, then it's considered to be neither an intersection nor a
                // touch-situation. Therefore, intersection checks are only performed for line segments that are
                // completely unrelated to this obstacle.
                if (!coordinateInCommon)
                {
                    intersectsWithSegment |=
                        Intersect.DoIntersectOrTouch(coordinateStart, coordinateEnd, coordinate, Coordinates[i]);
                }
            }

            return intersectsWithSegment;
        }

        /// <summary>
        /// Checks if "p" is inside the given triangle using a barycentric collision check. If "p" is location on an
        /// edge or corner of the triangle, it's not considered "inside" and returns false.
        /// </summary>
        public static bool IsInTriangle(Coordinate p, Coordinate c1, Coordinate c2, Coordinate c3)
        {
            var d = (c2.X - c1.X) * (c3.Y - c1.Y) - (c3.X - c1.X) * (c2.Y - c1.Y);

            var u = (c2.X - p.X) * (c3.X - p.Y) - (c3.X - p.X) * (c2.Y - p.Y);
            var v = (c3.X - p.X) * (c1.Y - p.Y) - (c1.X - p.X) * (c3.Y - p.Y);
            var w = (c1.X - p.X) * (c2.Y - p.Y) - (c2.X - p.X) * (c1.Y - p.Y);

            return 0 < u && u < d &&
                   0 < v && v < d &&
                   0 < w && w < d;
        }

        /// <summary>
        /// Checks if this obstacle contains a line segment specified by the two coordinates. The order of the
        /// coordinates is irrelevant.
        /// </summary>
        public bool HasLineSegment(Coordinate coordinateStart, Coordinate coordinateEnd)
        {
            // Offset of 1 for closed obstacles because first and last coordinates are the same.
            var offset = IsClosed ? 1 : 0;
            var indexBefore = Coordinates.Count - 1 - offset; // last coordinate
            var indexAfter = 1; // second coordinate

            for (var i = 0; i < Coordinates.Count - offset; i++)
            {
                if (Coordinates[i].Equals(coordinateStart))
                {
                    // The coordinateStart is at index i, check if coordinateEnd is right before/after it.
                    return Coordinates[indexBefore].Equals(coordinateEnd) ||
                           Coordinates[indexAfter].Equals(coordinateEnd);
                }

                indexBefore = (indexBefore + 1) % (Coordinates.Count - 1);
                indexAfter = (indexAfter + 1) % (Coordinates.Count - 1);
            }

            return false;
        }

        /// <summary>
        /// Calculates the shadow area seen from a given vertex.
        /// </summary>
        public ShadowArea? GetShadowAreaOfObstacle(Vertex vertex)
        {
            var angleFrom = double.NaN;
            var angleTo = double.NaN;
            var previousAngle = double.NaN;

            // Remember the coordinate defining the angleFrom and angleTo area to determine the distance of the shadow
            // area once it has been established. 
            Coordinate coordinateFrom = null;
            Coordinate coordinateTo = null;

            double distanceToFromCoordinate = double.PositiveInfinity;
            double distanceToToCoordinate = double.PositiveInfinity;

            // We have to manually go through all coordinates to build the shadow area. This is because the shadow might
            // exceed the 0° border and therefore simple min and max values on angles cannot be used.
            foreach (var coordinate in Coordinates)
            {
                if (Equals(coordinate, vertex.Coordinate))
                {
                    continue;
                }

                var angleToNewCoordinate = Angle.GetBearing(vertex.Coordinate, coordinate);

                if (Double.IsNaN(previousAngle))
                {
                    // At the first coordinate, there's no previous angle, so we set it here.
                    previousAngle = angleToNewCoordinate;
                }

                // Ignore coordinates with an angle that is within the already found angle area.
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
                            distanceToFromCoordinate = vertex.Coordinate.Distance(coordinateFrom);
                        }

                        if (mergedAngleTo == newSegmentAngleTo && mergedAngleTo != angleTo)
                        {
                            coordinateTo = coordinate;
                            distanceToToCoordinate = vertex.Coordinate.Distance(coordinateTo);
                        }

                        angleFrom = mergedAngleFrom;
                        angleTo = mergedAngleTo;
                    }
                }
                else if (Angle.AreEqual(angleFrom, angleToNewCoordinate))
                {
                    // If a coordinate was found at the edge of the current angle area, then check its distance. If the
                    // distance to this coordinate is lower than the current distance, then store the coordinate. This
                    // ensures the resulting shadow area to be as close as possible to the given vertex.
                    var distance = vertex.Coordinate.Distance(coordinate);
                    if (distance < distanceToFromCoordinate)
                    {
                        distanceToFromCoordinate = distance;
                        coordinateFrom = coordinate;
                    }
                }
                else if (Angle.AreEqual(angleTo, angleToNewCoordinate))
                {
                    // If a coordinate was found at the edge of the current angle area, then check its distance. If the
                    // distance to this coordinate is lower than the current distance, then store the coordinate. This
                    // ensures the resulting shadow area to be as close as possible to the given vertex.
                    var distance = vertex.Coordinate.Distance(coordinate);
                    if (distance < distanceToToCoordinate)
                    {
                        distanceToToCoordinate = distance;
                        coordinateTo = coordinate;
                    }
                }

                previousAngle = angleToNewCoordinate;
            }

            distanceToFromCoordinate = vertex.Coordinate.Distance(coordinateFrom);
            distanceToToCoordinate = vertex.Coordinate.Distance(coordinateTo);
            var maxDistance = Math.Max(distanceToFromCoordinate, distanceToToCoordinate);

            return ShadowArea.Of(angleFrom, angleTo, maxDistance);
        }

        public override int GetHashCode()
        {
            return _hash;
        }
    }
}