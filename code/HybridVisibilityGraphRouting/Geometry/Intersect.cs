using System.Runtime.CompilerServices;
using NetTopologySuite.Geometries;

namespace HybridVisibilityGraphRouting.Geometry
{
    /// <summary>
    /// Provides a method to check for intersection of two line segments.
    ///
    /// Credit goes to Cormen 3rd edition chapter 33.1. 
    /// </summary>
    public static class Intersect
    {
        /// <summary>
        /// Precondition: The segments p, q and r are collinear (-> Orientation(p, q, r) should return 0).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOnSegment(Coordinate start, Coordinate end, Coordinate p)
        {
            return Math.Min(start.X, end.X) <= p.X && p.X <= Math.Max(start.X, end.X) &&
                   Math.Min(start.Y, end.Y) <= p.Y && p.Y <= Math.Max(start.Y, end.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Orientation(Coordinate p0, Coordinate p1, Coordinate p2)
        {
            var val = (p1.X - p0.X) * (p2.Y - p0.Y) - (p2.X - p0.X) * (p1.Y - p0.Y);

            if (val == 0)
            {
                return 0;
            }

            return (val > 0) ? 1 : -1;
        }

        public static bool DoIntersect(Coordinate start1, Coordinate end1, Coordinate start2, Coordinate end2)
        {
            if (
                start1.X > start2.X && start1.X > end2.X && end1.X > start2.X && end1.X > end2.X ||
                start1.X < start2.X && start1.X < end2.X && end1.X < start2.X && end1.X < end2.X ||
                start1.Y > start2.Y && start1.Y > end2.Y && end1.Y > start2.Y && end1.Y > end2.Y ||
                start1.Y < start2.Y && start1.Y < end2.Y && end1.Y < start2.Y && end1.Y < end2.Y
            )
            {
                return false;
            }

            if (start1.Equals(start2) || end1.Equals(end2) ||
                start1.Equals(end2) || end1.Equals(start2))
            {
                return false;
            }

            var orientation1 = Orientation(start2, end2, start1);
            var orientation2 = Orientation(start2, end2, end1);
            if (orientation1 == orientation2)
            {
                return false;
            }

            var orientation3 = Orientation(start1, end1, start2);
            var orientation4 = Orientation(start1, end1, end2);
            if (orientation3 == orientation4)
            {
                return false;
            }

            return !(orientation1 == 0 && IsOnSegment(start2, end2, start1) ||
                     orientation2 == 0 && IsOnSegment(start2, end2, end1) ||
                     orientation3 == 0 && IsOnSegment(start1, end1, start2) ||
                     orientation4 == 0 && IsOnSegment(start1, end1, end2));
        }
    }
}