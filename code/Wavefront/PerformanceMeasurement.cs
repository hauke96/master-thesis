using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using ServiceStack;

namespace Wavefront;

public class PerformanceMeasurement
{
    public static bool IS_ACTIVE = false;

    public class Result
    {
        private static string NUMBER_FORMAT = "0.###";

        private readonly List<double> _iterations;
        private readonly string _name;

        private double IterationCount => _iterations.Count;
        private double TotalTime => _iterations.Sum();
        private double MinTime => !_iterations.IsEmpty() ? _iterations.Min() : 0;
        private double MaxTime => !_iterations.IsEmpty() ? _iterations.Max() : double.PositiveInfinity;
        private double AverageTime => TotalTime / IterationCount;
        private double Spread => MaxTime - MinTime;
        private double SpreadPercent => 100 - MinTime / MaxTime * 100;

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

            stringBuilder.Append("iteration_number," +
                                 "iteration_time," +
                                 "total_time," +
                                 "min_time," +
                                 "max_time," +
                                 "avg_time," +
                                 "spread," +
                                 "spread_percent" +
                                 "\n");
            for (var i = 0; i < _iterations.Count; i++)
            {
                var iteration = _iterations[i];

                stringBuilder.Append(String.Join(",",
                    new List<object>
                    {
                        i,
                        ToString(iteration),
                        ToString(TotalTime),
                        ToString(MinTime),
                        ToString(MaxTime),
                        ToString(AverageTime),
                        ToString(Spread),
                        ToString(SpreadPercent)
                    }));
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

        // On linux machines, root permissions are needed for this:
        // Prevents "Normal" processes from interrupting Threads
        // Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
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

    public static Result ForFunction(Action func, string name = "", int iterationCount = 10, int warmupCount = 5)
    {
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