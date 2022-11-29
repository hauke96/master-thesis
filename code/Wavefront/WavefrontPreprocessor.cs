using System.Diagnostics;
using Mars.Common;
using Mars.Common.Collections;
using Mars.Common.Core;
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
    /// Calculates the neighbor relationship for each vertex in the given obstacles.
    ///
    /// Neighbor here means across obstacles.
    /// </summary>
    /// <returns>A dict from position to a duplicate free list of all neighbors found.</returns>
    public static Dictionary<Position, List<Position>> GetNeighborsFromObstacleVertices(List<Obstacle> obstacles)
    {
        // A function that determines if any obstacles if between the two given coordinates.
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

        if (!PerformanceMeasurement.IS_ACTIVE)
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

    public static Dictionary<Vertex, List<Vertex>> CalculateVisibleKnn(QuadTree<Obstacle> obstacles,
        List<Vertex> vertices,
        int neighborCount)
    {
        var result = new Dictionary<Vertex, List<Vertex>>();
        Log.I($"Calculate nearest {neighborCount} visible neighbors for each vertex");

        var i = 1;
        var verticesPerPercent = vertices.Count / 100d;
        var nextProcessOutput = verticesPerPercent;
        var stopWatch = new Stopwatch();
        foreach (var vertex in vertices)
        {
            if (i > nextProcessOutput)
            {
                Log.I($"  {(int)(i / verticesPerPercent)}% done ({stopWatch.ElapsedMilliseconds}ms)");
                stopWatch.Restart();
                nextProcessOutput += verticesPerPercent;
            }

            i++;

            result[vertex] = GetVisibleNeighborsForVertex(obstacles, new List<Vertex>(vertices), vertex, neighborCount);
        }

        var vertexPositionDict = new Dictionary<Position, HashSet<Position>>();
        result.Keys.Each(vertex =>
        {
            var visibleNeighbors = result[vertex];
            var visibleNeighborPositions = visibleNeighbors.Map(v => v.Position);
            vertexPositionDict[vertex.Position] = new HashSet<Position>(visibleNeighborPositions);
        });

        if (!PerformanceMeasurement.IS_ACTIVE)
        {
            WriteVertexNeighborsToFile(vertexPositionDict, "vertex-visibility.geojson");
        }

        return result;
    }

    public static List<Vertex> GetVisibleNeighborsForVertex(QuadTree<Obstacle> obstacles, List<Vertex> vertices,
        Vertex vertex,
        int neighborCount)
    {
        var neighborList = new List<Vertex>();

        var shadowAreas = new BinIndex<AngleArea>(360);

        // [0] = Angle from
        // [1] = Angle to
        // [2] = Distance
        var obstaclesCastingShadow = new HashSet<Obstacle>();

        vertices.Remove(vertex);
        var sortedVertices = vertices
            .OrderBy(v => Distance.Euclidean(vertex.Position.PositionArray, v.Position.PositionArray)).ToList();

        for (var i = 0; i < sortedVertices.Count && neighborList.Count < neighborCount; i++)
        {
            var otherVertex = sortedVertices[i];
            if (Equals(otherVertex, vertex))
            {
                continue;
            }

            var angle = Angle.GetBearing(vertex.Position, otherVertex.Position);
            var distanceToOtherVertex =
                Distance.Euclidean(vertex.Position.PositionArray, otherVertex.Position.PositionArray);
            var isInShadowArea = IsInShadowArea(shadowAreas, angle, distanceToOtherVertex);
            if (isInShadowArea)
            {
                continue;
            }

            var envelope = new Envelope(vertex.Coordinate, otherVertex.Coordinate);
            var intersectsWithObstacle = false;
            obstacles.Query(envelope, (Action<Obstacle>)(obstacle =>
            {
                if (intersectsWithObstacle)
                {
                    return;
                }

                if (!obstaclesCastingShadow.Contains(obstacle))
                {
                    var (angleFrom, angleTo, distance) = obstacle.GetAngleAreaOfObstacle(vertex);
                    shadowAreas.Add(angleFrom, angleTo, new AngleArea(angleFrom, angleTo, distance));

                    obstaclesCastingShadow.Add(obstacle);
                }

                if (!intersectsWithObstacle)
                {
                    intersectsWithObstacle |= obstacle.CanIntersect(envelope) &&
                                              obstacle.IntersectsWithLine(vertex.Coordinate, otherVertex.Coordinate);
                }
            }));

            if (!intersectsWithObstacle)
            {
                neighborList.Add(otherVertex);
            }
        }

        return neighborList;
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

    private static async void WriteVertexNeighborsToFile(Dictionary<Position, HashSet<Position>> positionToNeighbors, string filename = "vertex-neighbors.geojson")
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