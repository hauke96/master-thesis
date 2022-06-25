using Mars.Common;
using Mars.Components.Layers;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Environments;

namespace RoutingWithoutObstacles.Model
{
    // TODO Find a better name than just "Agent".
    public class Agent : IPositionable, IAgent<VectorLayer>
    {
        private static readonly int STEP_SIZE = 1;

        public Position Position { get; set; }
        public Guid ID { get; set; }

        public void Init(VectorLayer layer)
        {
            Position = Position.CreateGeoPosition(0, 0);
            // SharedEnvironment.Environment.Insert(this, Position);
            SharedEnvironment.Environment.Insert(this);
        }

        public void Tick()
        {
            var distanceToTargetInM = Position.DistanceInMTo(Target.Position);
            if (distanceToTargetInM < STEP_SIZE)
            {
                Console.WriteLine("Target reached. Initialite new target.");
                Target.SetRandomPosition();
                return;
            }

            Console.WriteLine($"Distance to target: {Math.Round(distanceToTargetInM, 2)}m");

            var bearing = Position.GetBearing(Target.Position);

            // SharedEnvironment.Environment.Move(this, 45, 10);
            SharedEnvironment.Environment.MoveTowards(this, bearing, STEP_SIZE);
        }
    }
}