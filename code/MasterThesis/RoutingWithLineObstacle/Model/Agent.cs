using Mars.Common;
using Mars.Components.Layers;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using NetTopologySuite.Geometries;
using RoutingWithLineObstacle.Layer;
using RoutingWithLineObstacle.Wavefront;
using ServiceStack;
using Wavefront;
using Position = Mars.Interfaces.Environments.Position;

namespace RoutingWithLineObstacle.Model
{
    // TODO Find a better name than just "Agent".
    public class Agent : IPositionable, IAgent<VectorLayer>
    {
        private static readonly int STEP_SIZE = 100;

        [PropertyDescription] public ObstacleLayer ObstacleLayer { get; set; }

        public Position Position { get; set; }
        public Guid ID { get; set; }

        public Queue<Position> Waypoints = new Queue<Position>();

        public void Init(VectorLayer layer)
        {
            ResetPosition();

            // SharedEnvironment.Environment.Insert(this, Position);
            SharedEnvironment.Environment.Insert(this);

            Target.NewPosition();
            Waypoints.Enqueue(Target.Position);
        }

        public void Tick()
        {
            var currentWaypoint = Waypoints.Peek();
            Console.WriteLine($"Tick with current waypoint {currentWaypoint}");

            var distanceToTargetInM = Position.DistanceInMTo(currentWaypoint);
            if (distanceToTargetInM < STEP_SIZE)
            {
                Waypoints.Dequeue();

                // The current waypoint was the last one -> determine a whole new target
                if (Waypoints.Count == 0)
                {
                    Console.WriteLine($"Target {currentWaypoint} reached.");
                    DetermineNewWaypoints();
                }

                return;
            }

            // Console.WriteLine($"Distance to target: {Math.Round(distanceToTargetInM, 2)}m");

            var bearing = Position.GetBearing(currentWaypoint);

            // SharedEnvironment.Environment.Move(this, 45, 10);
            SharedEnvironment.Environment.MoveTowards(this, bearing, STEP_SIZE);

            Thread.Sleep(2);
        }

        private void DetermineNewWaypoints()
        {
            Target.NewPosition();
            // ResetPosition();

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

        private void ResetPosition()
        {
            Position = Position.CreateGeoPosition(0.1, 0.05);
        }
    }
}