using Mars.Interfaces.Environments;

namespace Wavefront;

public class Waypoint
{
    public Position Position { get; }
    public int Order { get; }
    public double Time { get; }
    public double DistanceFromStart { get; }

    public Waypoint(Position position, int order, double time, double distanceFromStart)
    {
        Position = position;
        Order = order;
        Time = time;
        DistanceFromStart = distanceFromStart;
    }

    public override bool Equals(object? obj)
    {
        return base.Equals(obj);
    }

    protected bool Equals(Waypoint other)
    {
        return Position.Equals(other.Position) && Order == other.Order && Time.Equals(other.Time);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Position, Order, Time);
    }

    public override string ToString()
    {
        return Position.ToString();
    }
}