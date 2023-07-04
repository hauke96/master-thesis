using System;
using System.Collections.Generic;
using System.Diagnostics;
using HybridVisibilityGraphRouting.Geometry;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;

namespace Evaluation;

public class LineSegmentIntersectionEvaluation
{
    public static void Run()
    {
        var measurements = new Dictionary<string,long>();

        Console.WriteLine("Create line segments");
        var segments = new List<LineSegment>();
        for (var i = 0; i < 10_000_000; i++)
        {
            segments.Add(NewLineSegment(i));
        }

        Console.WriteLine("Start evaluating");
        var stopwatch = new Stopwatch();

        var numberOfWarmups = 3;
        var numberOfEvaluations = 5;
        for (int i = 0; i < numberOfWarmups+numberOfEvaluations; i++)
        {
            Console.WriteLine("==========");
            Console.WriteLine("");
            Console.WriteLine("Start iteration");
            Console.WriteLine("");
            PerformEvaluations(stopwatch, segments, measurements);
            if (i == numberOfWarmups - 1)
            {
                Console.WriteLine("==========");
                Console.WriteLine("");
                Console.WriteLine("Warmup completed");
                measurements = new Dictionary<string,long>();
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

    private static void PerformEvaluations(Stopwatch stopwatch, List<LineSegment> segments, Dictionary<string, long> measurements)
    {
        string name;
        long measurement;
        
        //
        // Own implementation
        //
        name = "Own intersection implementation";
        stopwatch.Start();
        for (var i = 0; i < segments.Count - 1; i++)
        {
            Intersect.DoIntersectOrTouch(segments[i].P0, segments[i].P1, segments[i + 1].P0, segments[i + 1].P1);
        }
        stopwatch.Stop();
        Console.WriteLine($"{name} : {stopwatch.ElapsedMilliseconds}");
        measurements[name] = measurements.TryGetValue(name, out measurement) ? measurement + stopwatch.ElapsedMilliseconds : stopwatch.ElapsedMilliseconds;
        stopwatch.Reset();
        
        //
        // LineSegment.Intersection() (internally uses RobustLineIntersector)
        //
        name = "LineSegment.Intersection() ....";
        stopwatch.Start();
        for (var i = 0; i < segments.Count - 1; i++)
        {
            segments[i].Intersection(segments[i + 1]);
        }
        stopwatch.Stop();
        Console.WriteLine($"{name} : {stopwatch.ElapsedMilliseconds}");
        measurements[name] = measurements.TryGetValue(name, out measurement) ? measurement + stopwatch.ElapsedMilliseconds : stopwatch.ElapsedMilliseconds;
        stopwatch.Reset();

        //
        // RobustLineIntersector
        //
        name = "RobustLineIntersector .........";
        stopwatch.Start();
        var robustLineIntersector = new RobustLineIntersector();
        for (var i = 0; i < segments.Count - 1; i++)
        {
            robustLineIntersector.ComputeIntersection(segments[i].P0, segments[i].P1, segments[i + 1].P0,
                segments[i + 1].P1);
        }
        stopwatch.Stop();
        Console.WriteLine($"{name} : {stopwatch.ElapsedMilliseconds}");
        measurements[name] = measurements.TryGetValue(name, out measurement) ? measurement + stopwatch.ElapsedMilliseconds : stopwatch.ElapsedMilliseconds;
        stopwatch.Reset();

        //
        // IntersectionComputer
        //
        name = "IntersectionComputer ..........";
        stopwatch.Start();
        for (var i = 0; i < segments.Count - 1; i++)
        {
            IntersectionComputer.Intersection(segments[i].P0, segments[i].P1, segments[i + 1].P0, segments[i + 1].P1);
        }
        stopwatch.Stop();
        Console.WriteLine($"{name} : {stopwatch.ElapsedMilliseconds}");
        measurements[name] = measurements.TryGetValue(name, out measurement) ? measurement + stopwatch.ElapsedMilliseconds : stopwatch.ElapsedMilliseconds;
        stopwatch.Reset();

        // //
        // // PreparedLineString.Intersects()
        // //
        // name = "PreparedLineString.Intersects()";
        // stopwatch.Start();
        // for (var i = 0; i < segments.Count - 1; i++)
        // {
        //     new PreparedLineString(new LineString(new[] { segments[i].P0, segments[i].P1 })).Intersects(
        //         new LineString(new[] { segments[i + 1].P0, segments[i + 1].P1 }));
        // }
        // stopwatch.Stop();
        // Console.WriteLine($"{name} : {stopwatch.ElapsedMilliseconds}");
        // measurements[name] = measurements.TryGetValue(name, out measurement) ? measurement + stopwatch.ElapsedMilliseconds : stopwatch.ElapsedMilliseconds;
        // stopwatch.Reset();

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

    public static LineSegment NewLineSegment(int seed)
    {
        var r = new Random(seed);
        return new LineSegment(r.NextDouble(), r.NextDouble(), r.NextDouble(), r.NextDouble());
    }
}