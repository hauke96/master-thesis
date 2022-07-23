namespace Wavefront.Geometry;

public class Angle
{
    private const double FLOAT_TOLERANCE = 0.0001;

    /// <summary>
    /// Checks if the angle "angle" is between a and b.
    /// </summary>
    public static bool IsBetween(double a, double angle, double b)
    {
        var isLowerOrEqual = NormalizedLowerEqual(a, b, out a, out b);
        if (isLowerOrEqual)
        {
            angle = Normalize(angle);
            return a < angle && angle < b;
        }

        // We exceed the 0° border: Check if "angle" is NOT between b and a (which is the opposite part of the
        // imaginary circle and has no overlap with the 0° border.
        return !(LowerEqual(b, angle) && LowerEqual(angle, a));
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
    /// </summary>
    public static double StrictNormalize(double a)
    {
        return (a % 360 + 360) % 360;
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
        GetEnclosingAngles(a, b, out a, out b);
        return Difference(a, b) < FLOAT_TOLERANCE;
    }

    /// <summary>
    /// Returns a >= b. The equality check is made with a little bit of tolerance to compensate floating point inaccuracy.
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
    /// Returns a <= b. The equality check is made with a little bit of tolerance to compensate floating point inaccuracy.
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

    /// <summary>
    /// Returns a <= b. The equality check is made with a little bit of tolerance to compensate floating point inaccuracy.
    /// The given angles are normalized and returned via the two output variables.
    /// </summary>
    public static bool NormalizedLowerEqual(double a, double b, out double aNew, out double bNew)
    {
        aNew = Normalize(a);
        bNew = Normalize(b);
        return AreEqual(aNew, bNew) || aNew < bNew;
    }
}