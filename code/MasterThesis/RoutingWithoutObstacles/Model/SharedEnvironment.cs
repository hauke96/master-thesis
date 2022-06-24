using Mars.Components.Environments;

namespace RoutingWithoutObstacles.Model
{
    public class SharedEnvironment
    {
        // public static CollisionEnvironment<Agent, IObstacle> Environment { get; set; }
        public static GeoHashEnvironment<Agent> Environment { get; set; }

        public static void Init()
        {
            // Environment = new CollisionEnvironment<Agent, IObstacle>();
            Environment = GeoHashEnvironment<Agent>.BuildByBBox(0, 0, 1, 1);
        }
    }
}