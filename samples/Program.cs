using System;
using System.Threading.Tasks;

namespace AzDoAgentDrainer.CLI
{
    class Program
    {        
        static async Task Main(string[] args)
        {   
            var tfs = new Uri(args[0]);
            var tfsPat = args[1];

            var azdo = new Uri(args[2]);
            var azdoPat = args[3];

            var computerName = args[4];


            var agentsContext = await new AgentContextBuilder()
                                    .AddServer(tfs, tfsPat)
                                    .AddServer(azdo, azdoPat)
                                    .WhereComputerName(computerName)
                                    .Build();

            Console.WriteLine("Agent Context Built");


            await agentsContext.Drain();
            Console.WriteLine("All Jobs Finished again");
            await agentsContext.Enable();
        }
    }
}
