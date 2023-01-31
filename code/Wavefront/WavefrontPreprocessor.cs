using System.Diagnostics;
using Mars.Common;
using Mars.Common.Collections;
using Mars.Common.Core.Collections;
using Mars.Numerics;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json;
using ServiceStack;
using Wavefront.Geometry;
using Wavefront.Index;
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
    public static QuadTree<Obstacle> SplitObstacles(List<Obstacle> obstacles, int maxObstacleLength = 20)
    {
        var vertexCount = obstacles.Sum(o => o.Coordinates.Count);
        PerformanceMeasurement.TOTAL_VERTICES = vertexCount;
        Log.D($"Amount of obstacles before splitting: {obstacles.Count}");
        Log.D($"Amount of vertices before splitting: {vertexCount}");

        obstacles = obstacles.Map(o =>
        {
            var result = new List<Obstacle>();

            if (o.Coordinates.Count <= maxObstacleLength)
            {
                result.Add(o);
                return result;
            }

            for (int i = 0; i < o.Coordinates.Count - 1; i += maxObstacleLength)
            {
                if (i + maxObstacleLength < o.Coordinates.Count)
                {
                    result.Add(new Obstacle(new LineString(o.Coordinates.Skip(i).Take(maxObstacleLength + 1)
                        .ToArray())));
                }
                else
                {
                    result.Add(new Obstacle(new LineString(o.Coordinates.Skip(i).ToArray())));
                }
            }

            return result;
        }).SelectMany(x => x).ToList();

        vertexCount = obstacles.Sum(o => o.Coordinates.Count);
        PerformanceMeasurement.TOTAL_VERTICES_AFTER_PREPROCESSING = vertexCount;
        Log.D($"Amount of obstacles after splitting: {obstacles.Count}");
        Log.D($"Amount of vertices after splitting: {vertexCount}");

        var obstacleIndex = new QuadTree<Obstacle>();
        obstacles.Each(obstacle => obstacleIndex.Insert(obstacle.Envelope, obstacle));
        return obstacleIndex;
    }

    /// <summary>
    /// Calculates the neighbor relationship for each vertex in the given obstacles.
    ///
    /// Neighbor here means across obstacles.
    /// </summary>
    /// <returns>A dict from position to a duplicate free list of all neighbors found.</returns>
    public static Dictionary<Position, List<Position>> GetNeighborsFromObstacleVertices(IList<Obstacle> obstacles, bool debugModeActive = false)
    {
        // A function that determines if any obstacles is between the two given coordinates.
        var isCoordinateHidden = (Coordinate coordinate, Coordinate otherCoordinate, Obstacle obstacleOfCoordinate) =>
            obstacles.Any(o =>
                o.IsClosed &&
                (
                    !obstacleOfCoordinate.Equals(o) &&
                    o.HasLineSegment(coordinate, otherCoordinate) ||
                    o.IntersectsWithLine(coordinate, otherCoordinate)
                ));

        var positionToNeighbors = new Dictionary<Position, HashSet<Position>>();
        obstacles.Each(obstacle =>
        {
            if (obstacle.Coordinates.Count <= 1)
            {
                return;
            }

            AddNeighborsForObstacle(obstacle, positionToNeighbors, isCoordinateHidden);
        });

        if (!PerformanceMeasurement.IS_ACTIVE && debugModeActive)
        {
            WriteVertexNeighborsToFile(positionToNeighbors);
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

    public static Dictionary<Vertex, List<Vertex>> CalculateVisibleKnn(QuadTree<Obstacle> obstacles, int neighborBinCount, int neighborsPerBin = 10, bool debugModeActive = false)
    {
        Log.D("Get direct neighbors on each obstacle geometry");
        Dictionary<Position, List<Position>> positionToNeighbors = new();
        var allObstacles = obstacles.QueryAll();
        var result = PerformanceMeasurement.ForFunction(
            () => { positionToNeighbors = GetNeighborsFromObstacleVertices(allObstacles, debugModeActive); },
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
        var vertexNeighbors = new Dictionary<Vertex, List<Vertex>>();
        result = PerformanceMeasurement.ForFunction(() =>
        {
            vertexNeighbors =
                CalculateVisibleKnnInternal(obstacles, vertices, neighborBinCount, neighborsPerBin, debugModeActive);
        }, "CalculateVisibleKnn");
        result.Print();
        result.WriteToFile();

        return vertexNeighbors;
    }

    private static Dictionary<Vertex, List<Vertex>> CalculateVisibleKnnInternal(QuadTree<Obstacle> obstacles,
        List<Vertex> vertices, int neighborBinCount, int neighborsPerBin = 10, bool debugModeActive = false)
    {
        var result = new Dictionary<Vertex, List<Vertex>>();
        Log.D($"Calculate nearest visible neighbors for each vertex. Bin size is {neighborBinCount} with {neighborsPerBin} neighbors per bin.");

        var i = 1;
        var verticesPerPercent = vertices.Count / 100d;
        var nextProcessOutput = verticesPerPercent;
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        foreach (var vertex in vertices)
        {
            if (i > nextProcessOutput)
            {
                Log.D(
                    $"  {Math.Round((1000 * i * verticesPerPercent / vertices.Count) / verticesPerPercent) / 10.0}% done ({stopWatch.ElapsedMilliseconds}ms)");
                stopWatch.Restart();
                nextProcessOutput += verticesPerPercent;
            }

            i++;

            result[vertex] = GetVisibleNeighborsForVertex(obstacles, new List<Vertex>(vertices), vertex, neighborBinCount, neighborsPerBin);
        }

        if (!PerformanceMeasurement.IS_ACTIVE && debugModeActive)
        {
            var vertexPositionDict = new Dictionary<Position, HashSet<Position>>();
            result.Keys.Each(vertex =>
            {
                var visibleNeighbors = result[vertex];
                var visibleNeighborPositions = visibleNeighbors.Map(v => v.Position);
                vertexPositionDict[vertex.Position] = new HashSet<Position>(visibleNeighborPositions);
            });
        
            WriteVertexNeighborsToFile(vertexPositionDict, "vertex-visibility.geojson");
        }

        return result;
    }

    public static List<Vertex> GetVisibleNeighborsForVertex(QuadTree<Obstacle> obstacles, List<Vertex> vertices,
        Vertex vertex, int neighborBinCount, int neighborsPerBin = 10)
    {
        var shadowAreas = new BinIndex<AngleArea>(360);
        var obstaclesCastingShadow = new HashSet<Obstacle>();

        vertices.Remove(vertex);

        var degreePerBin = 360.0 / neighborBinCount;

        /*
         * These arrays store the neighbors sorted by their distance from close (at index 0) for furthest. In the end
         * the "neighbors" array contains the closest "neighborsPerBin"-many neighbors.
         */
        var neighbors = new LinkedList<Vertex>?[neighborBinCount];
        var maxDistances = new LinkedList<double>?[neighborBinCount];

        foreach (var otherVertex in vertices)
        {
            var angle = Angle.GetBearing(vertex.Coordinate, otherVertex.Coordinate);
            var binKey = (int)(angle / degreePerBin);

            var distanceToOtherVertex =
                Distance.Euclidean(vertex.Position.PositionArray, otherVertex.Position.PositionArray);
            if (maxDistances[binKey]?.Count == neighborsPerBin && distanceToOtherVertex >= maxDistances[binKey]?.Last?.Value)
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
                
                if (!obstaclesCastingShadow.Contains(obstacle))
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
                
                intersectsWithObstacle |= obstacle.IntersectsWithLine(vertex.Coordinate, otherVertex.Coordinate);
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

        var result = new List<Vertex>();
        foreach (var neighbor in neighbors)
        {
            if (neighbor != null)
            {
                result.AddRange(neighbor);
            }
        }

        return result;
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

    private static async void WriteVertexNeighborsToFile(Dictionary<Position, HashSet<Position>> positionToNeighbors,
        string filename = "vertex-neighbors.geojson")
    {
        var geometries = new List<NetTopologySuite.Geometries.Geometry>();
        foreach (var pair in positionToNeighbors)
        {
            if (pair.Value.IsEmpty())
            {
                continue;
            }

            var coordinates = new List<Coordinate>();
            coordinates.Add(pair.Key.ToCoordinate());
            foreach (var position in pair.Value)
            {
                coordinates.Add(position.ToCoordinate());
                coordinates.Add(pair.Key.ToCoordinate());
            }

            geometries.Add(new LineString(coordinates.ToArray()));
        }

        var geometry = new GeometryCollection(geometries.ToArray());

        var serializer = GeoJsonSerializer.Create();
        await using var stringWriter = new StringWriter();
        using var jsonWriter = new JsonTextWriter(stringWriter);

        serializer.Serialize(jsonWriter, geometry);
        var geoJson = stringWriter.ToString();

        await File.WriteAllTextAsync(filename, geoJson);
    }
}