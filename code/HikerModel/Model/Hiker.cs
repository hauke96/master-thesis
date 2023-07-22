using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HybridVisibilityGraphRouting;
using HybridVisibilityGraphRouting.IO;
using Mars.Common;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Numerics;
using NetTopologySuite.Geometries;
using Position = Mars.Interfaces.Environments.Position;

namespace HikerModel.Model
{
    public class Hiker : IAgent<HikerLayer>, IPositionable
    {
        [PropertyDescription] public WaypointLayer WaypointLayer { get; set; }
        [PropertyDescription] public ObstacleLayer ObstacleLayer { get; set; }
        [PropertyDescription] public UnregisterAgent UnregisterHandle { get; set; }

        public Position Position { get; set; }
        public Guid ID { get; set; }

        private HikerLayer _hikerLayer;
        private double _stepSize = 1;

        // Locations the hiker wants to visit
        private IEnumerator<Coordinate> _destinationWaypoints;
        private Coordinate NextDestinationWaypoint => _destinationWaypoints.Current;

        // Locations the routing engine determined
        private IEnumerator<Position> _routeWaypoints;
        private Position NextRouteWaypoint => _routeWaypoints.Current;

        private PerformanceMeasurement.RawResult _routingPerformanceResult;

        public void Init(HikerLayer layer)
        {
            _destinationWaypoints = WaypointLayer.TrackPoints.GetEnumerator();
            _routeWaypoints = new List<Position>().GetEnumerator();

            _destinationWaypoints.MoveNext();
            Position = NextDestinationWaypoint.ToPosition();
            _destinationWaypoints.MoveNext();

            _hikerLayer = layer;
            _hikerLayer.InitEnvironment(ObstacleLayer.Features, this);

            _stepSize = new Position(_hikerLayer.BBOX.MinX, _hikerLayer.BBOX.MinY).DistanceInMTo(
                new Position(_hikerLayer.BBOX.MaxX, _hikerLayer.BBOX.MaxY)) / 2500;

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
            if (NextDestinationWaypoint == null)
            {
                Log.I("No next waypoint");
                return;
            }

            if (NextRouteWaypoint == null)
            {
                Log.I("Hiker knows its destination but no route to it. Calculate route to next destination.");
                CalculateRoute(Position, NextDestinationWaypoint.ToPosition());
            }
            else if (NextRouteWaypoint.DistanceInMTo(Position) < _stepSize * 2)
            {
                _routeWaypoints.MoveNext();

                if (NextRouteWaypoint == null)
                {
                    Log.I("Hiker reached end of route, choose next destination and calculate new route.");
                    var previousWaypoint = NextDestinationWaypoint;
                    _destinationWaypoints.MoveNext();

                    if (NextDestinationWaypoint == null)
                    {
                        Log.I("Hiker reached last waypoint. He will now die of exhaustion. Farewell dear hiker.");
                        _routingPerformanceResult.WriteToFile();
                        Log.D("Performance data written to file");
                        _hikerLayer.Environment.Remove(this);
                        UnregisterHandle.Invoke(_hikerLayer, this);
                        Log.D("Hiker unregistered");
                        return;
                    }

                    CalculateRoute(previousWaypoint.ToPosition(), NextDestinationWaypoint.ToPosition());
                }
            }

            var bearing = Position.GetBearing(NextRouteWaypoint);
            _hikerLayer.Environment.MoveTowards(this, bearing, _stepSize);
        }

        private void CalculateRoute(Position from, Position to)
        {
            try
            {
                List<Position> routingResult = null;

                Log.I($"Calculate route from {from} to {to}");

                var performanceMeasurementResult = PerformanceMeasurement.NewMeasurementForFunction(
                    () => { routingResult = ObstacleLayer.HybridVisibilityGraph.WeightedShortestPath(from, to); },
                    "CalculateRoute", 1, 0);
                performanceMeasurementResult.Print();

                if (PerformanceMeasurement.IsActive)
                {
                    // Collect data for routing requests for each such request. Requests can be differently long and complex
                    // so it's interesting to put the result into a perspective (e.g. relative to distance between "from"
                    // and "to").
                    const string numberFormat = "0.###";
                    var invariantCulture = CultureInfo.InvariantCulture;
                    var distanceFromTo = Distance.Haversine(from.PositionArray, to.PositionArray);
                    var averageTimeString = performanceMeasurementResult.Iterations.Average()
                        .ToString(numberFormat, invariantCulture);
                    var minTimeString = performanceMeasurementResult.Iterations.Min()
                        .ToString(numberFormat, invariantCulture);
                    var maxTimeString = performanceMeasurementResult.Iterations.Max()
                        .ToString(numberFormat, invariantCulture);
                    var addPositionToGraphAvgTime = performanceMeasurementResult.Values["add_positions_to_graph_time"]
                        .Cast<double>().Average()
                        .ToString(numberFormat, invariantCulture);
                    var astarAvgTime = performanceMeasurementResult.Values["astar_time"].Cast<double>().Average()
                        .ToString(numberFormat, invariantCulture);
                    var restoreAvgTime = performanceMeasurementResult.Values["restore_graph"].Cast<double>().Average()
                        .ToString(numberFormat, invariantCulture);

                    _routingPerformanceResult.AddRow(new Dictionary<string, object>
                    {
                        // Geometry statistics
                        {
                            "obstacles_input",
                            ObstacleLayer.GraphGenerationResult.ObstacleCountInput.ToString(numberFormat,
                                invariantCulture)
                        },
                        {
                            "obstacles_after_unwrapping",
                            ObstacleLayer.GraphGenerationResult.ObstacleCountAfterUnwrapping.ToString(numberFormat,
                                invariantCulture)
                        },

                        {
                            "obstacle_vertices_input",
                            ObstacleLayer.GraphGenerationResult.ObstacleVertices.ToString(numberFormat,
                                invariantCulture)
                        },
                        {
                            "obstacle_vertices_after_unwrapping",
                            ObstacleLayer.GraphGenerationResult.ObstacleVerticesAfterPreprocessing.ToString(
                                numberFormat,
                                invariantCulture)
                        },

                        {
                            "road_vertices_input",
                            ObstacleLayer.GraphGenerationResult.RoadVertices.ToString(numberFormat, invariantCulture)
                        },
                        {
                            "road_vertices_after_merging",
                            ObstacleLayer.GraphGenerationResult.RoadVerticesAfterMerging.ToString(numberFormat,
                                invariantCulture)
                        },
                        {
                            "road_edges_input",
                            ObstacleLayer.GraphGenerationResult.RoadEdges.ToString(numberFormat, invariantCulture)
                        },
                        {
                            "road_edges_after_merging",
                            ObstacleLayer.GraphGenerationResult.RoadEdgesAfterMerging.ToString(numberFormat,
                                invariantCulture)
                        },

                        {
                            "other_vertices_input",
                            ObstacleLayer.GraphGenerationResult.AllInputVertices.ToString(numberFormat,
                                invariantCulture)
                        },

                        {
                            "visibility_edges_before_merging",
                            ObstacleLayer.GraphGenerationResult.VisibilityEdgesBeforeMerging.ToString(numberFormat,
                                invariantCulture)
                        },
                        {
                            "visibility_edges_after_merging",
                            ObstacleLayer.GraphGenerationResult.VisibilityEdgesAfterMerging.ToString(numberFormat,
                                invariantCulture)
                        },

                        // Routing statistics

                        {
                            "from",
                            from.X.ToString(numberFormat, invariantCulture) + " " +
                            from.Y.ToString(numberFormat, invariantCulture)
                        },
                        {
                            "to",
                            to.X.ToString(numberFormat, invariantCulture) + " " +
                            to.Y.ToString(numberFormat, invariantCulture)
                        },
                        { "distance_beeline", distanceFromTo.ToString(numberFormat, invariantCulture) },
                        {
                            "distance_route",
                            routingResult
                                .Select((position, i) => i == 0 ? 0 : position.DistanceInMTo(routingResult[i - 1]))
                                .Sum()
                                .ToString(numberFormat, invariantCulture)
                        },

                        { "min_time", minTimeString },
                        { "max_time", maxTimeString },
                        { "avg_time", averageTimeString },
                        {
                            "total_time_of_all_iterations",
                            performanceMeasurementResult.TotalTime.ToString(numberFormat, invariantCulture)
                        },
                        { "astar_avg_time", astarAvgTime },
                        { "add_positions_to_graph_avg_time", addPositionToGraphAvgTime },
                        { "restore_avg_time", restoreAvgTime },

                        {
                            "min_mem_before",
                            performanceMeasurementResult.MinMemoryBefore.ToString(numberFormat, invariantCulture)
                        },
                        {
                            "max_mem_before",
                            performanceMeasurementResult.MaxMemoryBefore.ToString(numberFormat, invariantCulture)
                        },
                        {
                            "avg_mem_before",
                            performanceMeasurementResult.AvgMemoryBefore.ToString(numberFormat, invariantCulture)
                        },
                        {
                            "min_mem_after",
                            performanceMeasurementResult.MinMemoryAfter.ToString(numberFormat, invariantCulture)
                        },
                        {
                            "max_mem_after",
                            performanceMeasurementResult.MaxMemoryAfter.ToString(numberFormat, invariantCulture)
                        },
                        {
                            "avg_mem_after",
                            performanceMeasurementResult.AvgMemoryAfter.ToString(numberFormat, invariantCulture)
                        },
                    });
                }

                if (routingResult.Count == 0)
                {
                    throw new Exception($"No route found from {from} to {to}");
                }

                _routeWaypoints = routingResult.GetEnumerator();
                _routeWaypoints.MoveNext();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}