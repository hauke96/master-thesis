using Mars.Interfaces.Environments;

namespace Wavefront;

public class Waypoint
{
    public Position Position { get; }
    public int Order { get; }
    public double Time { get; }

    public Waypoint(Position position, int order, double time)
    {
        Position = position;
        Order = order;
        Time = time;
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
}