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
using NetTopologySuite.Geometries.Implementation;
using NetTopologySuite.IO;
using NetTopologySuite.IO.Converters;
using Newtonsoft.Json;
using Pipelines.Sockets.Unofficial;
using ServiceStack;
using Wavefront;
using Wavefront.Geometry;
using Feature = NetTopologySuite.Features.Feature;
using GeometryFactory = NetTopologySuite.Geometries.GeometryFactory;
using Position = Mars.Interfaces.Environments.Position;

namespace GeoJsonRouting.Model
{
    public class Agent : ICharacter, IAgent<VectorLayer>
    {
        private static readonly double STEP_SIZE = 0.00001;

        [PropertyDescription] public UnregisterAgent UnregisterHandle { get; set; }
        [PropertyDescription] public ObstacleLayer ObstacleLayer { get; set; }

        public Position? Position { get; set; }
        public Guid ID { get; set; } = Guid.NewGuid();
        public double Extent { get; set; } = 0.0002; // -> Umrechnung in lat/lon-differenz für Euklidische Distanz

        private Position? _targetPosition;
        private Queue<Waypoint> _waypoints = new();

        public void Init(VectorLayer layer)
        {
            Position = ObstacleLayer.GetRandomStart();
            _targetPosition = ObstacleLayer.GetRandomTarget();

            while (_targetPosition.Equals(Position))
            {
                _targetPosition = ObstacleLayer.GetRandomTarget();
            }

            DetermineNewWaypoints();

            SharedEnvironment.Environment.Insert(this, Position);
        }

        public void Tick()
        {
            try
            {
                TickInternal();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void TickInternal()
        {
            var currentWaypoint = _waypoints.Peek();

            var distanceToTarget = Distance.Euclidean(currentWaypoint.Position.PositionArray, Position.PositionArray);
            if (distanceToTarget <= STEP_SIZE)
            {
                _waypoints.Dequeue();

                // The current waypoint was the last one -> we're done
                if (_waypoints.Count == 0)
                {
                    Kill();
                }

                return;
            }

            var bearing = Angle.GetBearing(Position, currentWaypoint.Position);
            var oldPosition = (Position)Position.Clone();
            for (int i = 0; i < 4; i++)
            {
                var newPosition =
                    SharedEnvironment.Environment.Move(this, (bearing + i * 45) % 360, STEP_SIZE);
                var distanceInMTo = Distance.Euclidean(oldPosition.PositionArray, Position.PositionArray);
                Console.WriteLine($"{distanceInMTo}");
                if (!newPosition.Equals(oldPosition) && distanceInMTo >= 0.5 * STEP_SIZE)
                {
                    break;
                }
                else
                {
                    Console.WriteLine("Try another direction");
                }
            }
            // TODO Wenn nicht bewegt ggf. andere Position anlaufen
        }

        public CollisionKind? HandleCollision(ICharacter other)
        {
            var distanceInMTo = other.Position.DistanceInMTo(Position);
            return distanceInMTo <= 5 ? CollisionKind.Block : CollisionKind.Pass;
            // return CollisionKind.Pass;
        }

        private void Kill()
        {
            Console.WriteLine("Agent reached target");
            SharedEnvironment.Environment.Remove(this);
            UnregisterHandle.Invoke(ObstacleLayer, this);
        }

        private void DetermineNewWaypoints()
        {
            try
            {
                var watch = Stopwatch.StartNew();
                var routingResult = ObstacleLayer.WavefrontAlgorithm.Route(Position, _targetPosition);
                watch.Stop();
                Console.WriteLine($"Routing duration: {watch.ElapsedMilliseconds}ms");

                if (routingResult.OptimalRoute.IsEmpty())
                {
                    throw new Exception($"No route found from {Position} to {_targetPosition}");
                }

                _waypoints = new Queue<Waypoint>(routingResult.OptimalRoute);

                // TODO make these exports agent specific
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
            var features = RoutesToGeometryCollection(routes);

            var serializer = GeoJsonSerializer.Create();
            foreach (var converter in serializer.Converters
                         .Where(c => c is CoordinateConverter || c is GeometryConverter)
                         .ToList())
            {
                serializer.Converters.Remove(converter);
            }

            serializer.Converters.Add(new CoordinateZMConverter());
            serializer.Converters.Add(new GeometryZMConverter());

            await using var stringWriter = new StringWriter();
            using var jsonWriter = new JsonTextWriter(stringWriter);

            serializer.Serialize(jsonWriter, features);
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

        private FeatureCollection RoutesToGeometryCollection(List<List<Waypoint>> routes)
        {
            var featureCollection = new FeatureCollection();
            routes.Each((i, r) => featureCollection.Add(
                new Feature(RouteToLineString(r),
                    new AttributesTable(
                        new Dictionary<string, object>
                        {
                            { "id", i }
                        }
                    )
                )
            ));
            return featureCollection;
        }

        private LineString RouteToLineString(List<Waypoint> route)
        {
            var baseDate = new DateTime(2010, 1, 1);
            var unixZero = new DateTime(1970, 1, 1);
            var coordinateSequence = CoordinateArraySequenceFactory.Instance.Create(route.Count, 3, 1);
            route.Each((i, w) =>
            {
                coordinateSequence.SetX(i, w.Position.X);
                coordinateSequence.SetY(i, w.Position.Y);
                coordinateSequence.SetM(i, baseDate.AddSeconds(w.Time).Subtract(unixZero).TotalSeconds);
            });
            var geometryFactory = new GeometryFactory(CoordinateArraySequenceFactory.Instance);
            return new LineString(coordinateSequence, geometryFactory);
        }
    }
}