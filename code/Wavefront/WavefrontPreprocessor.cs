using System.Diagnostics;
using Mars.Common;
using Mars.Common.Collections;
using Mars.Common.Core.Collections;
using Mars.Numerics;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Triangulate.Polygon;
using ServiceStack;
using Wavefront.Geometry;
using Wavefront.Index;
using Wavefront.IO;
using Feature = NetTopologySuite.Features.Feature;
using Position = Mars.Interfaces.Environments.Position;

namespace Wavefront;

public class WavefrontPreprocessor
{
    /// <summary>
    /// Helper class modeling an angle area starting at a certain distance. Think of a piece of pizza that doesn't start
    /// at the center of the pizza but somewhat further out.
    /// </summary>
    private class AngleArea
    {
        public double From { get; }
        public double To { get; }
        public double Distance { get; }

        public AngleArea(double from, double to, double distance)
        {
            From = from;
            To = to;
            Distance = distance;
        }

        public override int GetHashCode()
        {
            return (int)(Distance * 7919);
        }
    }

    /// <summary>
    /// Splits each obstacle into smaller obstacles with the given amount of edges. This enhances the performance of
    /// further preprocessing, because collision checks are now performed on smaller objects.
    /// </summary>
    public static QuadTree<Obstacle> SplitObstacles(List<Obstacle> obstacles, bool debugModeActive = false)
    {
        var vertexCount = obstacles.Sum(o => o.Coordinates.Count);
        PerformanceMeasurement.TOTAL_VERTICES = vertexCount;
        Log.D($"Amount of obstacles before splitting: {obstacles.Count}");
        Log.D($"Amount of vertices before splitting: {vertexCount}");

        obstacles = obstacles.Map(o =>
            {
                if (!o.IsClosed)
                {
                    return new List<Obstacle> { o };
                }

                var newObstacles = new List<Obstacle>();

                // Perform a triangulation of each polygon. This makes it much easier to perform visibility checks.
                try
                {
                    var geometryCollection = (GeometryCollection)PolygonTriangulator.Triangulate(o.Geometry);
                    geometryCollection.Each(triangle =>
                    {
                        Obstacle.Create(triangle).Each(newObstacle => newObstacles.Add(newObstacle));
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }

                return newObstacles;
            })
            .SelectMany(x => x)
            .ToList();

        vertexCount = obstacles.Sum(o => o.Coordinates.Count);
        PerformanceMeasurement.TOTAL_VERTICES_AFTER_PREPROCESSING = vertexCount;
        Log.D($"Amount of obstacles after splitting: {obstacles.Count}");
        Log.D($"Amount of vertices after splitting: {vertexCount}");

        if (debugModeActive)
        {
            var featureCollection = new FeatureCollection();
            obstacles.Each(o => featureCollection.Add(new Feature(o.Geometry, new AttributesTable())));
            Exporter.WriteFeaturesToFile(featureCollection, "obstacles-splitted.geojson").Wait();
        }

        var obstacleIndex = new QuadTree<Obstacle>();
        obstacles.Each(obstacle => obstacleIndex.Insert(obstacle.Envelope, obstacle));
        return obstacleIndex;
    }

    /// <summary>
    /// Calculates the neighbor relationship for each vertex v in the given obstacles. The term "neighbor" here means
    /// neighboring positions on multiple obstacles but not across open spaces. This means there is an edge on at least
    /// one of the given obstacles from v to its neighboring positions.
    /// </summary>
    /// <returns>A dict from position to a duplicate free list of all neighbors found.</returns>
    public static Dictionary<Position, List<Position>> GetNeighborsFromObstacleVertices(IList<Obstacle> obstacles,
        Dictionary<Coordinate, List<Obstacle>> coordinateToObstacles, bool debugModeActive = false)
    {
        // A function that determines if any obstacles is between the two given coordinates.
        var isCoordinateHidden = (Coordinate coordinate, Coordinate otherCoordinate, Obstacle obstacleOfCoordinate) =>
            obstacles.Any(o =>
                o.IsClosed &&
                (
                    !obstacleOfCoordinate.Equals(o) &&
                    o.HasLineSegment(coordinate, otherCoordinate) ||
                    o.IntersectsWithLine(coordinate, otherCoordinate, coordinateToObstacles)
                ));

        var positionToNeighbors = new Dictionary<Position, HashSet<Position>>();
        obstacles.Each(obstacle => { AddNeighborsForObstacle(obstacle, positionToNeighbors, isCoordinateHidden); });

        if (!PerformanceMeasurement.IS_ACTIVE && debugModeActive)
        {
            Exporter.WriteVertexNeighborsToFile(positionToNeighbors);
        }

        // Use a list for easier handling later on (Vertices will eventually receive these neighbors and must be able to
        // sort them).
        var result = new Dictionary<Position, List<Position>>();
        positionToNeighbors.Each(pair => result.Add(pair.Key, pair.Value.ToList()));
        return result;
    }

    private static void AddNeighborsForObstacle(Obstacle obstacle,
        Dictionary<Position, HashSet<Position>> positionToNeighbors,
        Func<Coordinate, Coordinate, Obstacle, bool> isCoordinateHidden)
    {
        var coordinates = obstacle.Coordinates.CreateCopy().Distinct().ToList();

        if (coordinates.Count == 1)
        {
            positionToNeighbors[coordinates[0].ToPosition()] = new HashSet<Position>();
            positionToNeighbors[coordinates[0].ToPosition()].AddRange(new[] { coordinates[0].ToPosition() });
            return;
        }

        coordinates.Each((index, coordinate) =>
        {
            var position = coordinate.ToPosition();
            if (!positionToNeighbors.ContainsKey(position))
            {
                positionToNeighbors[position] = new HashSet<Position>();
            }

            Coordinate? nextCoordinate =
                index + 1 < coordinates.Count ? coordinates[index + 1] : null;
            Coordinate? previousCoordinate = index - 1 >= 0 ? coordinates[index - 1] : null;

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

            positionToNeighbors[position].AddRange(neighbors);
        });
    }

    public static Dictionary<Vertex, List<List<Vertex>>> CalculateVisibleKnn(QuadTree<Obstacle> obstacles,
        int neighborBinCount, int neighborsPerBin = 10, bool debugModeActive = false)
    {
        Log.D("Get direct neighbors on each obstacle geometry");
        var allObstacles = obstacles.QueryAll();

        var coordinateToObstacles = GetCoordinateToObstaclesMapping(allObstacles);

        Dictionary<Position, List<Position>> positionToNeighbors = new();
        var result = PerformanceMeasurement.ForFunction(
            () =>
            {
                positionToNeighbors =
                    GetNeighborsFromObstacleVertices(allObstacles, coordinateToObstacles, debugModeActive);
            },
            "GetNeighborsFromObstacleVertices");
        result.Print();
        result.WriteToFile();

        Log.I("Create map of direct neighbor vertices on the obstacle geometries");
        var vertices = positionToNeighbors.Keys.Map(position => new Vertex(position, positionToNeighbors[position]));

        Log.I("Add vertices with neighbor information to obstacles");
        allObstacles.Each(o =>
        {
            var verticesInObstacle = vertices.Intersect(o.Vertices);
            o.Vertices = verticesInObstacle.ToList();
        });

        Log.I("Calculate KNN to get visible vertices");
        var vertexNeighbors = new Dictionary<Vertex, List<List<Vertex>>>();
        result = PerformanceMeasurement.ForFunction(() =>
        {
            vertexNeighbors =
                CalculateVisibleKnnInternal(obstacles, coordinateToObstacles, vertices, neighborBinCount,
                    neighborsPerBin, debugModeActive);
        }, "CalculateVisibleKnn");
        result.Print();
        result.WriteToFile();

        return vertexNeighbors;
    }

    private static Dictionary<Vertex, List<List<Vertex>>> CalculateVisibleKnnInternal(QuadTree<Obstacle> obstacles,
        Dictionary<Coordinate, List<Obstacle>> coordinateToObstacles, List<Vertex> vertices, int neighborBinCount,
        int neighborsPerBin = 10, bool debugModeActive = false)
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

            result[vertex] = GetVisibleNeighborsForVertex(obstacles, new List<Vertex>(vertices), coordinateToObstacles,
                vertex, neighborBinCount, neighborsPerBin);
        }

        Log.D($"  100% done after a total of {totalTimeStopWatch.ElapsedMilliseconds}ms");

        if (!PerformanceMeasurement.IS_ACTIVE && debugModeActive)
        {
            var vertexPositionDict = new Dictionary<Position, HashSet<Position>>();
            result.Keys.Each(vertex =>
            {
                var visibleNeighbors = result[vertex];
                var visibleNeighborPositions = visibleNeighbors.SelectMany(x => x).Map(v => v.Position);
                vertexPositionDict[vertex.Position] = new HashSet<Position>(visibleNeighborPositions);
            });

            Exporter.WriteVertexNeighborsToFile(vertexPositionDict, "vertex-visibility.geojson");
        }

        return result;
    }

    public static List<List<Vertex>> GetVisibleNeighborsForVertex(QuadTree<Obstacle> obstacles, List<Vertex> vertices,
        Vertex vertex, int neighborBinCount, int neighborsPerBin = 10)
    {
        return GetVisibleNeighborsForVertex(obstacles, vertices, new Dictionary<Coordinate, List<Obstacle>>(), vertex,
            neighborBinCount, neighborsPerBin);
    }

    public static List<List<Vertex>> GetVisibleNeighborsForVertex(QuadTree<Obstacle> obstacles, List<Vertex> vertices,
        Dictionary<Coordinate, List<Obstacle>> coordinateToObstacles, Vertex vertex, int neighborBinCount,
        int neighborsPerBin = 10)
    {
        var shadowAreas = new BinIndex<AngleArea>(360);
        var obstaclesCastingShadow = new HashSet<Obstacle>();

        var degreePerBin = 360.0 / neighborBinCount;

        /*
         * These arrays store the neighbors sorted by their distance from close (at index 0) for furthest. In the end
         * the "neighbors" array contains the closest "neighborsPerBin"-many neighbors.
         */
        var neighbors = new LinkedList<Vertex>?[neighborBinCount];
        var maxDistances = new LinkedList<double>?[neighborBinCount];

        foreach (var otherVertex in vertices)
        {
            if (otherVertex.Equals(vertex))
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

            var distanceToOtherVertex =
                Distance.Euclidean(vertex.Position.PositionArray, otherVertex.Position.PositionArray);
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
                var obstacleIsAlreadyCastingShadow = obstaclesCastingShadow.Contains(obstacle);

                // Only consider obstacles not belonging to this vertex (could lead to false shadows) and also just
                // consider new obstacles, since a shadow test with existing obstacles was already performed earlier.
                if (!vertexIsOnThisObstacle && !obstacleIsAlreadyCastingShadow)
                {
                    var (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(vertex);

                    if (!Double.IsNaN(angleFrom) && !Double.IsNaN(angleTo) && !Double.IsNaN(distance))
                    {
                        shadowAreas.Add(angleFrom, angleTo, new AngleArea(angleFrom, angleTo, distance));
                        obstaclesCastingShadow.Add(obstacle);

                        if (IsInShadowArea(shadowAreas, angle, distanceToOtherVertex))
                        {
                            // otherVertex is within newly added shadow areas -> definitely not visible
                            intersectsWithObstacle = true;
                            return;
                        }
                    }
                }

                intersectsWithObstacle |=
                    obstacle.IntersectsWithLine(vertex.Coordinate, otherVertex.Coordinate, coordinateToObstacles);
            }));

            if (!intersectsWithObstacle)
            {
                // For simplicity, only the neighbor list is used for null checks. However, the maxDistance list is
                // null if and only if the neighbor list is null.

                if (neighbors[binKey] == null)
                {
                    neighbors[binKey] = new LinkedList<Vertex>();
                    maxDistances[binKey] = new LinkedList<double>();
                }

                var neighborList = neighbors[binKey]!;
                var maxDistanceList = maxDistances[binKey]!;

                var neighborNode = neighborList.Last;
                var distanceNode = maxDistanceList.Last;

                // Find the first element in the list with a distance lower than the calculated  "distanceToOtherVertex"
                while (neighborNode != null)
                {
                    var distance = distanceNode!.Value;

                    if (distance > distanceToOtherVertex)
                    {
                        neighborNode = neighborNode.Previous;
                        distanceNode = distanceNode.Previous;
                    }
                    else
                    {
                        break;
                    }
                }

                if (neighborNode == null)
                {
                    neighborList.AddLast(otherVertex);
                    maxDistanceList.AddLast(distanceToOtherVertex);
                }
                else
                {
                    neighborList.AddAfter(neighborNode, otherVertex);
                    maxDistanceList.AddAfter(distanceNode, distanceToOtherVertex);
                }

                if (neighborList.Count > neighborsPerBin)
                {
                    neighborList.RemoveLast();
                    maxDistanceList.RemoveLast();
                }
            }
        }

        var allNeighbors = new HashSet<Vertex>();
        foreach (var neighbor in neighbors)
        {
            if (neighbor != null)
            {
                allNeighbors.AddRange(neighbor);
            }
        }

        return SortVisibleNeighborsIntoBins(vertex, allNeighbors.ToList());
    }

    /// <summary>
    /// Takes the obstacle neighbors from the given vertex and interprets the angle areas between these neighbors as
    /// bins. Each of the given visible neighbors is then sorted into these bins.
    /// </summary>
    public static List<List<Vertex>> SortVisibleNeighborsIntoBins(Vertex vertex, List<Vertex> allVisibleNeighbors)
    {
        if (vertex.Neighbors.Count < 2)
        {
            return new List<List<Vertex>>
            {
                allVisibleNeighbors
            };
        }

        allVisibleNeighbors.Sort((p1, p2) =>
            (int)(Angle.GetBearing(vertex.Position, p1.Position) - Angle.GetBearing(vertex.Position, p2.Position)));

        /*
         * This following routing collects all visible neighbors we just determined above and puts them into bins.
         * Each bin covers the angle area between two obstacle neighbors on the vertex.
         */
        var result = new List<List<Vertex>>();
        // var allNeighborIndex = 0;
        // var initialAllNeighborIndex = 0;

        // Skip all visible neighbors which angle is lower than the first obstacle neighbor. This just makes processing
        // below a bit easier.
        // foreach (var visibleNeighbor in allVisibleNeighbors)
        // {
        //     if (Angle.GetBearing(vertex.Position, visibleNeighbor.Position) >= vertex.Neighbors[0].Bearing)
        //     {
        //         break;
        //     }
        //
        //     initialAllNeighborIndex++;
        // }

        var bin = new List<Vertex>();

        // Collect all visible neighbors visible between two obstacle neighbors. We go all the way through the obstacle
        // neighbors of the vertex and always process the angle area between the current neighbor and the next. For the
        // last neighbor, the index of the next neighbor is 0 (hence the modulo).
        for (var obstacleNeighborIndex = 0; obstacleNeighborIndex < vertex.Neighbors.Count; obstacleNeighborIndex++)
        {
            var thisObstacleNeighbor = vertex.Neighbors[obstacleNeighborIndex];
            var nextObstacleNeighbor = vertex.Neighbors[(obstacleNeighborIndex + 1) % vertex.Neighbors.Count];

            // Due to the bins, it can happen that this obstacle neighbor is not within the list of visible neighbors,
            // even though it's obviously visible. Therefore this obstacle neighbor might not be added here.
            var thisVisibleNeighbor = allVisibleNeighbors.Where(v => v.Position.Equals(thisObstacleNeighbor)).ToList();
            if (!thisVisibleNeighbor.IsEmpty())
            {
                bin.Add(thisVisibleNeighbor[0]);
            }

            for (var i = 0; i < allVisibleNeighbors.Count; i++)
            {
                var visibleNeighbor = allVisibleNeighbors[i];
                if (Angle.IsBetweenEqual(thisObstacleNeighbor.Bearing,
                        Angle.GetBearing(vertex.Position, visibleNeighbor.Position), nextObstacleNeighbor.Bearing))
                {
                    bin.Add(visibleNeighbor);
                }
            }

            // Due to the bins, it can happen that this obstacle neighbor is not within the list of visible neighbors,
            // even though it's obviously visible. Therefore this obstacle neighbor might not be added here.
            var nextVisibleNeighbor = allVisibleNeighbors.Where(v => v.Position.Equals(nextObstacleNeighbor)).ToList();
            if (!nextVisibleNeighbor.IsEmpty())
            {
                bin.Add(nextVisibleNeighbor[0]);
            }

            result.Add(bin.Distinct().ToList());
            bin = new List<Vertex>();
        }

        return result;
    }

    public static Dictionary<Coordinate, List<Obstacle>> GetCoordinateToObstaclesMapping(IList<Obstacle> allObstacles)
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

    private static bool IsInShadowArea(BinIndex<AngleArea> shadowAreas, double angle, double distance)
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