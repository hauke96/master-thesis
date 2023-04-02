using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text;
using ServiceStack;

namespace HybridVisibilityGraphRouting;

public class PerformanceMeasurement
{
    public static bool IS_ACTIVE = false;
    public static int DEFAULT_ITERATION_COUNT = 10;
    public static int DEFAULT_WARMUP_COUNT = 5;

    public static int TOTAL_VERTICES = -1;
    public static int TOTAL_VERTICES_AFTER_PREPROCESSING = -1;

    public class Result
    {
        private static string NUMBER_FORMAT = "0.###";

        private readonly List<double> _iterations;
        private readonly List<long> _memUsage;
        private readonly string _name;

        public double IterationCount => _iterations.Count;
        public double TotalTime => _iterations.Sum();
        public double MinTime => !_iterations.IsEmpty() ? _iterations.Min() : 0;
        public double MaxTime => !_iterations.IsEmpty() ? _iterations.Max() : 0;
        public double AverageTime => TotalTime / IterationCount;
        public long MinMemory => !_memUsage.IsEmpty() ? _memUsage.Min() : 0;
        public long MaxMemory => !_memUsage.IsEmpty() ? _memUsage.Max() : 0;
        public double AverageMemory => _memUsage.Sum() / IterationCount;

        public Result(string name)
        {
            _iterations = new List<double>();
            _memUsage = new List<long>();
            _name = name;
        }

        public void AddIteration(double iterationDuration, long memBeforeIteration, long memAfterIteration)
        {
            _iterations.Add(iterationDuration);
            _memUsage.Add(memAfterIteration - memBeforeIteration);
        }

        public void Print()
        {
            if (_iterations.Count > 0)
            {
                Console.WriteLine(ToString());
            }
        }

        public string ToCsv()
        {
            var stringBuilder = new StringBuilder();

            var propertyNames = new List<string>
            {
                "iteration_number",
                "iteration_time",
                "total_time",
                "min_time",
                "max_time",
                "avg_time",
                "min_mem",
                "max_mem",
                "avg_mem",
                "total_vertices",
                "total_vertices_after_preprocessing"
            };

            stringBuilder.Append(String.Join(",", propertyNames));
            stringBuilder.Append("\n");

            for (var i = 0; i < _iterations.Count; i++)
            {
                var iteration = _iterations[i];

                var propertyValues = new List<object>
                {
                    i,
                    ToString(iteration),
                    ToString(TotalTime),
                    ToString(MinTime),
                    ToString(MaxTime),
                    ToString(AverageTime),
                    ToString(MinMemory),
                    ToString(MaxMemory),
                    ToString(AverageMemory),
                    ToString(TOTAL_VERTICES),
                    ToString(TOTAL_VERTICES_AFTER_PREPROCESSING)
                };

                stringBuilder.Append(String.Join(",", propertyValues));
                stringBuilder.Append("\n");
            }

            return stringBuilder.ToString();
        }

        public async void WriteToFile()
        {
            await File.WriteAllTextAsync("performance_" + _name + ".csv", ToCsv());
        }

        private static String ToString(double number)
        {
            return number.ToString(NUMBER_FORMAT, System.Globalization.CultureInfo.InvariantCulture);
        }

        public override string ToString()
        {
            return
                @$"Measurement '{_name}':
  Iterations: {IterationCount}
  Tot time  : {TotalTime}ms
  Time range: {MinTime} - {MaxTime} ms
  Avg time  : {AverageTime}ms
  Mem range : {MinMemory} - {MaxMemory} bytes
  Avg mem   : {AverageMemory} bytes"
                ;
        }
    }

    public class RawResult
    {
        private readonly string _name;
        private readonly Dictionary<string, List<object>> _values;

        private int _rowCount;

        public RawResult(string name)
        {
            _name = name;
            _values = new Dictionary<string, List<object>>();
            _rowCount = 0;
        }

        public void AddRow(Dictionary<string, string> row)
        {
            foreach (var key in row.Keys)
            {
                if (!_values.ContainsKey(key))
                {
                    _values[key] = new List<object>();
                }

                _values[key].Add(row[key]);
            }

            _rowCount++;
        }

        public void Print()
        {
            Console.WriteLine(ToString());
        }

        public string ToCsv()
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append(String.Join(",", _values.Keys));
            stringBuilder.Append("\n");

            for (var i = 0; i < _rowCount; i++)
            {
                var rowNu = i;
                var valuesForRow = _values.Keys.Map(k => _values[k][rowNu].ToString());

                stringBuilder.Append(String.Join(",", valuesForRow));
                stringBuilder.Append("\n");
            }

            return stringBuilder.ToString();
        }

        public async void WriteToFile()
        {
            await File.WriteAllTextAsync("performance_" + _name + ".csv", ToCsv());
        }
    }

    private static Stopwatch stopwatch = new();

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
            Log.I("WARN: Setting high priority on thread failed. Use normal priority.");
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
    private static void StartTimer()
    {
        stopwatch.Reset();
        stopwatch.Start();
    }

    /// <summary>
    /// Stops the current measurement and returns the elapsed time since the last Start() call.
    /// </summary>
    /// <returns>The number of milliseconds of this measurement.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double StopTimer()
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

    public static Result ForFunction(Action func, string name = "", int iterationCount = -1, int warmupCount = -1)
    {
        Result result = new Result(name);
        
        if (!IS_ACTIVE)
        {
            func();
            return result;
        }
        
        if (iterationCount == -1)
        {
            iterationCount = DEFAULT_ITERATION_COUNT;
        }

        if (warmupCount == -1)
        {
            warmupCount = DEFAULT_WARMUP_COUNT;
        }
        
        Log.D($"Performance measurement {name}: Measure performance using {warmupCount} warmup iterations and {iterationCount} actual iterations");

        Init();

        // Actual run
        for (var i = 0; i < iterationCount + warmupCount; i++)
        {
            GarbageCollection();

            GC.TryStartNoGCRegion(256 * 1024 * 1024);
            var memBefore = GetRamUsage();
            StartTimer();
            func();
            var iterationDuration = StopTimer();
            var memAfter = GetRamUsage();
            if (GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
            {
                GC.EndNoGCRegion();
            }

            // Add result if warmup completed
            if (i >= warmupCount)
            {
                result.AddIteration(iterationDuration, memBefore, memAfter);
                Log.I($"Performance measurement {name}: Iteration {i - warmupCount} / {iterationCount} done: {iterationDuration}ms");
            }
            else
            {
                Log.I($"Performance measurement {name}: Warmup iteration {i} / {warmupCount} done: {iterationDuration}ms");
            }
        }

        return result;
    }
}