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
            return IsOnSegment(start, end, p, Orientation(start, end, p));
        }

        /// <summary>
        /// Precondition: The segments p, q and r are collinear (-> Orientation(p, q, r) == 0).
        ///
        /// <param name="orientation">The orientation of p to the other coordinates. Only a collinear p (with means
        /// orientation == 0) can be on the line segment.</param>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOnSegment(Coordinate start, Coordinate end, Coordinate p, int orientation)
        {
            return orientation == 0 &&
                   Math.Min(start.X, end.X) <= p.X && p.X <= Math.Max(start.X, end.X) &&
                   Math.Min(start.Y, end.Y) <= p.Y && p.Y <= Math.Max(start.Y, end.Y);
        }

        /// <summary>
        /// Determines the orientation between the line segments p0p1 and p0p2. This means it determines if the line
        /// string p0p1p2 turns right or left at p1.
        /// </summary>
        /// <returns>
        /// <ul>
        /// <li>Returns -1, if p0p2 is counterclockwise to p0p1</li>
        /// <li>Returns 1, if p0p2 is clockwise to p0p1</li>
        /// <li>Returns 0, if p0, p1 and p2 are collinear (???)</li>
        /// </ul>
        /// </returns>
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

            return !AreTouching(start1, end1, start2, end2, orientation1, orientation2, orientation3, orientation4);
        }

        public static bool DoIntersectOrTouch(Coordinate start1, Coordinate end1, Coordinate start2, Coordinate end2)
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
                return true;
            }

            var orientation1 = Orientation(start2, end2, start1);
            var orientation2 = Orientation(start2, end2, end1);
            var orientation3 = Orientation(start1, end1, start2);
            var orientation4 = Orientation(start1, end1, end2);

            if (AreTouching(start1, end1, start2, end2, orientation1, orientation2, orientation3, orientation4))
            {
                return true;
            }
            
            if (orientation1 == orientation2)
            {
                return false;
            }
            
            if (orientation3 == orientation4)
            {
                return false;
            }

            return true;
        }

        private static bool AreTouching(Coordinate start1, Coordinate end1, Coordinate start2, Coordinate end2,
            int orientation1, int orientation2, int orientation3, int orientation4)
        {
            return IsOnSegment(start2, end2, start1, orientation1) ||
                   IsOnSegment(start2, end2, end1, orientation2) ||
                   IsOnSegment(start1, end1, start2, orientation3) ||
                   IsOnSegment(start1, end1, end2, orientation4);
        }
    }
}