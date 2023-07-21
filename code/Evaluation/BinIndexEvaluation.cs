using System;
using System.Collections.Generic;
using System.Diagnostics;
using HybridVisibilityGraphRouting.Geometry;
using HybridVisibilityGraphRouting.Index;
using NetTopologySuite.Algorithm;
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
        for (var i = 0; i < 1_000_000; i++)
        {
            intervals.Add((random.NextDouble()*100, random.NextDouble()*100));
        }

        Console.WriteLine("Start evaluating");
        var stopwatch = new Stopwatch();

        var numberOfWarmups = 2;
        var numberOfEvaluations = 3;
        Console.WriteLine("Start iterations");
        for (int i = 0; i < numberOfWarmups+numberOfEvaluations; i++)
        {
            Console.WriteLine($"Iteration: {i-numberOfWarmups} / {numberOfEvaluations}");
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
        Console.WriteLine($"  Step: {name}");
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
        Console.WriteLine($"  Step: {name}");
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
        Console.WriteLine($"  Step: {name}");
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
        Console.WriteLine($"  Step: {name}");
        var f = 100;
        stopwatch.Start();
        for (var i = 0; i < intervals.Count/f - 1; i++)
        {
            // binTree.Query(intervals[i].Item1);
            binTree.Query((intervals[i].Item1+intervals[i].Item2)/2);
            // binTree.Query(intervals[i].Item2);
        }
        stopwatch.Stop();
        // Console.WriteLine($"{name} : {stopwatch.ElapsedMilliseconds}");
        measurements[name] = measurements.TryGetValue(name, out measurement) ? measurement + GetMicroseconds(stopwatch.ElapsedTicks*f) : GetMicroseconds(stopwatch.ElapsedTicks*f);
        stopwatch.Reset();
    }

    private static long GetMicroseconds(long ticks)
    {
        return (long)(ticks / (double)Stopwatch.Frequency * 1_000_000d);
    }
}