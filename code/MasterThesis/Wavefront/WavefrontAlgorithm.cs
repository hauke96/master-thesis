using Mars.Common;
using Mars.Common.Core.Collections;
using Mars.Interfaces.Environments;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Predicate;
using NetTopologySuite.Operation.Valid;
using RoutingWithLineObstacle.Wavefront.Events;
using ServiceStack;
using Wavefront.Geometry;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront
{
    public class WavefrontAlgorithm
    {
        private readonly List<NetTopologySuite.Geometries.Geometry> Obstacles;
        private readonly List<Vertex> Vertices;
        private readonly Dictionary<Position, Position?> PositionToPredecessor;

        private List<Wavefront> Wavefronts;

        public WavefrontAlgorithm(List<NetTopologySuite.Geometries.Geometry> obstacles)
        {
            Obstacles = obstacles;
            Vertices = new List<Vertex>();
            PositionToPredecessor = new Dictionary<Position, Position?>();

            Obstacles.Each(obstacle =>
            {
                var positions = obstacle.Coordinates.Map(c => new Vertex(c, obstacle));
                Vertices.AddRange(positions);
            });
            Wavefronts = new List<Wavefront>();
        }

        public List<Position> route(Position source, Position target)
        {
            Vertices.Add(new Vertex(target));
            PositionToPredecessor[source] = null;

            var initialWavefront = Wavefront.newIfValid(0, 360, new Vertex(source), Vertices, 0);
            if (initialWavefront == null)
            {
                return new List<Position>();
            }

            Wavefronts.Add(initialWavefront);

            while (!PositionToPredecessor.ContainsKey(target))
            {
                // TODO Use sorted queue (prioroty queue?)
                Wavefronts.Sort((w1, w2) => w1.DistanceToNextVertex() - w2.DistanceToNextVertex() > 0 ? 1 : -1);
                var wavefront = Wavefronts[0];
                var currentVertex = wavefront.GetNextVertex();

                if (currentVertex == null)
                {
                    // This wavefront doesn't have any events ahead, to we can remove it. 
                    Wavefronts.Remove(wavefront);
                    continue;
                }

                // We reached the target -> exit loop
                // if (Equals(currentVertex.Position, target))
                // {
                //     PositionToPredecessor[target] = wavefront.RootVertex.Position;
                //     break;
                // }

                if (!IsEventValid(wavefront.RootVertex.Position, currentVertex.Position)
                    || PositionToPredecessor.ContainsKey(currentVertex.Position))
                {
                    wavefront.IgnoreVertex(currentVertex);
                    continue;
                }

                wavefront.RemoveNextVertex();

                // TODO Need the event here? Probably want to keep if for more complex situations later?
                var currentEvent = new VertexEvent(currentVertex, wavefront.RootVertex.Position,
                    wavefront.DistanceToRootFromSource);

                PositionToPredecessor[currentEvent.Position] = currentEvent.Root;

                var rightNeighbor = currentVertex.RightNeighbor;
                var angleWavefrontRight = HandleNeighborVertex(wavefront, rightNeighbor, currentVertex);

                var leftNeighbor = currentVertex.LeftNeighbor;
                var angleWavefrontLeft = HandleNeighborVertex(wavefront, leftNeighbor, currentVertex);

                // We need to create a new wavefront starting at the end of the line we found
                if (!(rightNeighbor == null && leftNeighbor == null) && (rightNeighbor == null || leftNeighbor == null))
                {
                    var neighbor = rightNeighbor ?? leftNeighbor;
                    var angleCurrentVertexToNeighbor =
                        currentVertex.Position.GetBearing(new Position(neighbor.X, neighbor.Y));
                    var angleWavefrontRootToCurrentVertex =
                        wavefront.RootVertex.Position.GetBearing(currentVertex.Position);

                    var fromAngle = angleWavefrontRootToCurrentVertex;
                    var toAngle = angleCurrentVertexToNeighbor;
                    var distanceFromSourceToCurrentVertex = wavefront.DistanceToRootFromSource +
                                                            wavefront.RootVertex.Position.DistanceInMTo(
                                                                currentVertex.Position);

                    AdjustWavefront(wavefront.RelevantVertices, currentVertex, distanceFromSourceToCurrentVertex,
                        fromAngle, toAngle);

                    // We exceeded the 0° line -> create two separate wavefronts fromAngle->360 and 0->toAngle
                    // if (fromAngle > toAngle)
                    // {
                    //     AddWavefrontIfValid(wavefront.RelevantVertices, distanceFromSourceToCurrentVertex,
                    //         currentVertex, fromAngle, 360);
                    //     AddWavefrontIfValid(wavefront.RelevantVertices, distanceFromSourceToCurrentVertex,
                    //         currentVertex, 0, toAngle);
                    // }
                    // else
                    // {
                    //     AddWavefrontIfValid(wavefront.RelevantVertices, distanceFromSourceToCurrentVertex,
                    //         currentVertex, fromAngle, toAngle);
                    // }


                    // var newWavefront = AddWavefrontIfValid(wavefront.RelevantVertices,
                    //     distanceFromWavefrontRootToCurrentVertex, currentVertex,
                    //     angleWavefrontRootToCurrentVertex, angleCurrentVertexToNeighbor);
                    // if (newWavefront != null)
                    // {
                    //     AdjustWavefront(newWavefront, wavefront.FromAngle, wavefront.ToAngle);
                    // }
                }

                AdjustWavefront(wavefront.RelevantVertices, wavefront.RootVertex, wavefront.DistanceToRootFromSource,
                    angleWavefrontRight, angleWavefrontLeft);
            }

            var waypoints = new List<Position>();
            var nextPosition = target;

            while (nextPosition != null)
            {
                waypoints.Add(nextPosition);
                nextPosition = PositionToPredecessor.ContainsKey(nextPosition)
                    ? PositionToPredecessor[nextPosition]
                    : null;
            }

            waypoints.Reverse();
            return waypoints;
        }

        /// <summary>
        /// Handles the given neighbor vertex of currentVertex. This means a Wavefront.of might be spawned if the
        /// neighbor is not visible from the root vertex of the given wavefront.
        /// </summary>
        /// <returns>
        /// The angle that should be used to adjust the given wavefront. This is basically the right/left edge
        /// of the "shadow" casted by the wavefront and the line-part between left and right neighbor of the event.
        /// </returns>
        private double HandleNeighborVertex(Wavefront wavefront, Coordinate? neighbor, Vertex currentVertex)
        {
            if (neighbor == null)
            {
                return wavefront.RootVertex.Position.GetBearing(currentVertex.Position);
            }

            var neighborVisible = !TrajectoryCollidesWithObstacle(wavefront.RootVertex.X, wavefront.RootVertex.Y,
                neighbor.X, neighbor.Y);
            var angleWavefrontRootToNeighbor =
                wavefront.RootVertex.Position.GetBearing(Position.CreateGeoPosition(neighbor.X, neighbor.Y));
            var angleNewWavefrontEdge = angleWavefrontRootToNeighbor;
            if (!neighborVisible)
            {
                // The neighbor is not visible -> The angle wavefront based on the wavefront.RootVertex only
                // goes to the angle between wavefront root and the current vertex
                angleNewWavefrontEdge = wavefront.RootVertex.Position.GetBearing(currentVertex.Position);
                var angleEventRootToNeighbor =
                    currentVertex.Position.GetBearing(new Position(neighbor.X, neighbor.Y));

                var fromAngle = Math.Min(angleEventRootToNeighbor, angleNewWavefrontEdge);
                var toAngle = Math.Max(angleEventRootToNeighbor, angleNewWavefrontEdge);

                AddWavefrontIfValid(wavefront.RelevantVertices, wavefront.DistanceToRootFromSource, currentVertex,
                    fromAngle, toAngle);
            }

            return angleNewWavefrontEdge;
        }

        private Wavefront? AddWavefrontIfValid(List<Vertex> relevantVertices, double distanceFromSourceToVertex,
            Vertex rootVertex, double fromAngle, double toAngle)
        {
            const double floatRoundingTolerance = 0.0001;
            if (Math.Abs(fromAngle % 360 - toAngle % 360) < floatRoundingTolerance)
            {
                return null;
            }

            toAngle = toAngle == 0 ? 360 : toAngle;
            var newWavefront = Wavefront.newIfValid(fromAngle, toAngle, rootVertex, relevantVertices,
                distanceFromSourceToVertex);
            if (newWavefront != null)
            {
                Wavefronts.Add(newWavefront);
            }

            return newWavefront;
        }

        private void AdjustWavefront(List<Vertex> vertices, Vertex root, double distanceToRootFromSource,
            double fromAngle, double toAngle, Wavefront? wavefront = null)
        {
            /*
                 *              r
                 *  * -------- *
                 *   \         |
                 *    \        |        X source
                 *     * ----- *
                 *    l         v
                 *
                 * Vertices r and v are visible from the source X. Let r be the right neighbor and l be the left.
                 * 
                 * This means "leftAngle" is the same as the angle from X to v and right angle is the angle from
                 * X to r. Let's say leftAngle is 250 and rightAngle is 300. The angleDiff is then
                 *
                 *     (300 - 250 + 360) % 360
                 *   = (50 + 360) % 360
                 *   = 410 % 360
                 *   = 50
                 *
                 * So angleDiff < 180 and this means the angle of the wavefront rooted in X should now be between
                 * rightAngle and leftAngle, so the imaginary arc should go from r to l (clockwise). 
                 */

            // When angleDiff > 180 then the wanted range of the resulting wavefront is from left neighbor to
            // the right neighbor.
            var angleDiff = (fromAngle - toAngle + 360) % 360;

            // When angleDiff < 180 then the situation is switched and the wanted area for the wavefront is from
            // the right neighbor to the left neighbor.
            if (angleDiff < 180)
            {
                (fromAngle, toAngle) = (toAngle, fromAngle);
            }

            /*
             * If the interesting area exceeds the 0° border (e.g. goes from 300° via 0° to 40°), then we remove the
             * old wavefront and create two new ones. One from 300° to 360° and one from 0° to 40°. This simply
             * makes range checks easier and has no further reason.
             */
            if (fromAngle > toAngle)
            {
                if (wavefront != null)
                {
                    Wavefronts.Remove(wavefront);
                }

                AddWavefrontIfValid(vertices, distanceToRootFromSource, root, fromAngle, 360);
                AddWavefrontIfValid(vertices, distanceToRootFromSource, root, 0, toAngle);
            }
            else
            {
                AddWavefrontIfValid(vertices, distanceToRootFromSource, root, fromAngle, toAngle);
            }
        }

        private List<VertexEvent> initializeVertexEventsFor(Position eventRoot, double distanceToRootFromSource)
        {
            var vertexEvents = getSortedVerticesFor(eventRoot)
                .Map(vertexPosition =>
                    new VertexEvent(vertexPosition, eventRoot, distanceToRootFromSource))
                .FindAll(isEventValid);
            vertexEvents.Sort();
            return vertexEvents;
        }

        private bool isEventValid(VertexEvent rawEvent)
        {
            var eventRootPosition = rawEvent.Root;
            var eventPosition = rawEvent.Position;

            return IsEventValid(eventRootPosition, eventPosition);
        }

        private bool IsEventValid(Position eventRootPosition, Position eventPosition)
        {
            return !TrajectoryCollidesWithObstacle(eventRootPosition, eventPosition);
        }

        private bool TrajectoryCollidesWithObstacle(Position startPosition, Position endPosition)
        {
            return TrajectoryCollidesWithObstacle(startPosition.X, startPosition.Y, endPosition.X, endPosition.Y);
        }

        private bool TrajectoryCollidesWithObstacle(double startPositionX, double startPositionY, double endPositionX,
            double endPositionY)
        {
            var lineStringToEvent = new LineString(new[]
            {
                new Coordinate(startPositionX, startPositionY),
                new Coordinate(endPositionX, endPositionY)
            });

            return Obstacles.Any(obstacle => obstacle.Crosses(lineStringToEvent));
        }

        private List<Vertex> getSortedVerticesFor(Position position)
        {
            return Vertices;
        }
    }
}