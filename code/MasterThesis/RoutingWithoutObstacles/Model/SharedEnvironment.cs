using Mars.Components.Environments;
using Mars.Components.Environments.Cartesian;

namespace Model
{
    public class SharedEnvironment
    {
        // public static CollisionEnvironment<ICharacter, IObstacle> Environment { get; set; }
        public static GeoHashEnvironment<ICharacter> Environment { get; set; }

        public static void Init()
        {
            // Environment = new CollisionEnvironment<ICharacter, IObstacle>();
            Environment = GeoHashEnvironment<ICharacter>.BuildEnvironment(0, 0, 1, 1);
        }
    }
}