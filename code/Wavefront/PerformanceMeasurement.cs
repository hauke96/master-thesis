using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Mars.Common.Core;
using ServiceStack;

namespace Wavefront;

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
        private readonly string _name;

        public double IterationCount => _iterations.Count;
        public double TotalTime => _iterations.Sum();
        public double MinTime => !_iterations.IsEmpty() ? _iterations.Min() : 0;
        public double MaxTime => !_iterations.IsEmpty() ? _iterations.Max() : double.PositiveInfinity;
        public double AverageTime => TotalTime / IterationCount;
        public double Spread => MaxTime - MinTime;
        public double SpreadPercent => 100 - MinTime / MaxTime * 100;

        public Result(string name)
        {
            _iterations = new();
            _name = name;
        }

        public void AddIteration(double iterationDuration)
        {
            _iterations.Add(iterationDuration);
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
                "spread",
                "spread_percent",
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
                    ToString(Spread),
                    ToString(SpreadPercent),
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
  Min time  : {MinTime}ms
  Max time  : {MaxTime}ms
  Avg time  : {AverageTime}ms
  Max - Min : {Spread}ms -> {SpreadPercent}%";
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
    private static void Start()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        stopwatch.Reset();
        stopwatch.Start();
    }

    /// <summary>
    /// Stops the current measurement and returns the elapsed time since the last Start() call.
    /// </summary>
    /// <returns>The number of milliseconds of this measurement.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Stop()
    {
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    public static Result ForFunction(Action func, string name = "", int iterationCount = -1, int warmupCount = -1)
    {
        if (iterationCount == -1)
        {
            iterationCount = DEFAULT_ITERATION_COUNT;
        }

        if (warmupCount == -1)
        {
            warmupCount = DEFAULT_WARMUP_COUNT;
        }


        Result result = new Result(name);

        if (!IS_ACTIVE)
        {
            func();
            return result;
        }

        Init();

        // Actual run
        for (var i = 0; i < iterationCount + warmupCount; i++)
        {
            Start();
            func();
            var iterationDuration = Stop();

            // Add result if warmup completed
            if (i >= warmupCount)
            {
                result.AddIteration(iterationDuration);
            }

            Log.D($"Iteration {i}{(i < warmupCount ? "(warmup)" : "")}: {iterationDuration}ms");
        }

        return result;
    }
}