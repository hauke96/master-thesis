namespace Wavefront;

public class RoutingResult
{
    public List<Waypoint> OptimalRoute { get; }
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