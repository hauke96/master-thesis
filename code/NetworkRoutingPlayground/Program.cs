using System.Globalization;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using NetworkRoutingPlayground.Layer;
using NetworkRoutingPlayground.Model;

namespace NetworkRoutingPlayground
{
    class Program
    {
        // TODO create config.json and load GeoJSON data for routing. Maybe the "HamburgBaseModel" helps.
        
        static void Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var description = new ModelDescription();
            description.AddLayer<NetworkLayer>();
            description.AddAgent<Agent, NetworkLayer>();

            var file = File.ReadAllText("config.json");
            var config = SimulationConfig.Deserialize(file);

            var task = SimulationStarter.Start(description, config);
            var loopResults = task.Run();

            Console.WriteLine($"Simulation execution finished after {loopResults.Iterations} steps");
        }
    }
}