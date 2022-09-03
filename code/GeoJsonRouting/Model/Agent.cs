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
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json;
using ServiceStack;
using Wavefront;
using Wavefront.Geometry;
using Feature = NetTopologySuite.Features.Feature;
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
                var routingResult = wavefrontAlgorithm.Route(Position, _targetPosition);
                Console.WriteLine($"Routing duration: {watch.ElapsedMilliseconds}ms");
                
                _waypoints = new Queue<Waypoint>(routingResult.OptimalRoute);

                WriteRoutesToFile(routingResult.AllRoutes);
                WriteVisitedPositionsToFile(routingResult.AllRoutes);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private async void WriteRoutesToFile(List<List<Waypoint>> routes)
        {
            var geometry = RoutesToGeometryCollection(routes);

            var serializer = GeoJsonSerializer.Create();
            await using var stringWriter = new StringWriter();
            using var jsonWriter = new JsonTextWriter(stringWriter);

            serializer.Serialize(jsonWriter, geometry);
            var geoJson = stringWriter.ToString();

            await File.WriteAllTextAsync("agent-routes.geojson", geoJson);
        }

        private async void WriteVisitedPositionsToFile(List<List<Waypoint>> routes)
        {
            var waypoints = routes.SelectMany(l => l) // Flatten list of lists
                .GroupBy(w => w.Position) // Waypoint may have been visited multiple times
                .Map(g => g.OrderBy(w => w.Order).First()) // Get the first visited waypoint
                .ToList();
            var features = new FeatureCollection();
            waypoints.Each(w =>
            {
                var pointGeometry = (Geometry)new Point(w.Position.ToCoordinate());
                var attributes = new AttributesTable
                {
                    { "order", w.Order },
                    { "time", w.Time }
                };
                features.Add(new Feature(pointGeometry, attributes));
            });

            var serializer = GeoJsonSerializer.Create();
            await using var stringWriter = new StringWriter();
            using var jsonWriter = new JsonTextWriter(stringWriter);

            serializer.Serialize(jsonWriter, features);
            var geoJson = stringWriter.ToString();

            await File.WriteAllTextAsync("agent-points.geojson", geoJson);
        }

        private GeometryCollection RoutesToGeometryCollection(List<List<Waypoint>> routes)
        {
            var geometries = routes.Map(r => (Geometry)RouteToLineString(r));
            return new GeometryCollection(geometries.ToArray());
        }

        private LineString RouteToLineString(List<Waypoint> route)
        {
            return new LineString(route.Map(w => w.Position.ToCoordinate()).ToArray());
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