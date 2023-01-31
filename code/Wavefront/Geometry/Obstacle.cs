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

        public Obstacle(NetTopologySuite.Geometries.Geometry geometry) : this(geometry,
            geometry.Coordinates.Map(c => new Vertex(c.X, c.Y)))
        {
        }

        public Obstacle(NetTopologySuite.Geometries.Geometry geometry, List<Vertex> vertices)
        {
            Coordinates = geometry.Coordinates.ToList();
            Vertices = vertices;
            Hash = (int)geometry.Coordinates.Sum(coordinate => coordinate.X * 7919 + coordinate.Y * 4813);
            Envelope = geometry.EnvelopeInternal;
            IsClosed = Equals(Coordinates.First(), Coordinates.Last());
        }

        public bool CanIntersect(Envelope envelope)
        {
            return Coordinates.Count >= 2 && Envelope.Intersects(envelope);
        }

        public bool IntersectsWithLine(Coordinate coordinateStart, Coordinate coordinateEnd)
        {
            // Used below when a vertex is found lying exactly on the line between start and end -> the angle from
            // start to that vertex is exactly this angle here, so we can pre-calculate it.
            var rotationAngle = -1 * Angle.GetBearing(coordinateStart, coordinateEnd);

            for (var i = 0; i < Coordinates.Count - 1; i++)
            {
                var coordinate = Coordinates[i];

                if (Intersect.DoIntersect(coordinateStart, coordinateEnd, coordinate, Coordinates[i + 1]))
                {
                    return true;
                }

                // They do not intersect. However, it can happen that the coordinate i is exactly on the line from the
                // given start the the given end. In this case it's interesting to know if there are other obstacles
                // touching the coordinate i causing the line from the given start to the given end to intersect the
                // connection between these two obstacles.
                if (!coordinateStart.Equals(coordinate) && !coordinateEnd.Equals(coordinate) &&
                    Intersect.Orientation(coordinateStart, coordinateEnd, coordinate) == 0 &&
                    Intersect.IsOnSegment(coordinateStart, coordinateEnd, coordinate))
                {
                    // The coordinate i lies exactly on the line segment from the given start to the given end
                    // coordinate. So we check if any neighbor of that coordinate/vertex is on the left or right side.
                    // If not, so if at least one neighbor is on the east and one on the west side, then there's an
                    // intersection. Also see the WavefrontAlgorithm.HandleNeighbors for a similar handling of
                    // intersection checks.
                    var vertex = Vertices.First(v => v.Coordinate.Equals(coordinate));
                    var allVerticesOnSameSide = vertex.Neighbors
                        .Where(n => n.X != coordinateStart.X || n.Y != coordinateStart.Y)
                        .Map(n =>
                        {
                            var angleFromStartToNeighbor =
                                Angle.GetBearing(coordinateStart.X, coordinateStart.Y, n.X, n.Y);
                            var angle = Angle.Normalize(angleFromStartToNeighbor + rotationAngle);
                            // Return -1 (west side), 0 (neither side and exactly aligned) or 1 (east side) to specify on
                            // which side the neighbor is.
                            return angle == 0 || angle == 180 ? 0 : angle < 180 ? 1 : -1;
                        })
                        // Neighbors with angle of 0 are aligned (directly in front of the vertex seen from
                        // coordinateStart) and can be ignored since such a neighbor would be visible from the start.
                        .Where(a => a != 0)
                        .Distinct()
                        .Count() == 1; // Only one element after .Distinct() -> all elements are the same

                    if (!allVerticesOnSameSide)
                    {
                        return true;
                    }
                }
            }

            return false;
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