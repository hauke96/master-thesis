using System;
using System.Collections.Generic;
using System.Diagnostics;
using HybridVisibilityGraphRouting.Geometry;
using HybridVisibilityGraphRouting.Index;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Bintree;

namespace Evaluation;

public class BinIndexEvaluation
{
    public static void Run()
    {
        var measurements = new Dictionary<string,long>();

        Console.WriteLine("Create intervals");
        var intervals = new List<(double, double)>();
        var random = new Random();
        for (var i = 0; i < 100_000; i++)
        {
            intervals.Add((random.NextDouble()*100, random.NextDouble()*100));
        }

        Console.WriteLine("Start evaluating");
        var stopwatch = new Stopwatch();

        var numberOfWarmups = 3;
        var numberOfEvaluations = 10;
        Console.WriteLine("Start iterations");
        for (int i = 0; i < numberOfWarmups+numberOfEvaluations; i++)
        {
            PerformEvaluations(stopwatch, intervals, measurements);
            if (i == numberOfWarmups - 1)
            {
                Console.WriteLine("Warmup completed");
                measurements = new Dictionary<string,long>();
            }
        }

        Console.WriteLine("Iterations completed");
        Console.WriteLine("Results:");
        Console.WriteLine("");
        foreach (var measurement in measurements)
        {
            Console.WriteLine($"{measurement.Key} : {measurement.Value / (double)numberOfEvaluations / 1000d} ms");
        }
    }

    private static void PerformEvaluations(Stopwatch stopwatch, List<(double, double)> intervals, Dictionary<string, long> measurements)
    {
        string name;
        long measurement;
        
        //
        // Own implementation
        //
        name = "BinIndex (filling)";
        stopwatch.Start();
        var binIndex = new BinIndex<(double, double)>(100);
        for (var i = 0; i < intervals.Count - 1; i++)
        {
            binIndex.Add(intervals[i].Item1, intervals[i].Item2, intervals[i]);
        }
        stopwatch.Stop();
        // Console.WriteLine($"{name} : {stopwatch.ElapsedMilliseconds}");
        measurements[name] = measurements.TryGetValue(name, out measurement) ? measurement + GetMicroseconds(stopwatch.ElapsedTicks) : GetMicroseconds(stopwatch.ElapsedTicks);
        stopwatch.Reset();
        
        name = "BinIndex (querying)";
        stopwatch.Start();
        for (var i = 0; i < intervals.Count - 1; i++)
        {
            binIndex.Query(intervals[i].Item1);
            binIndex.Query((intervals[i].Item1+intervals[i].Item2)/2);
            binIndex.Query(intervals[i].Item2);
        }
        stopwatch.Stop();
        // Console.WriteLine($"{name} : {stopwatch.ElapsedMilliseconds}");
        measurements[name] = measurements.TryGetValue(name, out measurement) ? measurement + GetMicroseconds(stopwatch.ElapsedTicks) : GetMicroseconds(stopwatch.ElapsedTicks);
        stopwatch.Reset();
        
        //
        // BinTree
        //
        name = "Bintree (filling)";
        stopwatch.Start();
        var binTree = new Bintree<(double, double)>();
        for (var i = 0; i < intervals.Count - 1; i++)
        {
            binTree.Insert(new Interval(intervals[i].Item1, intervals[i].Item2), intervals[i]);
        }
        stopwatch.Stop();
        // Console.WriteLine($"{name} : {stopwatch.ElapsedMilliseconds}");
        measurements[name] = measurements.TryGetValue(name, out measurement) ? measurement + GetMicroseconds(stopwatch.ElapsedTicks) : GetMicroseconds(stopwatch.ElapsedTicks);
        stopwatch.Reset();
        
        name = "Bintree (querying)";
        stopwatch.Start();
        for (var i = 0; i < intervals.Count/100 - 1; i++)
        {
            binTree.Query(intervals[i].Item1);
            binTree.Query((intervals[i].Item1+intervals[i].Item2)/2);
            binTree.Query(intervals[i].Item2);
        }
        stopwatch.Stop();
        // Console.WriteLine($"{name} : {stopwatch.ElapsedMilliseconds}");
        measurements[name] = measurements.TryGetValue(name, out measurement) ? measurement + GetMicroseconds(stopwatch.ElapsedTicks*100) : GetMicroseconds(stopwatch.ElapsedTicks*100);
        stopwatch.Reset();
    }

    private static int GetMicroseconds(long ticks)
    {
        return (int)(ticks / (double)Stopwatch.Frequency * 1_000_000);
    }

    public static LineSegment NewLineSegment(int seed)
    {
        var r = new Random(seed);
        return new LineSegment(r.NextDouble(), r.NextDouble(), r.NextDouble(), r.NextDouble());
    }
}