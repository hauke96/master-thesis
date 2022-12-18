using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mars.Common;
using Mars.Common.Core;
using Mars.Common.Data;
using ServiceStack;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Numerics;
using Mars.Numerics.Distances;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NetTopologySuite.IO;
using NetTopologySuite.IO.Converters;
using Newtonsoft.Json;
using Wavefront;
using CollectionExtensions = ServiceStack.CollectionExtensions;
using Feature = NetTopologySuite.Features.Feature;
using Position = Mars.Interfaces.Environments.Position;

namespace HikerModel.Model
{
    public class Hiker : IAgent<HikerLayer>, IPositionable
    {
        [PropertyDescription] public WaypointLayer WaypointLayer { get; set; }
        [PropertyDescription] public ObstacleLayer ObstacleLayer { get; set; }
        [PropertyDescription] public UnregisterAgent UnregisterHandle { get; set; }

        public static double StepSize = 250;

        public HikerLayer HikerLayer { get; private set; }
        public Position Position { get; set; }
        public Guid ID { get; set; }

        // Locations the hiker wants to visit
        private IEnumerator<Coordinate> _targetWaypoints;
        private Coordinate _nextTargetWaypoint => _targetWaypoints.Current;

        // Locations the routing engine determined
        private IEnumerator<Waypoint> _routeWaypoints;
        private Waypoint _nextRouteWaypoint => _routeWaypoints.Current;

        public PerformanceMeasurement.RawResult _routingPerformanceResult;

        public void Init(HikerLayer layer)
        {
            _targetWaypoints = WaypointLayer.TrackPoints.GetEnumerator();
            _routeWaypoints = new List<Waypoint>().GetEnumerator();

            _targetWaypoints.MoveNext();
            Position = _nextTargetWaypoint.ToPosition();
            _targetWaypoints.MoveNext();

            HikerLayer = layer;
            layer.Environment.Insert(this);

            _routingPerformanceResult = new PerformanceMeasurement.RawResult("Routing");
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
            if (_nextTargetWaypoint == null)
            {
                Console.WriteLine("No next waypoint");
                return;
            }

            if (_nextRouteWaypoint == null)
            {
                Console.WriteLine("Hiker has target but no route. Calculate route to next target.");
                CalculateRoute(Position, _nextTargetWaypoint.ToPosition());
            }
            else if (_nextRouteWaypoint.Position.DistanceInMTo(Position) < StepSize * 2)
            {
                _routeWaypoints.MoveNext();

                if (_nextRouteWaypoint == null)
                {
                    Console.WriteLine("Hiker reached end of route, choose next target and calculate new route.");
                    _targetWaypoints.MoveNext();

                    if (_nextTargetWaypoint == null)
                    {
                        Console.WriteLine(
                            "Hiker reached last waypoint. He will now die of exhaustion. Farewell dear hiker.");
                        _routingPerformanceResult.WriteToFile();
                        HikerLayer.Environment.Remove(this);
                        UnregisterHandle.Invoke(HikerLayer, this);
                        return;
                    }

                    CalculateRoute(Position, _nextTargetWaypoint.ToPosition());
                }
            }

            var bearing = Position.GetBearing(_nextRouteWaypoint.Position);
            HikerLayer.Environment.MoveTowards(this, bearing, StepSize);
        }

        private void CalculateRoute(Position from, Position to)
        {
            try
            {
                RoutingResult routingResult = null;

                PerformanceMeasurement.IS_ACTIVE = true;

                var performanceMeasurementResult = PerformanceMeasurement.ForFunction(
                    () => { routingResult = ObstacleLayer.WavefrontAlgorithm.Route(from, to); },
                    "CalculateRoute");
                performanceMeasurementResult.Print();

                // Collect data for routing requests for each such request. Requests can be differently long and complex
                // so it's interesting to put the result into a perspective (e.g. relative to distance between "from"
                // and "to").
                const string numberFormat = "0.###";
                var invariantCulture = System.Globalization.CultureInfo.InvariantCulture;
                var distanceFromTo = Distance.Haversine(from.PositionArray, to.PositionArray);
                _routingPerformanceResult.AddRow(new Dictionary<string, string>
                {
                    { "distance", distanceFromTo.ToString(numberFormat, invariantCulture) },
                    { "route_length", routingResult.OptimalRouteLength.ToString(numberFormat, invariantCulture) },
                    { "avg_time", performanceMeasurementResult.AverageTime.ToString(numberFormat, invariantCulture) },
                    { "min_time", performanceMeasurementResult.MinTime.ToString(numberFormat, invariantCulture) },
                    { "max_time", performanceMeasurementResult.MaxTime.ToString(numberFormat, invariantCulture) },
                    { "total_time", performanceMeasurementResult.TotalTime.ToString(numberFormat, invariantCulture) },
                    {
                        "from",
                        from.X.ToString(numberFormat, invariantCulture) + " " +
                        from.Y.ToString(numberFormat, invariantCulture)
                    },
                    {
                        "to",
                        to.X.ToString(numberFormat, invariantCulture) + " " +
                        to.Y.ToString(numberFormat, invariantCulture)
                    }
                });

                if (routingResult.OptimalRoute.Count == 0)
                {
                    throw new Exception($"No route found from {from} to {to}");
                }

                WriteRoutesToFile(routingResult.AllRoutes);

                _routeWaypoints = routingResult.OptimalRoute.GetEnumerator();
                _routeWaypoints.MoveNext();
                
                PerformanceMeasurement.IS_ACTIVE = false;
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