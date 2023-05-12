using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Mars.Common;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Numerics;
using NetTopologySuite.Geometries;
using HybridVisibilityGraphRouting;
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
        private IEnumerator<Position> _routeWaypoints;
        private Position NextRouteWaypoint => _routeWaypoints.Current;

        public void Init(HikerLayer layer)
        {
            _targetWaypoints = WaypointLayer.TrackPoints.GetEnumerator();
            _routeWaypoints = new List<Position>().GetEnumerator();

            _targetWaypoints.MoveNext();
            Position = NextTargetWaypoint.ToPosition();
            _targetWaypoints.MoveNext();

            _hikerLayer = layer;
            _hikerLayer.InitEnvironment(ObstacleLayer.Features, this);
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
            else if (NextRouteWaypoint.DistanceInMTo(Position) < StepSize * 2)
            {
                _routeWaypoints.MoveNext();

                if (NextRouteWaypoint == null)
                {
                    Log.I("Hiker reached end of route, choose next target and calculate new route.");
                    var previousWaypoint = NextTargetWaypoint;
                    _targetWaypoints.MoveNext();

                    if (NextTargetWaypoint == null)
                    {
                        Log.I("Hiker reached last waypoint. He will now die of exhaustion. Farewell dear hiker.");
                        _hikerLayer.Environment.Remove(this);
                        UnregisterHandle.Invoke(_hikerLayer, this);
                        Log.D("Hiker unregistered");
                        return;
                    }

                    CalculateRoute(previousWaypoint.ToPosition(), NextTargetWaypoint.ToPosition());
                }
            }

            var bearing = Position.GetBearing(NextRouteWaypoint);
            _hikerLayer.Environment.MoveTowards(this, bearing, StepSize);
        }

        private void CalculateRoute(Position from, Position to)
        {
            try
            {
                var routingResult = ObstacleLayer.HybridVisibilityGraph.ShortestPath(from, to);
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