using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzDoAgentDrainer
{
    public class Context
    {
        public List<ServerContext> Servers { get; } = new List<ServerContext>();

        public Context(List<ServerContext> Servers)
        {
            this.Servers = Servers;
        }

        public async Task Drain()
        {
            Console.WriteLine("Draining Agents");

            foreach (var sc in Servers)
            {
                await DrainByServer(sc);
            }

        }

        private async Task DrainByServer(ServerContext sc)
        {
            var agentsByPool = sc.Agents.GroupBy(p => p.PoolID);

            foreach (var pool in agentsByPool)
            {
                foreach (var agent in pool)
                {
                    // Some agents are disabled. We need to keep track of the ones to be re-enabled. 
                    if (agent.Agent.Enabled ?? false)
                    {
                        agent.Reenable = true;
                        Console.WriteLine($"Disabling {agent.Agent.Name}");
                    }

                    agent.Agent.Enabled = false;
                    await sc.Client.UpdateAgentAsync(agent.PoolID, agent.Agent.Id, agent.Agent);
                }

                // Wait 15 seconds
                await Task.Delay(15000);

                // Wait until no jobs are running.
                await Policy
                    .HandleResult<List<TaskAgent>>(x => x.Any(a => a.AssignedRequest != null))
                    .WaitAndRetryAsync(20, x => TimeSpan.FromSeconds(30),
                        (result, timespan, context) =>
                        {
                            result.Result.Where(a => a.AssignedRequest != null)
                                         .ForEach(x => Console.WriteLine(x.AssignedRequest.JobId));
                        })
                    .ExecuteAsync(async () => await sc.Client.GetAgentsAsync(poolId: pool.Key, includeAssignedRequest: true));
            }
        }

        public async Task Enable()
        {
            Console.WriteLine("Agents to be enabled:");
            
            foreach (var sc in Servers)
            {
                await EnableByServer(sc);
            };
        }

        public async Task EnableByServer(ServerContext sc)
        {
            var agentsByPool = sc.Agents.Where(x => x.Reenable)
                                        .GroupBy(x => x.PoolID);

            foreach (var pool in agentsByPool)
            {
                foreach (var a in pool)
                {
                    Console.WriteLine($"Agent Name: {a.AgentName}");

                    var agent = await sc.Client.GetAgentAsync(a.PoolID, a.AgentId);
                    agent.Enabled = true;

                    await sc.Client.UpdateAgentAsync(pool.Key, a.AgentId, agent);
                }
            }
        }
    }
}
