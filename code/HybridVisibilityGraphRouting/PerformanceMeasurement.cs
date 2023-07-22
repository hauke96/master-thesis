using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text;
using HybridVisibilityGraphRouting.IO;
using ServiceStack;

namespace HybridVisibilityGraphRouting;

public class PerformanceMeasurement
{
    public static bool IsActive = false;

    public const int DefaultIterationCount = 5;
    public const int DefaultWarmupCount = 3;

    public static Result? CurrentRun;

    public class Result : RawResult
    {
        private const string IterationNumberKey = "iteration_number";
        private const string IterationTimeKey = "iteration_time";

        private const string ObstacleCountInputKey = "obstacles_input";
        private const string ObstacleCountAfterUnwrappingKey = "obstacles_after_unwrapping";

        private const string ObstacleVerticesKey = "obstacle_vertices_input";
        private const string ObstacleVerticesAfterPreprocessingKey = "obstacle_vertices_after_unwrapping";

        private const string RoadVerticesKey = "road_vertices_input";
        private const string RoadVerticesAfterMergingKey = "road_vertices_after_merging";
        private const string RoadEdgesKey = "road_edges_input";
        private const string RoadEdgesAfterMergingKey = "road_edges_after_merging";

        private const string AllInputVerticesKey = "other_vertices_input";

        private const string VisibilityEdgesBeforeMergingKey = "visibility_edges_before_merging";
        private const string VisibilityEdgesAfterMergingKey = "visibility_edges_after_merging";

        public List<double> Iterations => Values[IterationTimeKey].Map(v => (double)v);

        public double MinMemoryBefore => _memUsageBefore.Min();
        public double MaxMemoryBefore => _memUsageBefore.Max();
        public double AvgMemoryBefore => _memUsageBefore.Average();
        public double MinMemoryAfter => _memUsageAfter.Min();
        public double MaxMemoryAfter => _memUsageAfter.Max();
        public double AvgMemoryAfter => _memUsageAfter.Average();

        public double ObstacleCountInput = -1;
        public double ObstacleCountAfterUnwrapping = -1;

        public double ObstacleVertices = -1;
        public double ObstacleVerticesAfterPreprocessing = -1;

        public double RoadVertices = -1;
        public double RoadVerticesAfterMerging = -1;
        public double RoadEdges = -1;
        public double RoadEdgesAfterMerging = -1;

        public double AllInputVertices = -1;

        public double VisibilityEdgesBeforeMerging = -1;
        public double VisibilityEdgesAfterMerging = -1;

        public double IterationCount => RowCount;
        public double TotalTime => Iterations.Sum();

        private readonly List<long> _memUsageBefore = new();
        private readonly List<long> _memUsageAfter = new();

        public Result(string name) : base(name)
        {
        }

        public void AddIteration(double iterationDuration, long memBeforeIteration, long memAfterIteration)
        {
            AddRow(new Dictionary<string, object>
            {
                { IterationNumberKey, IterationCount },
                { IterationTimeKey, iterationDuration },
            });
            _memUsageBefore.Add(memBeforeIteration);
            _memUsageAfter.Add(memAfterIteration);
        }

        /// <summary>
        /// This "closes" the result. It means the total number of vertices are added to all existing rows. This
        /// should therefore only be called once after the measurement.
        /// </summary>
        public void Close()
        {
            var rows = new Dictionary<string, List<object>>();

            ToColumn(rows, ObstacleCountInputKey, ObstacleCountInput);
            ToColumn(rows, ObstacleCountAfterUnwrappingKey, ObstacleCountAfterUnwrapping);

            ToColumn(rows, ObstacleVerticesKey, ObstacleVertices);
            ToColumn(rows, ObstacleVerticesAfterPreprocessingKey, ObstacleVerticesAfterPreprocessing);

            ToColumn(rows, RoadVerticesKey, RoadVertices);
            ToColumn(rows, RoadVerticesAfterMergingKey, RoadVerticesAfterMerging);
            ToColumn(rows, RoadEdgesKey, RoadEdges);
            ToColumn(rows, RoadEdgesAfterMergingKey, RoadEdgesAfterMerging);

            ToColumn(rows, AllInputVerticesKey, AllInputVertices);

            ToColumn(rows, VisibilityEdgesBeforeMergingKey, VisibilityEdgesBeforeMerging);
            ToColumn(rows, VisibilityEdgesAfterMergingKey, VisibilityEdgesAfterMerging);

            AddRows(rows);
        }

        private void ToColumn(Dictionary<string, List<object>> dict, string key, object value)
        {
            dict.Add(
                key,
                Enumerable.Range(0, RowCount)
                    .Map(x => ToString(value))
                    .Cast<object>()
                    .ToList()
            );
        }

        public override string ToString()
        {
            var minTime = Iterations.Min();
            var maxTime = Iterations.Max();
            var avgTime = Iterations.Average();

            return
                @$"Measurement '{Name}':
  Iterations: {IterationCount}
  Tot time  : {TotalTime}ms
  Time range: {minTime} - {maxTime} ms
  Avg time  : {avgTime}ms"
                ;
        }
    }

    public class RawResult
    {
        private const string NumberFormat = "0.###";

        protected readonly string Name;
        protected int RowCount;

        public readonly Dictionary<string, List<object>> Values;

        public RawResult(string name)
        {
            Name = name;
            Values = new Dictionary<string, List<object>>();
            RowCount = 0;
        }

        /// <summary>
        /// Adds a row entry for the given columns (represented as dictionary keys).
        /// </summary>
        public void AddRow(Dictionary<string, object> row, bool updateRowCount = true)
        {
            foreach (var key in row.Keys)
            {
                if (!Values.ContainsKey(key))
                {
                    Values[key] = new List<object>();
                }

                Values[key].Add(row[key]);
            }

            if (updateRowCount)
            {
                UpdateRowCount();
            }
        }

        protected void AddRows(Dictionary<string, List<object>> row, bool updateRowCount = true)
        {
            foreach (var key in row.Keys)
            {
                if (!Values.ContainsKey(key))
                {
                    Values[key] = new List<object>();
                }

                Values[key].AddRange(row[key]);
            }

            if (updateRowCount)
            {
                UpdateRowCount();
            }
        }

        private void UpdateRowCount()
        {
            var rowCounts = Values.Map(pair => pair.Value.Count).ToSet();
            if (rowCounts.Count != 1)
            {
                throw new Exception($"Row count mismatch, found {rowCounts.Count} different row counts: " +
                                    rowCounts.Join(","));
            }

            // All columns have the same amount of rows
            RowCount = rowCounts.First();
        }

        public void Print()
        {
            Console.WriteLine(ToString());
        }

        protected static String ToString(double number)
        {
            return number.ToString(NumberFormat, CultureInfo.InvariantCulture);
        }

        protected static String ToString(object o)
        {
            if (o is double d)
            {
                return ToString(d);
            }

            return o.ToString() ?? "" + o;
        }

        private string ToCsv()
        {
            var keys = Values.Keys.ToList();
            Log.D($"Create CSV for result \'{Name}\' with the following columns:");
            keys.Each(k => Log.D("  " + k));

            var stringBuilder = new StringBuilder();

            stringBuilder.Append(String.Join(",", keys));
            stringBuilder.Append("\n");

            for (var i = 0; i < RowCount; i++)
            {
                var rowNu = i;
                var valuesForRow = keys.Map(k => ToString(Values[k][rowNu]));

                stringBuilder.Append(String.Join(",", valuesForRow));
                stringBuilder.Append("\n");
            }

            return stringBuilder.ToString();
        }

        public async void WriteToFile()
        {
            await File.WriteAllTextAsync("performance_" + Name + ".csv", ToCsv());
        }
    }

    public static long TimestampBeforeGraphGeneration = 0;
    public static long TimestampAfterGraphGeneration = 0;
    public static long TimestampGraphGenerationGetObstacleStart = 0;
    public static long TimestampGraphGenerationObstacleNeighborsStart = 0;
    public static long TimestampGraphGenerationKNNStart = 0;
    public static long TimestampGraphGenerationCreateGraphStart = 0;
    public static long TimestampGraphGenerationMergePrepareStart = 0;
    public static long TimestampGraphGenerationMergeInsertStart = 0;
    public static long TimestampAfterAgentInit = 0;
    public static long TimestampAfterAgent = 0;

    public static void PrintTimestamps()
    {
        Console.WriteLine("Store the following to the according 'timestamps.csv' file:");
        Console.WriteLine();
        Console.WriteLine("name,time");
        Console.WriteLine($"before_graph_generation,{TimestampBeforeGraphGeneration}");
        Console.WriteLine($"graph_creation_get_obstacle_start,{TimestampGraphGenerationGetObstacleStart}");
        Console.WriteLine($"graph_creation_obstacle_neighbors_start,{TimestampGraphGenerationObstacleNeighborsStart}");
        Console.WriteLine($"graph_creation_knn_start,{TimestampGraphGenerationKNNStart}");
        Console.WriteLine($"graph_creation_create_graph_start,{TimestampGraphGenerationCreateGraphStart}");
        Console.WriteLine($"graph_creation_merge_prepare_start,{TimestampGraphGenerationMergePrepareStart}");
        Console.WriteLine($"graph_creation_merge_insert_start,{TimestampGraphGenerationMergeInsertStart}");
        Console.WriteLine($"after_graph_generation,{TimestampAfterGraphGeneration}");
        Console.WriteLine($"after_agent_init,{TimestampAfterAgentInit}");
        Console.WriteLine($"after_agent,{TimestampAfterAgent}");
        Console.WriteLine();
    }

    /// <summary>
    /// Prepares the measurement setup by setting process and thread parameter.
    /// See also:
    /// * https://www.codeproject.com/Articles/61964/Performance-Tests-Precise-Run-Time-Measurements-wi
    /// * https://stackoverflow.com/questions/1047218/benchmarking-small-code-samples-in-c-can-this-implementation-be-improved
    /// </summary>
    private static void Init()
    {
        // Uses the second Core or Processor for the Test
        Process.GetCurrentProcess().ProcessorAffinity = new IntPtr(2);
        Thread.CurrentThread.Priority = ThreadPriority.Highest;

        try
        {
            // On linux machines, root permissions are needed for this. To prevent unwanted exit, this catch just prints
            // a warning. Successfully setting high priority on this thread prevents "normal" processes from easily
            // interrupting this thread.
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
        }
        catch (Exception)
        {
            Log.W("Setting high priority on thread failed. Use normal priority.");
        }
    }

    /// <summary>
    /// Starts a new measurement. Running measurement will be canceled.
    ///
    /// Call the function to measure once before this call to warm up the CPU pipeline and caches.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GarbageCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>
    /// Resets and starts the stopwatch timer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StartTimer(Stopwatch stopwatch)
    {
        stopwatch.Reset();
        stopwatch.Start();
    }

    /// <summary>
    /// Stops the current measurement and returns the elapsed time since the last Start() call.
    /// </summary>
    /// <returns>The number of milliseconds of this measurement.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double StopTimer(Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    /// <summary>
    /// Gets the current memory usage of the working set.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetRamUsage()
    {
        Process.GetCurrentProcess().Refresh();
        return Process.GetCurrentProcess().WorkingSet64;
    }

    /// <summary>
    /// Executes the given function and measures its time. The result will be added to the <code>CurrentRun</code>
    /// result with <code>name</code> as key/column.
    /// </summary>
    public static double AddFunctionDurationToCurrentRun(Action func, string name = "")
    {
        var stopwatch = new Stopwatch();
        StartTimer(stopwatch);
        func();
        var time = StopTimer(stopwatch);

        if (IsActive && CurrentRun != null)
        {
            CurrentRun.AddRow(new Dictionary<string, object> { { name, time } }, false);
        }

        return time;
    }

    /// <summary>
    /// Starts a new performance measurement. The Field <code>CurrentRun</code> must not be set, i.e. an exception is
    /// thrown in case a measurement run is currently in progress. 
    /// </summary>
    public static Result NewMeasurementForFunction(Action func, string name = "",
        int iterationCount = DefaultIterationCount,
        int warmupCount = DefaultWarmupCount)
    {
        if (CurrentRun != null) throw new Exception("A measurement is already in progress, cannot start a new one.");

        var stopwatch = new Stopwatch();

        if (!IsActive)
        {
            StartTimer(stopwatch);
            func();
            var iterationDuration = StopTimer(stopwatch);
            var r = new Result(name);
            r.AddIteration(iterationDuration, 0, 0);
            CurrentRun = null;
            return r;
        }

        Log.D(
            $"Performance measurement {name}: Measure performance using {warmupCount} warmup iterations and {iterationCount} actual iterations");

        Init();

        // Actual run
        for (var i = 0; i < iterationCount + warmupCount; i++)
        {
            if (i == warmupCount)
            {
                // Warmup finished, this is the first normal iteration. Therefore the current run result is created.
                CurrentRun = new Result(name);
            }

            GarbageCollection();

            GC.TryStartNoGCRegion(256 * 1024 * 1024);
            var memBefore = GetRamUsage();
            StartTimer(stopwatch);
            func();
            var iterationDuration = StopTimer(stopwatch);
            var memAfter = GetRamUsage();
            if (GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
            {
                GC.EndNoGCRegion();
            }

            // Add result if warmup completed
            if (i >= warmupCount)
            {
                CurrentRun!.AddIteration(iterationDuration, memBefore, memAfter);
                Log.I(
                    $"Performance measurement {name}: Iteration {i - warmupCount + 1} / {iterationCount} done: {iterationDuration}ms");
            }
            else
            {
                Log.I(
                    $"Performance measurement {name}: Warmup iteration {i + 1} / {warmupCount} done: {iterationDuration}ms");
            }
        }

        CurrentRun!.Close();

        var result = CurrentRun;
        CurrentRun = null;
        return result;
    }
}