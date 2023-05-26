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

    public const int DefaultIterationCount = 10;
    public const int DefaultWarmupCount = 5;

    public static Result? CurrentRun;

    public class Result : RawResult
    {
        private const string IterationNumberKey = "iteration_number";
        private const string IterationTimeKey = "iteration_time";
        private const string MemoryKey = "memory";
        private const string TotalVerticesKey = "total_vertices";
        private const string TotalVerticesAfterPreprocessingKey = "total_vertices_after_preprocessing";

        private List<double> Iterations => Values[IterationTimeKey].Map(v => (double)v);

        private List<long> MemUsage => Values[MemoryKey].Map(v => (long)v);
        public double MinMemory => MemUsage.Min();
        public double MaxMemory => MemUsage.Max();
        public double AvgMemory => MemUsage.Average();

        public int TotalVertices = -1;
        public int TotalVerticesAfterPreprocessing = -1;

        public double IterationCount => RowCount;
        public double TotalTime => Iterations.Sum();

        public Result(string name) : base(name)
        {
        }

        public void AddIteration(double iterationDuration, long memBeforeIteration, long memAfterIteration)
        {
            AddRow(new Dictionary<string, object>
            {
                { IterationNumberKey, IterationCount },
                { IterationTimeKey, iterationDuration },
                { MemoryKey, memAfterIteration - memBeforeIteration },
            });
        }

        /// <summary>
        /// This "closes" the result. It means the total number of vertices are added to all existing rows. This
        /// should therefore only be called once after the measurement.
        /// </summary>
        public void Close()
        {
            var rowCount = RowCount;
            AddRows(new Dictionary<string, List<object>>
            {
                {
                    TotalVerticesKey,
                    Enumerable.Range(0, rowCount)
                        .Map(x => ToString(TotalVertices))
                        .Cast<object>()
                        .ToList()
                },
                {
                    TotalVerticesAfterPreprocessingKey,
                    Enumerable.Range(0, rowCount)
                        .Map(x => ToString(TotalVerticesAfterPreprocessing))
                        .Cast<object>()
                        .ToList()
                }
            });
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
  Avg time  : {avgTime}ms
  Mem range : {MinMemory} - {MaxMemory} bytes
  Avg mem   : {AvgMemory} bytes"
                ;
        }
    }

    public class RawResult
    {
        private const string NumberFormat = "0.###";

        protected readonly string Name;
        protected readonly Dictionary<string, List<object>> Values;
        protected int RowCount;

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