using Mars.Components.Environments.Cartesian;

namespace GeoJsonRouting.Model
{
    public static class SharedEnvironment
    {
        public static CollisionEnvironment<ICharacter, IObstacle> Environment { get; set; }

        public static void Init()
        {
            Environment = new CollisionEnvironment<ICharacter, IObstacle>();
        }
    }
}