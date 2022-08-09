using Mars.Interfaces.Environments;
using Mars.Numerics;

namespace Wavefront.Geometry;

public class Angle
{
    public const double FLOAT_TOLERANCE = 0.0001;

    public static double GetBearing(Position a, Position b)
    {
        var aX = a.X;
        var aY = a.Y;
        var bX = b.X;
        var bY = b.Y;
        return GetBearing(aX, aY, bX, bY);
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
            // a to b exceeds to 0° border
            angle = angle < b ? angle + 360.0 : angle;
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
        return NormalizedAreEqual(a, b);
    }

    /// <summary>
    /// Actual logic for the AreEqual method. Assumes that parameters are correctly normalized.
    /// </summary>
    private static bool NormalizedAreEqual(double a, double b)
    {
        var diff = a >= b ? a - b : b - a;
        return diff < FLOAT_TOLERANCE;
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
}