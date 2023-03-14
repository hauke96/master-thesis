using System.Globalization;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using NetworkRoutingPlayground.Layer;
using NetworkRoutingPlayground.Model;
using Wavefront;

namespace NetworkRoutingPlayground
{
    class Program
    {
        static void Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Log.LogLevel = Log.DEBUG;

            var description = new ModelDescription();
            description.AddLayer<NetworkLayer>();
            description.AddLayer<AgentLayer>();
            description.AddAgent<Agent, AgentLayer>();

            var file = File.ReadAllText("config.json");
            var config = SimulationConfig.Deserialize(file);

            var task = SimulationStarter.Start(description, config);
            var loopResults = task.Run();

            Console.WriteLine($"Simulation execution finished after {loopResults.Iterations} steps");
        }
    }
}