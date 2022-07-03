using Mars.Numerics;

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
            return a <= angle && angle <= b;
        }
        else
        {
            // We exceed the 0° border: Check if "angle" is NOT between b and a (which is the opposite part of the
            // imaginary circle and has no overlap with the 0° border.
            return !(b < angle && angle < a);
        }
    }

    public static double Difference(double a, double b)
    {
        a = Normalize(a);
        b = Normalize(b);
        return a < b ? b - a : 360 - a + b;
    }

    public static double Normalize(double a)
    {
        return ((a % 360) + 360) % 360;
    }
}