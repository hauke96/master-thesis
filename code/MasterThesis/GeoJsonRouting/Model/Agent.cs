using GeoJsonRouting.Layer;
using Mars.Common;
using Mars.Components.Layers;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using ServiceStack;
using Wavefront;
using Position = Mars.Interfaces.Environments.Position;

namespace GeoJsonRouting.Model
{
    // TODO Find a better name than just "Agent".
    public class Agent : IPositionable, IAgent<VectorLayer>
    {
        private static readonly int STEP_SIZE = 100;

        [PropertyDescription] public UnregisterAgent UnregisterHandle { get; set; }
        [PropertyDescription] public ObstacleLayer ObstacleLayer { get; set; }

        public Position? Position { get; set; }
        public Guid ID { get; set; }

        public Queue<Position> Waypoints = new Queue<Position>();

        public void Init(VectorLayer layer)
        {
        }

        public void Tick()
        {
            if (Position == null)
            {
                Position = ObstacleLayer.GetStart();
                Target.Position = ObstacleLayer.GetTarget();
                // Waypoints.Enqueue(Target.Position);

                DetermineNewWaypoints();

                SharedEnvironment.Environment.Insert(this);
            }

            var currentWaypoint = Waypoints.Peek();
            Console.WriteLine($"Tick with current waypoint {currentWaypoint}");

            var distanceToTargetInM = Position.DistanceInMTo(currentWaypoint);
            if (distanceToTargetInM == 0)
            {
                Waypoints.Dequeue();

                // The current waypoint was the last one -> we're done
                if (Waypoints.Count == 0)
                {
                    Console.WriteLine($"Target {currentWaypoint} reached.");
                    Kill();
                }
                return;
            }

            if (distanceToTargetInM < STEP_SIZE)
            {
                SharedEnvironment.Environment.MoveTo(this, currentWaypoint);
                return;
            }

            var bearing = Position.GetBearing(currentWaypoint);
            SharedEnvironment.Environment.MoveTowards(this, bearing, STEP_SIZE);

            // Thread.Sleep(TimeSpan.FromMilliseconds(0.5));
        }

        private void DetermineNewWaypoints()
        {
            // Target.NewPosition();
            // ResetPosition();
            //
            // if (Target.Position.Y >= 0.12)
            // {
            //     SharedEnvironment.Environment.Remove(this);
            //     return;
            // }

            var obstacleGeometries = ObstacleLayer.Features.Map(f => f.VectorStructured.Geometry);
            var wavefrontAlgorithm = new WavefrontAlgorithm(obstacleGeometries);
            try
            {
                Waypoints = new Queue<Position>(wavefrontAlgorithm.Route(Position, Target.Position));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private void Kill()
        {
            SharedEnvironment.Environment.Remove(this);
            UnregisterHandle.Invoke(ObstacleLayer, this);
        }
    }
}