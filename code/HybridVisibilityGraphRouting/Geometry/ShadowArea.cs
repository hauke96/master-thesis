namespace HybridVisibilityGraphRouting.Geometry;

/// <summary>
/// Helper class modeling an angle area starting at a certain distance. Think of a piece of pizza that doesn't start
/// at the center of the pizza but somewhat further out.
/// </summary>
public class ShadowArea
{
    public double From { get; }
    public double To { get; }
    public double Distance { get; }

    public readonly bool IsValid;

    public ShadowArea(double from, double to, double distance)
    {
        From = from;
        To = to;
        Distance = distance;

        IsValid = !Double.IsNaN(From) && !Double.IsNaN(To) && !Double.IsNaN(Distance);
    }

    public override int GetHashCode()
    {
        // TODO Really just use the distance? Need this method in general, it's neither called during unit tests nor NetworkRoutingPlayground?
        return (int)(Distance * 13 + From * 31 + To * 23);
    }
}