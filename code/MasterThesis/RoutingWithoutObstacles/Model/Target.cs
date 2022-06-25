using Mars.Common;
using Mars.Interfaces.Environments;

namespace RoutingWithoutObstacles.Model
{
    // TODO Clarify what a target should be (Entity, Agent, just a position as it's now, ...).
    public class Target
    {
        public static Position Position { get; set; }

        public static void SetRandomPosition()
        {
            Position = PositionHelper.RandomPositionFromGeometry(SharedEnvironment.Environment.BoundingBox);
        }
    }
}