using Mars.Common;
using Mars.Interfaces.Environments;

namespace RoutingWithLineObstacle.Model
{
    // TODO Clarify what a target should be (Entity, Agent, just a position as it's now, ...).
    public class Target
    {
        public static Position Position { get; set; } = Position.CreateGeoPosition(0, -0.00015);

        public static void NewPosition()
        {
            // Position = PositionHelper.RandomPositionFromGeometry(SharedEnvironment.Environment.BoundingBox);
            Position += Position.CreateGeoPosition(0.0, 0.0001);
        }
    }
}