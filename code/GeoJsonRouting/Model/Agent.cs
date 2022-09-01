using System.Diagnostics;
using GeoJsonRouting.Layer;
using Mars.Common;
using Mars.Components.Environments.Cartesian;
using Mars.Components.Layers;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Numerics;
using NetTopologySuite.Geometries;
using ServiceStack;
using Wavefront;
using Wavefront.Geometry;
using Position = Mars.Interfaces.Environments.Position;

namespace GeoJsonRouting.Model
{
    public class Agent : IPositionable, IAgent<VectorLayer>, IVisible, ICollidable
    {
        private static readonly double STEP_SIZE = 0.0001;

        [PropertyDescription] public UnregisterAgent UnregisterHandle { get; set; }
        [PropertyDescription] public ObstacleLayer ObstacleLayer { get; set; }

        public Position? Position { get; set; }
        public Guid ID { get; set; }

        private Position? _targetPosition;
        private Queue<Waypoint> _waypoints = new();

        public void Init(VectorLayer layer)
        {
        }

        public void Tick()
        {
            if (Position == null)
            {
                Position = ObstacleLayer.GetStart();
                _targetPosition = ObstacleLayer.GetTarget();

                DetermineNewWaypoints();

                SharedEnvironment.Environment.Insert(this, new Point(Position.ToCoordinate()));
            }

            var currentWaypoint = _waypoints.Peek();

            var distanceToTarget = Distance.Euclidean(currentWaypoint.Position.PositionArray, Position.PositionArray);
            if (distanceToTarget <= STEP_SIZE)
            {
                _waypoints.Dequeue();

                // The current waypoint was the last one -> we're done
                if (_waypoints.Count == 0)
                {
                    Console.WriteLine($"Target {currentWaypoint} reached.");
                    Kill();
                }

                return;
            }

            var bearing = Angle.GetBearing(Position, currentWaypoint.Position);
            Position = SharedEnvironment.Environment.Move(this, bearing, STEP_SIZE).Centroid.Coordinate.ToPosition();
        }

        private void DetermineNewWaypoints()
        {
            try
            {
                var obstacleGeometries = ObstacleLayer.Features.Map(f => new Obstacle(f.VectorStructured.Geometry));
                var watch = Stopwatch.StartNew();

                var wavefrontAlgorithm = new WavefrontAlgorithm(obstacleGeometries);
                Console.WriteLine($"Algorithm creation: {watch.ElapsedMilliseconds}ms");

                watch.Restart();
                _waypoints = new Queue<Waypoint>(wavefrontAlgorithm.Route(Position, _targetPosition));
                Console.WriteLine($"Routing duration: {watch.ElapsedMilliseconds}ms");
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

        public VisibilityKind? HandleExploration(IEntity collisionEntity)
        {
            return VisibilityKind.Opaque;
        }
        
        public CollisionKind? HandleCollision(IEntity collisionEntity)
        {
            return CollisionKind.Pass;
        }
    }
}