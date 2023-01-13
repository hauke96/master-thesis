using Mars.Common;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NetTopologySuite.IO;
using NetTopologySuite.IO.Converters;
using NetworkRoutingPlayground.Layer;
using Newtonsoft.Json;
using ServiceStack;
using Wavefront;
using Feature = NetTopologySuite.Features.Feature;
using Position = Mars.Interfaces.Environments.Position;

namespace NetworkRoutingPlayground.Model
{
    public class Agent : IAgent<NetworkLayer>, ISpatialGraphEntity
    {
        private static readonly double STEP_SIZE = 0.00001;

        [PropertyDescription] public UnregisterAgent UnregisterHandle { get; set; }
        [PropertyDescription] public NetworkLayer NetworkLayer { get; set; }

        public Position? Position { get; set; }
        public Guid ID { get; set; } = Guid.NewGuid();

        private Route _route;
        
        // Spatial entity stuff:
        public double Length => 0.0;
        public ISpatialEdge CurrentEdge { get; set; }
        public double PositionOnCurrentEdge { get; set; }
        public int LaneOnCurrentEdge { get; set; }
        public SpatialModalityType ModalityType => SpatialModalityType.Walking;
        public bool IsCollidingEntity => false;

        public void Init(NetworkLayer layer)
        {
            var start = new Position();
            var destination = new Position();

            var startNode = layer.Environment.NearestNode(start, SpatialModalityType.Walking);
            var destinationNode = layer.Environment.NearestNode(destination, SpatialModalityType.Walking);

            _route = layer.Environment.FindShortestRoute(startNode, destinationNode);

            NetworkLayer.Environment.Insert(this, startNode);
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
            if (_route.GoalReached)
            {
                Kill();
                return;
            }
            
            NetworkLayer.Environment.Move(this, _route, STEP_SIZE);
        }

        private void Kill()
        {
            Console.WriteLine("Agent reached target");
            NetworkLayer.Environment.Remove(this);
            UnregisterHandle.Invoke(NetworkLayer, this);
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