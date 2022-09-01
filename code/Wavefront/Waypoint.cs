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
}