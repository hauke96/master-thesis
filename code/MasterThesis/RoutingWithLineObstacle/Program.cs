using System.Globalization;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using RoutingWithLineObstacle.Layer;
using RoutingWithLineObstacle.Model;

namespace RoutingWithLineObstacle
{
    class Program
    {
        static void Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            SharedEnvironment.Init();
            Target.SetRandomPosition();

            var description = new ModelDescription();
            description.AddLayer<Layer.Layer>();
            description.AddLayer<ObstacleLayer>();
            description.AddAgent<Agent, Layer.Layer>();

            var file = File.ReadAllText("config.json");
            var config = SimulationConfig.Deserialize(file);

            var task = SimulationStarter.Start(description, config);
            var loopResults = task.Run();

            Console.WriteLine($"Simulation execution finished after {loopResults.Iterations} steps");
        }
    }
}