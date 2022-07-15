namespace Wavefront.Geometry;

public class Angle
{
    /// <summary>
    /// Checks if the angle "angle" is between a and b.
    /// </summary>
    public static bool IsBetween(double a, double angle, double b)
    {
        a = Normalize(a);
        b = Normalize(b);
        angle = Normalize(angle);

        if (a <= b)
        {
            return a < angle && angle < b;
        }

        // We exceed the 0° border: Check if "angle" is NOT between b and a (which is the opposite part of the
        // imaginary circle and has no overlap with the 0° border.
        return !(b <= angle && angle <= a);
    }

    public static bool IsEnclosedBy(double a, double angle, double b)
    {
        GetEnclosingAngles(a, b, out a, out b);
        return IsBetween(a, angle, b);
    }

    public static double Difference(double a, double b)
    {
        a = Normalize(a);
        b = Normalize(b);
        return a <= b ? b - a : 360 - a + b;
    }

    public static double Normalize(double a)
    {
        if (a == 360)
        {
            return a;
        }

        return ((a % 360) + 360) % 360;
    }

    /// <summary>
    /// Calculates the enclosing angle between the original angles. Meaning the angle between them that's at most 180°.
    /// Or in other words: An angle between the returned fromAngle and toAngle is "inside" that area.
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

    public static bool Overlap(double from1, double to1, double from2, double to2)
    {
        return IsBetween(from1, from2, to1) || IsBetween(from1, to2, to1) ||
               IsBetween(from2, from1, to2) || IsBetween(from2, to1, to2);
    }

    public static double GetAbsoluteValue(double fromAngle, double toAngle)
    {
        return Normalize(toAngle) - Normalize(fromAngle);
    }
}