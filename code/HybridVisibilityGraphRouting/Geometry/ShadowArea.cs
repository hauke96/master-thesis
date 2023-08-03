namespace HybridVisibilityGraphRouting.Geometry;

/// <summary>
/// Shadow areas are angle areas (angular ranges) starting at a certain distance. Think of a piece of pizza that doesn't
/// start at the center of the pizza but somewhat further out.
/// </summary>
public class ShadowArea
{
    public double From { get; }
    public double To { get; }
    public double Distance { get; }

    private ShadowArea(double from, double to, double distance)
    {
        From = from;
        To = to;
        Distance = distance;
    }

    public static ShadowArea? Of(double from, double to, double distance)
    {
        var isValid = !Double.IsNaN(from) && !Double.IsNaN(to) && !Double.IsNaN(distance);
        return isValid ? new ShadowArea(from, to, distance) : null;
    }

    public override int GetHashCode()
    {
        return (int)(Distance * 13 + From * 31 + To * 23);
    }
}