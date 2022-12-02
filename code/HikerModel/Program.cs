using System;
using System.IO;
using HikerModel.Model;
using Mars.Components.Starter;
using Mars.Interfaces.Model;
using Wavefront;

namespace HikerModel
{
    internal static class Program
    {
        private static void Main()
        {
            try
            {
                // PerformanceMeasurement.IS_ACTIVE = true;
                Log.LogLevel = Log.DEBUG;
                
                // the scenario consist of the model (represented by the model description)
                // an the simulation configuration (see config.json)

                // Create a new model description that holds all parts of the model
                var description = new ModelDescription();
                description.AddLayer<HikerLayer>();
                description.AddLayer<WaypointLayer>();
                description.AddLayer<ObstacleLayer>();
                description.AddAgent<Hiker, HikerLayer>();

                // scenario definition
                // use config.json that provides the specification of the scenario
                var file = File.ReadAllText("config.json");
                var config = SimulationConfig.Deserialize(file);

                // Create simulation task accordingly
                var starter = SimulationStarter.Start(description, config);
                // Run simulation
                var handle = starter.Run();
                // Feedback to user that simulation run was successful
                Console.WriteLine("Successfully executed iterations: " + handle.Iterations);
                starter.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private static void work()
        {
            double d = 1.0;
            for (int i = 0; i < 1000000; i++)
            {
                d *= 3945762736.28335867983454;
                d = Math.Tan(d * 23) + Math.Sqrt(d);
                d /= 476.34579;
            }
        }
    }
}