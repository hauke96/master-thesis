using Mars.Common;
using NetTopologySuite.Geometries;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront.Geometry
{
    public class Obstacle
    {
        public readonly List<Coordinate> Coordinates;
        public readonly Envelope Envelope;
        public readonly bool IsClosed;
        public readonly Position Center;

        public Obstacle(NetTopologySuite.Geometries.Geometry geometry)
        {
            Coordinates = geometry.Coordinates.ToList();
            Envelope = geometry.EnvelopeInternal;
            Center = geometry.Centroid.Coordinate.ToPosition();
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
    }
}