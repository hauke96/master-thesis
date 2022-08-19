using Mars.Numerics;
using NetTopologySuite.Geometries;

namespace Wavefront.Geometry
{
    public class Obstacle
    {
        public readonly List<Coordinate> Coordinates;
        public readonly Envelope Envelope;
        public readonly bool IsClosed;
        public readonly int Hash;

        public Obstacle(NetTopologySuite.Geometries.Geometry geometry)
        {
            Coordinates = geometry.Coordinates.ToList();
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
            for (var i = 0; i < Coordinates.Count - 1; i++)
            {
                if (Intersect.DoIntersect(coordinateStart, coordinateEnd, Coordinates[i], Coordinates[i + 1]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Calculates the shadow area and max distance seen from a given vertex.
        /// </summary>
        public (double, double, double) GetAngleAreaOfObstacle(Vertex vertex)
        {
            var angleFrom = double.NaN;
            var angleTo = double.NaN;
            var previousAngle = double.NaN;
            var maxDistance = 0d;
            var coordinateArray = new double[2];

            // We have to manually go through all coordinates to build the shadow area. This is because the shadow might
            // exceed the 0° border and therefore simple min and max values cannot be used.
            for (var j = 0; j < Coordinates.Count - 1; j++)
            {
                var coordinate = Coordinates[j];

                if (Equals(coordinate, vertex.Coordinate))
                {
                    continue;
                }

                var a1 = Angle.GetBearing(vertex.Position.X, vertex.Position.Y, coordinate.X, coordinate.Y);

                if (!Angle.IsBetweenEqual(angleFrom, a1, angleTo))
                {
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

                    coordinateArray[0] = coordinate.X;
                    coordinateArray[1] = coordinate.Y;
                }

                var distance = Distance.Euclidean(vertex.Position.PositionArray, coordinateArray);
                maxDistance = distance > maxDistance ? distance : maxDistance;
            }

            return (angleFrom, angleTo, maxDistance);
        }

        public override int GetHashCode()
        {
            return Hash;
        }
    }
}