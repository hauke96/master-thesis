namespace Wavefront.Geometry;

public class Angle
{
    private const double FLOAT_TOLERANCE = 0.0001;

    /// <summary>
    /// Checks if the angle "angle" is between a and b.
    /// </summary>
    public static bool IsBetween(double a, double angle, double b)
    {
        a = Normalize(a);
        b = Normalize(b);
        angle = Normalize(angle);

        if (LowerEqual(a, b))
        {
            return a < angle && angle < b;
        }

        // We exceed the 0° border: Check if "angle" is NOT between b and a (which is the opposite part of the
        // imaginary circle and has no overlap with the 0° border.
        return !(LowerEqual(b, angle) && LowerEqual(angle, a));
    }

    public static bool IsEnclosedBy(double a, double angle, double b)
    {
        GetEnclosingAngles(a, b, out a, out b);
        return IsBetween(a, angle, b);
    }

    public static double Difference(double a, double b)
    {
        a = StrictNormalize(a);
        b = StrictNormalize(b);
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
    /// </summary>
    public static double StrictNormalize(double a)
    {
        return (a % 360 + 360) % 360;
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

    /// <summary>
    /// Returns a == b. The equality check is made with a little bit of tolerance to compensate floating point inaccuracy.
    /// </summary>
    public static bool AreEqual(double a, double b)
    {
        a = StrictNormalize(a);
        b = StrictNormalize(b);
        GetEnclosingAngles(a, b, out a, out b);
        return Difference(a, b) < FLOAT_TOLERANCE;
    }

    /// <summary>
    /// Returns a >= b. The equality check is made with a little bit of tolerance to compensate floating point inaccuracy.
    /// </summary>
    public static bool GreaterEqual(double a, double b)
    {
        a = Normalize(a);
        b = Normalize(b);
        return AreEqual(a, b) || a > b;
    }

    /// <summary>
    /// Returns a <= b. The equality check is made with a little bit of tolerance to compensate floating point inaccuracy.
    /// </summary>
    public static bool LowerEqual(double a, double b)
    {
        a = Normalize(a);
        b = Normalize(b);
        return AreEqual(a, b) || a < b;
    }
}