using Mars.Components.Environments;
using Mars.Components.Environments.Cartesian;
using ServiceStack.Text;

namespace GeoJsonRouting.Model
{
    public class SharedEnvironment
    {
        /*
         * TODO Decide on an environment.
         * I think the GeoHashEnv. is the one to go as we probably want to use real spatial data. Collisions might
         * not that helpful when considering Mitchells algorithm. For visibility graphs, collisions might be helpful
         * but the graph is calculated prior to routing.
         */
        // public static CollisionEnvironment<Agent, IObstacle> Environment { get; set; }
        public static GeoHashEnvironment<Agent> Environment { get; set; }

        public static void Init()
        {
            // Environment = new CollisionEnvironment<Agent, IObstacle>();
            Environment = GeoHashEnvironment<Agent>.BuildByBBox(0, 0, 0.01, 0.01);
        }
    }
}