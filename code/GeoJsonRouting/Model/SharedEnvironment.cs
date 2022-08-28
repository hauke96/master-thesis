using Mars.Components.Environments.Cartesian;

namespace GeoJsonRouting.Model
{
    public static class SharedEnvironment
    {
        public static CartesianEnvironment<Agent> Environment { get; set; }

        public static void Init()
        {
            Environment = new CartesianEnvironment<Agent>();
        }
    }
}