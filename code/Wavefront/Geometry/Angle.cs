using Mars.Numerics;
using NetTopologySuite.Geometries;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront.Geometry;

public class Angle
{
    private const double FLOAT_TOLERANCE = 0.0001;

    public static double GetBearing(Position a, Position b)
    {
        return GetBearing(a.X, a.Y, b.X, b.Y);
    }

    public static double GetBearing(Coordinate a, Coordinate b)
    {
        return GetBearing(a.X, a.Y, b.X, b.Y);
    }

    public static double GetBearing(double aX, double aY, double bX, double bY)
    {
        var degrees = MathHelper.ToDegrees(Math.Atan2(bX - aX, bY - aY));
        return StrictNormalize(degrees);
    }

    /// <summary>
    /// Checks if the angle "angle" is between a and b.
    /// </summary>
    public static bool IsBetweenWithNormalize(double a, double angle, double b)
    {
        a = Normalize(a);
        b = Normalize(b);
        angle = Normalize(angle);

        // 0° is NOT between a and b -> normal check
        if (a < b || AreEqual(a, b))
        {
            return a < angle && angle < b;
        }

        return b > angle || angle > a;
    }

    /// <summary>
    /// Checks if the angle "angle" is between a and b.
    /// This method assumes that all angles are correctly normalized.
    /// </summary>
    public static bool IsBetween(double a, double angle, double b)
    {
        if (a > b)
        {
            // a to b exceeds to 0° border
            angle = angle < b ? angle + 360.0 : angle;
            b += 360.0;
        }

        return a < angle && angle < b;
    }

    /// <summary>
    /// Checks if the angle "angle" is between a and b including being equal to a or b.
    /// This method assumes that all angles are correctly normalized.
    /// </summary>
    public static bool IsBetweenEqual(double a, double angle, double b)
    {
        // As other equal methods do: Allow some small inaccuracy for floats
        a -= FLOAT_TOLERANCE;
        b += FLOAT_TOLERANCE;
        if (a > b)
        {
            // a to b exceeds the 0° border
            angle = LowerEqual(angle, b) ? angle + 360.0 : angle;
            b += 360.0;
        }

        return a <= angle && angle <= b;
    }

    public static double Difference(double a, double b)
    {
        return a <= b ? b - a : 360 - a + b;
    }

    /// <summary>
    /// Normalizes the angle. For convenience, 360° stays 360°.
    /// </summary>
    public static double Normalize(double a)
    {
        if (a == 360)
        {
            return a;
        }

        return StrictNormalize(a);
    }

    /// <summary>
    /// Like "Normalize(double)" but 360° will be turned into 0.
    ///
    /// Assumption: -360 <= a < 720
    /// </summary>
    public static double StrictNormalize(double a)
    {
        if (a >= 360.0)
        {
            return a - 360.0;
        }

        if (a < 0.0)
        {
            return a + 360.0;
        }

        return a;
    }

    /// <summary>
    /// Calculates the enclosing angle between the original angles. Meaning the angle between them that's at most 180°.
    /// Or in other words: An angle between the returned fromAngle and toAngle is "inside" that area.
    ///
    /// Precondition:
    /// The angles are strictly normalized (e.g. by using the StrictNormalize(double) method).
    ///
    /// Example:
    /// Say originalFrom is 10° and originalTo is 200° building an angle of 190°. Then the returning fromAngle is
    /// 200° and toAngle is 10° marking an angle of 170°.
    /// </summary>
    public static void GetEnclosingAngles(double originalFromAngle, double originalToAngle, out double fromAngle,
        out double toAngle)
    {
        fromAngle = originalFromAngle;
        toAngle = originalToAngle;
        if (Difference(fromAngle, toAngle) > 180)
        {
            fromAngle = originalToAngle;
            toAngle = originalFromAngle;
        }
    }

    /// <summary>
    /// Returns a == b. The equality check is made with a little bit of tolerance to compensate floating point inaccuracy.
    /// </summary>
    public static bool AreEqual(double a, double b)
    {
        a = StrictNormalize(a);
        b = StrictNormalize(b);
        return Difference(a, b) < FLOAT_TOLERANCE || Difference(b, a) < FLOAT_TOLERANCE;
    }

    /// <summary>
    /// Returns a >= b. Both angles will be normalized before checking the greater-equal-relation. The equality check is
    /// made with a little bit of tolerance to compensate floating point inaccuracy.
    /// </summary>
    public static bool GreaterEqual(double a, double b)
    {
        if (AreEqual(a, b))
        {
            return true;
        }

        a = Normalize(a);
        b = Normalize(b);
        return a > b;
    }

    /// <summary>
    /// Returns a <= b. Both angles will be normalized before checking the greater-equal-relation. The equality check is
    /// made with a little bit of tolerance to compensate floating point inaccuracy.
    /// </summary>
    public static bool LowerEqual(double a, double b)
    {
        if (AreEqual(a, b))
        {
            return true;
        }

        a = Normalize(a);
        b = Normalize(b);
        return a < b;
    }

    /**
     * Merges the given angle intervals. The returned interval might exceed the 0° border. If no merge was possible
     * (because the intervals do not overlap or touch), a (-1, -1) interval is returned.
     */
    public static (double, double) Merge(double from1, double to1, double from2, double to2)
    {
        if (AreEqual(to1, from2))
        {
            // Areas touch on the to1 point.
            return (from1, to2);
        }

        if (AreEqual(from1, to2))
        {
            // Areas touch on the from1 point.
            return (from2, to1);
        }

        if (IsBetweenEqual(from1, from2, to1) && IsBetweenEqual(from1, to2, to1))
        {
            // Area 2 is within area 1.
            return (from1, to1);
        }

        if (IsBetweenEqual(from2, from1, to2) && IsBetweenEqual(from2, to1, to2))
        {
            // Area 1 is within area 2.
            return (from2, to2);
        }

        if (IsBetweenEqual(from1, from2, to1))
        {
            // Area 2 starts within area 1 but is not completely within (s. above).
            return (from1, to2);
        }

        if (IsBetweenEqual(from1, to2, to1))
        {
            // Area 2 ends within area 1 but is not completely within (s. above).
            return (from2, to1);
        }

        return (-1, -1);
    }
}