using System;
using System.Collections.Generic;
using System.Diagnostics;
using HybridVisibilityGraphRouting.Geometry;
using NetTopologySuite.Algorithm.Locate;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;

namespace Evaluation;

public class TriangleIntersectionEvaluation
{
    public static void Run()
    {
        var measurements = new Dictionary<string, long>();

        Console.WriteLine("Create line segments");
        var triangles = new List<(Polygon, Point)>();
        for (var i = 0; i < 1_000_000; i++)
        {
            triangles.Add(NewTriangleAndPoint(i));
        }

        Console.WriteLine("Start evaluating");
        var stopwatch = new Stopwatch();

        var numberOfWarmups = 3;
        var numberOfEvaluations = 5;
        for (int i = 0; i < numberOfWarmups + numberOfEvaluations; i++)
        {
            Console.WriteLine("==========");
            Console.WriteLine("");
            Console.WriteLine("Start iteration");
            Console.WriteLine("");
            PerformEvaluations(stopwatch, triangles, measurements);
            if (i == numberOfWarmups - 1)
            {
                Console.WriteLine("==========");
                Console.WriteLine("");
                Console.WriteLine("Warmup completed");
                measurements = new Dictionary<string, long>();
            }

            Console.WriteLine("");
        }

        Console.WriteLine("==========");
        Console.WriteLine("Results:");
        Console.WriteLine("");
        foreach (var measurement in measurements)
        {
            Console.WriteLine($"{measurement.Key} : {measurement.Value / numberOfEvaluations} ms");
        }
    }

    private static void PerformEvaluations(Stopwatch stopwatch, List<(Polygon, Point)> triangles,
        Dictionary<string, long> measurements)
    {
        string name;
        long measurement;

        //
        // Polygon.Intersects
        //
        name = "Polygon.Intersects() ..........";
        stopwatch.Start();
        for (var i = 0; i < triangles.Count - 1; i++)
        {
            triangles[i].Item1.Intersects(triangles[i].Item2);
        }

        stopwatch.Stop();
        Console.WriteLine($"{name} : {stopwatch.ElapsedMilliseconds}");
        measurements[name] = measurements.TryGetValue(name, out measurement)
            ? measurement + stopwatch.ElapsedMilliseconds
            : stopwatch.ElapsedMilliseconds;
        stopwatch.Reset();

        //
        // Triangle.Intersects
        //
        name = "Triangle.Intersects() .........";
        stopwatch.Start();
        for (var i = 0; i < triangles.Count - 1; i++)
        {
            Triangle.Intersects(
                triangles[i].Item1.Coordinates[0],
                triangles[i].Item1.Coordinates[1],
                triangles[i].Item1.Coordinates[2],
                triangles[i].Item2.Coordinate
            );
        }

        stopwatch.Stop();
        Console.WriteLine($"{name} : {stopwatch.ElapsedMilliseconds}");
        measurements[name] = measurements.TryGetValue(name, out measurement)
            ? measurement + stopwatch.ElapsedMilliseconds
            : stopwatch.ElapsedMilliseconds;
        stopwatch.Reset();

        //
        // IndexedPointInAreaLocator
        //
        name = "IndexedPointInAreaLocator .....";
        stopwatch.Start();
        for (var i = 0; i < triangles.Count - 1; i++)
        {
            new IndexedPointInAreaLocator(triangles[i].Item1).Locate(triangles[i].Item2.Coordinate);
        }

        stopwatch.Stop();
        Console.WriteLine($"{name} : {stopwatch.ElapsedMilliseconds}");
        measurements[name] = measurements.TryGetValue(name, out measurement)
            ? measurement + stopwatch.ElapsedMilliseconds
            : stopwatch.ElapsedMilliseconds;
        stopwatch.Reset();

        //
        // Own implementation
        //
        name = "Own intersection implementation";
        stopwatch.Start();
        for (var i = 0; i < triangles.Count - 1; i++)
        {
            Obstacle.IsInTriangle(
                triangles[i].Item1.Coordinates[0],
                triangles[i].Item1.Coordinates[1],
                triangles[i].Item1.Coordinates[2],
                triangles[i].Item2.Coordinate
            );
        }

        stopwatch.Stop();
        Console.WriteLine($"{name} : {stopwatch.ElapsedMilliseconds}");
        measurements[name] = measurements.TryGetValue(name, out measurement)
            ? measurement + stopwatch.ElapsedMilliseconds
            : stopwatch.ElapsedMilliseconds;
        stopwatch.Reset();

        //
        // IPreparedGeometry.Intersects()
        //
        name = "IPreparedGeometry.Intersects() ";
        stopwatch.Start();
        var preparedGeometryFactory = new PreparedGeometryFactory();
        for (var i = 0; i < triangles.Count - 1; i++)
        {
            var preparedGeometry = preparedGeometryFactory.Create(triangles[i].Item1);
            preparedGeometry.Intersects(triangles[i].Item2);
        }
        stopwatch.Stop();
        Console.WriteLine($"{name} : {stopwatch.ElapsedMilliseconds}");
        measurements[name] = measurements.TryGetValue(name, out measurement) ? measurement + stopwatch.ElapsedMilliseconds : stopwatch.ElapsedMilliseconds;
        stopwatch.Reset();

        // //
        // // LineString.Intersects()
        // //
        // stopwatch.Start();
        // for (var i = 0; i < segments.Count-1; i++)
        // {
        //     new LineString(new []{segments[i].P0, segments[i].P1}).Intersects(new LineString(new []{segments[i+1].P0, segments[i+1].P1}));
        // }
        // stopwatch.Stop();
        // Console.WriteLine($"{name} : {stopwatch.ElapsedMilliseconds}");
        // measurements[name] = measurements.TryGetValue(name, out measurement) ? measurement + stopwatch.ElapsedMilliseconds : stopwatch.ElapsedMilliseconds;
        // stopwatch.Reset();
    }

    public static (Polygon, Point) NewTriangleAndPoint(int seed)
    {
        var r = new Random(seed);
        var first = new Coordinate(r.NextDouble(), r.NextDouble());
        return (
            new Polygon(
                new LinearRing(
                    new[]
                    {
                        first,
                        new Coordinate(r.NextDouble(), r.NextDouble()),
                        new Coordinate(r.NextDouble(), r.NextDouble()),
                        first
                    })
            ),
            new Point(r.NextDouble(), r.NextDouble())
        );
    }
}