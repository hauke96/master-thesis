using Mars.Common;
using Mars.Components.Layers;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using NetTopologySuite.Geometries;
using RoutingWithLineObstacle.Layer;
using RoutingWithLineObstacle.Wavefront;
using ServiceStack;
using Position = Mars.Interfaces.Environments.Position;

namespace RoutingWithLineObstacle.Model
{
    // TODO Find a better name than just "Agent".
    public class Agent : IPositionable, IAgent<VectorLayer>
    {
        private static readonly int STEP_SIZE = 1;

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

            Thread.Sleep(10);
        }

        private void DetermineNewWaypoints()
        {
            Target.NewPosition();
            ResetPosition();

            var obstacleGeometries = ObstacleLayer.Features.Map(f => f.VectorStructured.Geometry);
            var wavefrontAlgorithm = new WavefrontAlgorithm(obstacleGeometries);
            Waypoints = new Queue<Position>(wavefrontAlgorithm.route(Position, Target.Position));

            // TODO use this in algorithm
            // var lineStringToTarget = new LineString(new[]
            //     { new Coordinate(Position.X, Position.Y), new Coordinate(Target.Position.X, Target.Position.Y) });
            //
            // var intersectsWithObstacle = ObstacleLayer
            //     .Explore(Position.PositionArray, -1,
            //         feature => feature.VectorStructured.Geometry.Intersects(lineStringToTarget))
            //     .Any();
            //
            // if (intersectsWithObstacle)
            // {
            //     // TODO Do not simply take the nearest vertex but actually perform a simplified wave-algorithm by determining the distance from each vertex to the target
            //     var nearest = ObstacleLayer.NearestVertex(Position);
            //     Waypoints.Enqueue(nearest);
            // }

            // Waypoints.Enqueue(Target.Position);
        }

        private void ResetPosition()
        {
            Position = Position.CreateGeoPosition(0.001, 0.0005);
        }
    }
}