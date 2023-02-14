using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Mars.Common;
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
using ServiceStack;
using Wavefront;
using Feature = NetTopologySuite.Features.Feature;
using Position = Mars.Interfaces.Environments.Position;

namespace HikerModel.Model
{
    public class Hiker : IAgent<HikerLayer>, IPositionable
    {
        [PropertyDescription] public WaypointLayer WaypointLayer { get; set; }
        [PropertyDescription] public ObstacleLayer ObstacleLayer { get; set; }
        [PropertyDescription] public UnregisterAgent UnregisterHandle { get; set; }

        private static readonly double StepSize = 250;

        public Position Position { get; set; }
        public Guid ID { get; set; }
        
        private HikerLayer _hikerLayer;

        // Locations the hiker wants to visit
        private IEnumerator<Coordinate> _targetWaypoints;
        private Coordinate NextTargetWaypoint => _targetWaypoints.Current;

        // Locations the routing engine determined
        private IEnumerator<Waypoint> _routeWaypoints;
        private Waypoint NextRouteWaypoint => _routeWaypoints.Current;

        private PerformanceMeasurement.RawResult _routingPerformanceResult;

        public void Init(HikerLayer layer)
        {
            _targetWaypoints = WaypointLayer.TrackPoints.GetEnumerator();
            _routeWaypoints = new List<Waypoint>().GetEnumerator();

            _targetWaypoints.MoveNext();
            Position = NextTargetWaypoint.ToPosition();
            _targetWaypoints.MoveNext();

            _hikerLayer = layer;
            _hikerLayer.InitEnvironment(ObstacleLayer.Features, this);

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
                throw;
            }
        }

        private void TickInternal()
        {
            if (NextTargetWaypoint == null)
            {
                Log.I("No next waypoint");
                return;
            }

            if (NextRouteWaypoint == null)
            {
                Log.I("Hiker has target but no route. Calculate route to next target.");
                CalculateRoute(Position, NextTargetWaypoint.ToPosition());
            }
            else if (NextRouteWaypoint.Position.DistanceInMTo(Position) < StepSize * 2)
            {
                _routeWaypoints.MoveNext();

                if (NextRouteWaypoint == null)
                {
                    Log.I("Hiker reached end of route, choose next target and calculate new route.");
                    _targetWaypoints.MoveNext();

                    if (NextTargetWaypoint == null)
                    {
                        Log.I("Hiker reached last waypoint. He will now die of exhaustion. Farewell dear hiker.");
                        _routingPerformanceResult.WriteToFile();
                        Log.D("Performance data written to file");
                        _hikerLayer.Environment.Remove(this);
                        UnregisterHandle.Invoke(_hikerLayer, this);
                        Log.D("Hiker unregistered");
                        return;
                    }

                    CalculateRoute(Position, NextTargetWaypoint.ToPosition());
                }
            }

            var bearing = Position.GetBearing(NextRouteWaypoint.Position);
            _hikerLayer.Environment.MoveTowards(this, bearing, StepSize);
        }

        private void CalculateRoute(Position from, Position to)
        {
            try
            {
                RoutingResult routingResult = null;

                var performanceMeasurementResult = PerformanceMeasurement.ForFunction(
                    () => { routingResult = ObstacleLayer.WavefrontAlgorithm.Route(from, to); },
                    "CalculateRoute",
                    PerformanceMeasurement.DEFAULT_ITERATION_COUNT*10,
                    PerformanceMeasurement.DEFAULT_WARMUP_COUNT*3);
                performanceMeasurementResult.Print();

                // Collect data for routing requests for each such request. Requests can be differently long and complex
                // so it's interesting to put the result into a perspective (e.g. relative to distance between "from"
                // and "to").
                const string numberFormat = "0.###";
                var invariantCulture = CultureInfo.InvariantCulture;
                var distanceFromTo = Distance.Haversine(from.PositionArray, to.PositionArray);
                var averageTimeString = performanceMeasurementResult.AverageTime.ToString(numberFormat, invariantCulture);
                _routingPerformanceResult.AddRow(new Dictionary<string, string>
                {
                    { "total_vertices", PerformanceMeasurement.TOTAL_VERTICES.ToString(numberFormat, invariantCulture) },
                    { "total_vertices_after_preprocessing", PerformanceMeasurement.TOTAL_VERTICES_AFTER_PREPROCESSING.ToString(numberFormat, invariantCulture) },
                    { "distance", distanceFromTo.ToString(numberFormat, invariantCulture) },
                    { "route_length", routingResult.OptimalRouteLength.ToString(numberFormat, invariantCulture) },
                    { "avg_time", averageTimeString },
                    { "iteration_time", averageTimeString },
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