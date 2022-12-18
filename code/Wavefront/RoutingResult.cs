using Mars.Numerics;

namespace Wavefront;

public class RoutingResult
{
    public List<Waypoint> OptimalRoute { get; }

    public double OptimalRouteLength
    {
        get
        {
            var length = 0.0;
            for (int i = 0; i < OptimalRoute.Count - 1; i++)
            {
                length += Distance.Haversine(OptimalRoute[i].Position.PositionArray,
                    OptimalRoute[i + 1].Position.PositionArray);
            }

            return length;
        }
    }

    public List<List<Waypoint>> AllRoutes { get; }
    // TODO Store all waypoints as well

    public RoutingResult(List<Waypoint> optimalRoute, List<List<Waypoint>> allRoutes)
    {
        OptimalRoute = optimalRoute;
        AllRoutes = allRoutes;
    }

    public RoutingResult()
    {
        OptimalRoute = new List<Waypoint>();
        AllRoutes = new List<List<Waypoint>>();
    }
}