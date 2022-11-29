using System.Diagnostics;
using System.Text;
using ServiceStack;
using ServiceStack.Text;

namespace Wavefront;

public class PerformanceMeasurement
{
    public class Result
    {
        private List<double> _iterations;
        private string _name;

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

        public string ToCsv()
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append("iteration,total_time,min_time,max_time,avg_time,spread,spread_percent");
            foreach (var iteration in _iterations)
            {
                stringBuilder.Append(String.Join(",",
                    new List<double> { iteration, TotalTime, MinTime, MaxTime, AverageTime, Spread, SpreadPercent }));
            }

            return stringBuilder.ToString();
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
    public static void Init()
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
    public static void Start()
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
    public static double Stop()
    {
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    public static Result ForFunction(Action func, string name = "", int iterationCount = 10)
    {
        Result result = new Result(name);
        Init();

        // Warmup
        for (var i = 0; i < 5; i++)
        {
            Start();
            func();
            Stop();
        }


        // Actual run
        for (var i = 0; i < iterationCount; i++)
        {
            Start();
            func();
            var iterationDuration = Stop();
            result.AddIteration(iterationDuration);
            Console.WriteLine($"Iteration {i}: {iterationDuration}ms");
        }

        return result;
    }
}