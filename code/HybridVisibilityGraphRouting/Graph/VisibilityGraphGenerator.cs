using System.Diagnostics;
using HybridVisibilityGraphRouting.Geometry;
using HybridVisibilityGraphRouting.Index;
using HybridVisibilityGraphRouting.IO;
using Mars.Common;
using Mars.Common.Collections;
using Mars.Common.Core.Collections;
using NetTopologySuite.Geometries;
using ServiceStack;
using Position = Mars.Interfaces.Environments.Position;

namespace HybridVisibilityGraphRouting;

public static class VisibilityGraphGenerator
{
    /// <summary>
    /// Calculates the neighbor relationship for each vertex v in the given obstacles. The term "neighbor" here means
    /// neighboring positions on adjacent obstacles but not across open spaces. This means there is an edge on at least
    /// one of the given obstacles from any vertex v to its neighboring positions.
    ///
    /// This neighbor relation is stored on the vertices of the obstacles. They therefore need to share the vertex
    /// instances for identical vertices.
    /// </summary>
    public static void AddObstacleNeighborsForObstacles(
        IList<Obstacle> obstacles,
        Dictionary<Coordinate, List<Obstacle>> coordinateToObstacles,
        bool debugModeActive = false)
    {
        // A function that determines if any obstacles is between the two given coordinates.
        bool IsCoordinateHidden(Coordinate coordinate, Coordinate otherCoordinate, Obstacle obstacleOfCoordinate) =>
            obstacles.Any(o =>
                // There are two cases to consider of which one has to be true when checking if there's anything between
                // "coordinate" and "otherCoordinate":
                //
                // 1. case:
                // We have a different obstacle than "obstacleOfCoordinate" which also has the line segment
                // between the two coordinates, which means "o" and "obstacleOfCoordinate" touch each other on
                // this segment. It's important that both are closed, because if one is open, the two coordinates
                // are reachable and therefore might actually see each other.
                !obstacleOfCoordinate.Equals(o) && o.IsClosed
                                                && obstacleOfCoordinate.IsClosed
                                                && o.HasLineSegment(coordinate, otherCoordinate)
                                                && obstacleOfCoordinate.HasLineSegment(coordinate, otherCoordinate)
                ||
                // 2. case:
                // We have something else, so we check for true line intersection.
                !obstacleOfCoordinate.Equals(o) &&
                o.IntersectsWithLine(coordinate, otherCoordinate, coordinateToObstacles)
            );

        obstacles.Each(obstacle => { AddObstacleNeighborsForObstacle(obstacle, IsCoordinateHidden); });

        if (debugModeActive)
        {
            var positionToObstacleNeighbors = new Dictionary<Coordinate, HashSet<Position>>();
            obstacles.Map(o => o.Vertices)
                .SelectMany(x => x)
                .Each(v => positionToObstacleNeighbors[v.Coordinate] = v.ObstacleNeighbors.ToSet());
            Exporter.WriteVertexNeighborsToFile(positionToObstacleNeighbors);
        }
    }

    /// <summary>
    /// Adds the obstacle neighbors (neighbors on this and touching obstacles but not across open spaces) to the given
    /// map.
    /// </summary>
    /// <param name="obstacle">The obstacle of which the neighbor relation should be determined.</param>
    /// <param name="isCoordinateHidden">A function that determines if the obstacle is between the two given coordinates , in other words, if the two coordinates see each other.</param>
    private static void AddObstacleNeighborsForObstacle(
        Obstacle obstacle,
        Func<Coordinate, Coordinate, Obstacle, bool> isCoordinateHidden)
    {
        var coordinates = obstacle.Coordinates.CreateCopy().Distinct().ToList();
        if (coordinates.Count == 1)
        {
            return;
        }

        coordinates.Each((index, coordinate) =>
        {
            var nextCoordinate = index + 1 < coordinates.Count ? coordinates[index + 1] : null;
            var previousCoordinate = index - 1 >= 0 ? coordinates[index - 1] : null;

            var neighbors = new HashSet<Position>();

            if (obstacle.IsClosed && nextCoordinate == null)
            {
                nextCoordinate = coordinates.First();
            }

            if (obstacle.IsClosed && previousCoordinate == null)
            {
                previousCoordinate = coordinates[^1];
            }

            if (nextCoordinate != null)
            {
                var nextVertexHidden = isCoordinateHidden(coordinate, nextCoordinate, obstacle);
                if (!nextVertexHidden)
                {
                    neighbors.Add(nextCoordinate.ToPosition());
                }
            }

            if (previousCoordinate != null)
            {
                var previousVertexHidden = isCoordinateHidden(coordinate, previousCoordinate, obstacle);
                if (!previousVertexHidden)
                {
                    neighbors.Add(previousCoordinate.ToPosition());
                }
            }

            var vertex = obstacle.Vertices.First(v => v.Coordinate.Equals(coordinate));
            neighbors.Each(n =>
            {
                if (!vertex.ObstacleNeighbors.Contains(n))
                {
                    vertex.ObstacleNeighbors.Add(n);
                }
            });
            vertex.SortObstacleNeighborsByAngle();
        });
    }

    /// <summary>
    /// Determines for each vertex the "k" nearest visible vertices (visibility neighbors) with respect to the given
    /// obstacles. The vertices are extracted from the obstacles.
    ///
    /// The parameter "k" (for the knn search, hence the methods name) is determined by the following partitioning
    /// strategy:
    /// This method uses bins to limit the number of visibility neighbors per angle area, since each bin covers a
    /// certain angle area of each vertex. Example: If "neighborBinCount" is set to 4, then each bin covers 90°. The
    /// "neighborsPerBin" also limit the amount of visible vertices per bin.
    /// </summary>
    /// <returns>
    /// A map from vertex to "neighborBinCount"-many sub-lists, each containing vertices of one bin.
    /// </returns>
    public static Dictionary<Vertex, List<List<Vertex>>> CalculateVisibleKnn(QuadTree<Obstacle> obstacles,
        int neighborBinCount, int neighborsPerBin = 10, bool debugModeActive = false)
    {
        Log.D("Get direct neighbors on each obstacle geometry");
        var allObstacles = obstacles.QueryAll();
        var coordinateToObstacles = GetCoordinateToObstaclesMapping(allObstacles);
        AddObstacleNeighborsForObstacles(allObstacles, coordinateToObstacles, debugModeActive);

        Log.D("Collect all unique vertices");
        var allVertices = allObstacles.Map(o => o.Vertices).SelectMany(x => x).Where(v => v.IsOnConvexHull).ToSet();

        Log.D("Calculate KNN to get visible vertices");
        var vertexNeighbors = CalculateVisibleKnnInternal(obstacles, coordinateToObstacles, allVertices,
            neighborBinCount, neighborsPerBin, debugModeActive);

        return vertexNeighbors;
    }

    /// <summary>
    /// Same as "CalculateVisibleKnn()" but takes the already determined vertices and the mapping from coordinate to
    /// obstacles.
    /// </summary>
    /// <param name="coordinateToObstacles">
    /// A map from coordinate to all obstacles that include this geometry somewhere in their geometry.
    /// </param>
    private static Dictionary<Vertex, List<List<Vertex>>> CalculateVisibleKnnInternal(
        QuadTree<Obstacle> obstacles,
        Dictionary<Coordinate, List<Obstacle>> coordinateToObstacles,
        ICollection<Vertex> vertices,
        int neighborBinCount,
        int neighborsPerBin = 10,
        bool debugModeActive = false)
    {
        var result = new Dictionary<Vertex, List<List<Vertex>>>();
        Log.D(
            $"Calculate nearest visible neighbors for each vertex. Bin size is {neighborBinCount} with {neighborsPerBin} neighbors per bin.");

        var i = 1;
        var verticesPerPercent = vertices.Count / 100d;
        var nextProcessOutput = verticesPerPercent;
        var totalTimeStopWatch = new Stopwatch();
        var stopWatch = new Stopwatch();
        totalTimeStopWatch.Start();
        stopWatch.Start();
        foreach (var vertex in vertices)
        {
            if (i > nextProcessOutput)
            {
                Log.D($"  {(int)(i / verticesPerPercent)}% done ({stopWatch.ElapsedMilliseconds}ms)");
                stopWatch.Restart();
                nextProcessOutput += verticesPerPercent;
            }

            i++;

            result[vertex] = GetVisibilityNeighborsForVertex(obstacles, new List<Vertex>(vertices),
                coordinateToObstacles,
                vertex, neighborBinCount, neighborsPerBin);
        }

        Log.D($"  100% done after a total of {totalTimeStopWatch.ElapsedMilliseconds}ms");

        if (debugModeActive)
        {
            var vertexPositionDict = new Dictionary<Coordinate, HashSet<Position>>();
            result.Keys.Each(vertex =>
            {
                var visibilityNeighbors = result[vertex];
                var visibilityNeighborPositions =
                    visibilityNeighbors.SelectMany(x => x).Map(v => v.Coordinate.ToPosition());
                vertexPositionDict[vertex.Coordinate] = new HashSet<Position>(visibilityNeighborPositions);
            });

            Exporter.WriteVertexNeighborsToFile(vertexPositionDict, "vertex-visibility.geojson");
        }

        return result;
    }

    /// <summary>
    /// Determines all visibility neighbors with respect to the limits given by the maximum of "neighborsPerBin" many
    /// neighbors per bin for each of the "neighborBinCount" many bins.
    /// </summary>
    public static List<List<Vertex>> GetVisibilityNeighborsForVertex(
        QuadTree<Obstacle> obstacles,
        List<Vertex> vertices,
        Dictionary<Coordinate, List<Obstacle>> coordinateToObstacles,
        Vertex vertex,
        int neighborBinCount = 36,
        int neighborsPerBin = 10)
    {
        /*
         * The idea of the shadow areas:
         *
         * Imagine a vertex is a lamp, each obstacle casts a shadow, which covers a certain angle area. Each shadow
         * (= instance of the AngleArea class) also has a certain distance. Everything within this angle area and
         * further away than that distance is definitely not visible.
         *
         * Keeping track of these shadow areas and their distances reduces the amount of expensive collision checks
         * significantly (i.e. by a factor of 20 for a ~250 obstacles large dataset).
         */
        var shadowAreas = new BinIndex<ShadowArea>(360);

        // Store the shadow areas for each obstacle, to no add them twice and to prevent unnecessary intersection checks.
        var obstacleToShadowArea = new Dictionary<Obstacle, ShadowArea>();

        var degreePerBin = 360.0 / neighborBinCount;

        // TODO Documentation
        var validAngleAreas = new List<(double, double)>();
        if (vertex.ObstacleNeighbors.Count == 0)
        {
            validAngleAreas.Add((0, 360));
        }
        else if (vertex.ObstacleNeighbors.Count == 1)
        {
            var angleToSingleObstacleNeighbor =
                Angle.GetBearing(vertex.Coordinate.ToPosition(), vertex.ObstacleNeighbors[0]);
            if (angleToSingleObstacleNeighbor != 0)
            {
                validAngleAreas.Add((angleToSingleObstacleNeighbor, 0));
                validAngleAreas.Add((0, angleToSingleObstacleNeighbor));
            }
        }
        else if (vertex.ObstacleNeighbors.Count >= 2)
        {
            // Only up to one angle between two adjacent obstacle neighbors can be >180°. This means only none or one
            // valid angle areas exist.
            vertex.ObstacleNeighbors
                .Each((thisIndex, neighbor) =>
                {
                    var nextIndex = thisIndex + 1;
                    if (thisIndex == vertex.ObstacleNeighbors.Count - 1)
                    {
                        nextIndex = 0;
                    }

                    var angleFrom = Angle.GetBearing(vertex.Coordinate.ToPosition(), neighbor);
                    var angleTo = Angle.GetBearing(vertex.Coordinate.ToPosition(), vertex.ObstacleNeighbors[nextIndex]);

                    if (Angle.Difference(angleFrom, angleTo) > 180)
                    {
                        validAngleAreas.Add((angleFrom, Angle.Normalize(angleTo - 180)));
                        validAngleAreas.Add((Angle.Normalize(angleFrom - 180), angleTo));
                    }
                });
        }

        /*
         * These arrays store the neighbors sorted by their distance from close (at index 0) for furthest. In the end
         * the "neighbors" array contains the closest "neighborsPerBin"-many visibility neighbors.
         */
        var visibilityNeighbors = new LinkedList<Vertex>?[neighborBinCount];
        var maxDistances = new LinkedList<double>?[neighborBinCount];

        foreach (var otherVertex in vertices)
        {
            if (otherVertex.Equals(vertex) || !otherVertex.IsOnConvexHull)
            {
                continue;
            }

            if (!validAngleAreas.Any(area => Angle.IsBetweenEqual(area.Item1,
                    Angle.GetBearing(vertex.Coordinate, otherVertex.Coordinate), area.Item2)))
            {
                continue;
            }

            var angle = Angle.GetBearing(vertex.Coordinate, otherVertex.Coordinate);
            var binKey = (int)(angle / degreePerBin);

            if (binKey == neighborBinCount)
            {
                // This can rarely happen and an "if" is faster than doing module.
                binKey = 0;
            }

            var distanceToOtherVertex = vertex.Coordinate.Distance(otherVertex.Coordinate);
            if (maxDistances[binKey]?.Count == neighborsPerBin &&
                distanceToOtherVertex >= maxDistances[binKey]?.Last?.Value)
            {
                continue;
            }

            var isInShadowArea = IsInShadowArea(shadowAreas, angle, distanceToOtherVertex);
            if (isInShadowArea)
            {
                continue;
            }

            var envelope = new Envelope(vertex.Coordinate, otherVertex.Coordinate);
            var intersectsWithObstacle = false;
            obstacles.Query(envelope, (Action<Obstacle>)(obstacle =>
            {
                if (intersectsWithObstacle || !obstacle.CanIntersect(envelope))
                {
                    return;
                }

                var vertexIsOnThisObstacle = obstacle.Vertices.Contains(vertex);

                ShadowArea? shadowArea;
                bool obstacleIsAlreadyCastingShadow;
                if (obstacleToShadowArea.TryGetValue(obstacle, out var value))
                {
                    shadowArea = value;
                    obstacleIsAlreadyCastingShadow = true;
                }
                else
                {
                    shadowArea = obstacle.GetShadowAreaOfObstacle(vertex);
                    obstacleToShadowArea[obstacle] = shadowArea;
                    obstacleIsAlreadyCastingShadow = false;
                }

                // Only consider obstacles not belonging to this vertex (could lead to false shadows) and also just
                // consider new obstacles, since a shadow test with existing obstacles was already performed earlier.
                if (!vertexIsOnThisObstacle && !obstacleIsAlreadyCastingShadow)
                {
                    if (shadowArea.IsValid)
                    {
                        shadowAreas.Add(shadowArea.From, shadowArea.To, shadowArea);

                        if (IsInShadowArea(shadowAreas, angle, distanceToOtherVertex))
                        {
                            // otherVertex is within newly added shadow areas -> definitely not visible
                            intersectsWithObstacle = true;
                            return;
                        }
                    }
                }

                var otherVertexIsInObstacleAngleArea = Angle.IsBetweenEqual(shadowArea.From, angle, shadowArea.To);
                if (!otherVertexIsInObstacleAngleArea)
                {
                    // If "otherVertex" is NOT within the angle area of the obstacle, then there's no chance they
                    // intersect. Therefore, a subsequent intersection check can be skipped.
                    return;
                }

                intersectsWithObstacle |=
                    obstacle.IntersectsWithLine(vertex.Coordinate, otherVertex.Coordinate, coordinateToObstacles);
            }));

            if (intersectsWithObstacle)
            {
                // The line between "vertex" and "otherVertex" intersects with an obstacle -> "otherVertex" not visible
                // so we can check the next other vertex.
                continue;
            }

            // For simplicity, only the neighbor list is used for null checks. However, the maxDistance list is
            // null if and only if the neighbor list is null.
            // TODO Find a better solution for this two-list-situation

            if (visibilityNeighbors[binKey] == null)
            {
                visibilityNeighbors[binKey] = new LinkedList<Vertex>();
                maxDistances[binKey] = new LinkedList<double>();
            }

            var visibilityNeighborList = visibilityNeighbors[binKey]!;
            var maxDistanceList = maxDistances[binKey]!;

            var visibilityNeighborNode = visibilityNeighborList.Last;
            var distanceNode = maxDistanceList.Last;

            // Find the first element in the list with a distance lower than the calculated  "distanceToOtherVertex"
            while (visibilityNeighborNode != null)
            {
                var distance = distanceNode!.Value;

                if (distance > distanceToOtherVertex)
                {
                    visibilityNeighborNode = visibilityNeighborNode.Previous;
                    distanceNode = distanceNode.Previous;
                }
                else
                {
                    break;
                }
            }

            if (visibilityNeighborNode == null)
            {
                visibilityNeighborList.AddLast(otherVertex);
                maxDistanceList.AddLast(distanceToOtherVertex);
            }
            else
            {
                visibilityNeighborList.AddAfter(visibilityNeighborNode, otherVertex);
                maxDistanceList.AddAfter(distanceNode, distanceToOtherVertex);
            }

            if (visibilityNeighborList.Count > neighborsPerBin)
            {
                visibilityNeighborList.RemoveLast();
                maxDistanceList.RemoveLast();
            }
        }

        var allVisibilityNeighbors = new HashSet<Vertex>();
        foreach (var neighbor in visibilityNeighbors)
        {
            if (neighbor != null)
            {
                allVisibilityNeighbors.AddRange(neighbor);
            }
        }

        return SortVisibilityNeighborsIntoBins(vertex, allVisibilityNeighbors.ToList());
    }

    /// <summary>
    /// Takes the obstacle neighbors from the given vertex and interprets the angle areas between these neighbors as
    /// bins. Each of the given visibility neighbors is then sorted into these bins.
    /// </summary>
    public static List<List<Vertex>> SortVisibilityNeighborsIntoBins(Vertex vertex, List<Vertex> allVisibilityNeighbors)
    {
        if (vertex.ObstacleNeighbors.Count < 2)
        {
            return new List<List<Vertex>>
            {
                allVisibilityNeighbors
            };
        }

        allVisibilityNeighbors.Sort((p1, p2) =>
            (int)(Angle.GetBearing(vertex.Coordinate, p1.Coordinate) -
                  Angle.GetBearing(vertex.Coordinate, p2.Coordinate)));

        /*
         * This following routing collects all visibility neighbors we just determined above and puts them into bins.
         * Each bin covers the angle area between two obstacle neighbors on the vertex.
         */
        var result = new List<List<Vertex>>();
        var bin = new List<Vertex>();

        // Collect all visibility neighbors with angles between two obstacle neighbors. We go all the way through the
        // obstacle neighbors of the vertex and always process the angle area between the current obstacle neighbor and
        // the next. For the last obstacle neighbor, the index of the next one is 0 (hence the modulo).
        for (var obstacleNeighborIndex = 0;
             obstacleNeighborIndex < vertex.ObstacleNeighbors.Count;
             obstacleNeighborIndex++)
        {
            var thisObstacleNeighbor = vertex.ObstacleNeighbors[obstacleNeighborIndex];
            var nextObstacleNeighbor =
                vertex.ObstacleNeighbors[(obstacleNeighborIndex + 1) % vertex.ObstacleNeighbors.Count];

            var thisObstacleNeighborBearing = Angle.GetBearing(vertex.Coordinate, thisObstacleNeighbor.ToCoordinate());
            var nextObstacleNeighborBearing = Angle.GetBearing(vertex.Coordinate, nextObstacleNeighbor.ToCoordinate());

            // Due to the bins, it can happen that this obstacle neighbor is not within the list of visibility
            // neighbors, even though it's obviously visible. Therefore this obstacle neighbor might not be added here.
            var thisVisibilityNeighbor =
                allVisibilityNeighbors.Where(v =>
                    v.Coordinate.X == thisObstacleNeighbor.X && v.Coordinate.Y == thisObstacleNeighbor.Y).ToList();
            if (!thisVisibilityNeighbor.IsEmpty())
            {
                bin.Add(thisVisibilityNeighbor[0]);
            }

            foreach (var visibilityNeighbor in allVisibilityNeighbors)
            {
                if (Angle.IsBetweenEqual(thisObstacleNeighborBearing,
                        Angle.GetBearing(vertex.Coordinate, visibilityNeighbor.Coordinate),
                        nextObstacleNeighborBearing))
                {
                    bin.Add(visibilityNeighbor);
                }
            }

            // Due to the bins, it can happen that this obstacle neighbor is not within the list of visibility
            // neighbors, even though it's obviously visible. Therefore this obstacle neighbor might not be added here.
            var nextVisibilityNeighbor =
                allVisibilityNeighbors.Where(v =>
                    v.Coordinate.X == nextObstacleNeighbor.X && v.Coordinate.Y == nextObstacleNeighbor.Y).ToList();
            if (!nextVisibilityNeighbor.IsEmpty())
            {
                bin.Add(nextVisibilityNeighbor[0]);
            }

            result.Add(bin.Distinct().ToList());
            bin = new List<Vertex>();
        }

        return result;
    }

    public static Dictionary<Coordinate, List<Obstacle>> GetCoordinateToObstaclesMapping(
        IEnumerable<Obstacle> allObstacles)
    {
        Dictionary<Coordinate, List<Obstacle>> coordinateToObstacles = new();
        allObstacles.Each(o => o.Vertices.Each(v =>
        {
            if (!coordinateToObstacles.ContainsKey(v.Coordinate))
            {
                coordinateToObstacles[v.Coordinate] = new List<Obstacle>();
            }

            coordinateToObstacles[v.Coordinate].Add(o);
        }));
        return coordinateToObstacles;
    }

    private static bool IsInShadowArea(BinIndex<ShadowArea> shadowAreas, double angle, double distance)
    {
        foreach (var area in shadowAreas.Query(angle))
        {
            if (distance > area.Distance && Angle.IsBetweenEqual(area.From, angle, area.To))
            {
                return true;
            }
        }

        return false;
    }
}