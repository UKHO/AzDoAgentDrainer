using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzDoAgentDrainer
{
    public class Context : IDrainerContext
    {
        private readonly ILogger logger;
        public List<ServerContext> Servers { get; } = new List<ServerContext>();

        public Context(List<ServerContext> Servers, ILogger logger)
        {
            this.Servers = Servers;
            this.logger = logger;
        }

        public async Task DrainAsync()
        {
            logger.LogInformation("Draining agents");
            // Iterate over each server in parallel
            await Task.WhenAll(Servers.Select(sc => DrainByServer(sc)));
            logger.LogDebug("Agents drained");
        }

        private async Task DrainByServer(ServerContext sc)
        {
            var agentsByPool = sc.Agents.GroupBy(p => p.PoolID);

            // Iterate over each pool and disable agents in parallel
            await Task.WhenAll(agentsByPool.Select(async abp =>
            {
                foreach (var agent in abp)
                {
                    // Some agents are disabled. We need to keep track of the ones to be re-enabled. 
                    if (agent.Agent.Enabled ?? false)
                    {
                        agent.Reenable = true;
                        logger.LogInformation("Disabling {AgentName} {AgentPoolId} {AgentServer}", agent.AgentName, abp.Key, sc.Client.BaseAddress);                        
                    }
                    else
                    {
                        logger.LogInformation("{AgentName} already disabled {AgentPoolId} {AgentServer}", agent.AgentName, abp.Key, sc.Client.BaseAddress);
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
                                         .ForEach(x => logger.LogInformation("Waiting for {job} to finish", x.AssignedRequest.JobId)); ;
                        })
                    .ExecuteAsync(async () => await sc.Client.GetAgentsAsync(poolId: abp.Key, includeAssignedRequest: true));

            }));
        }

        public async Task EnableAsync()
        {
            logger.LogInformation("Renabling Agents");
            // Iterate over each server in parallel
            await Task.WhenAll(Servers.Select(sc => EnableByServer(sc)));
            logger.LogDebug("Agents reenabled");
        }

        private async Task EnableByServer(ServerContext sc)
        {
            var agentsByPool = sc.Agents.Where(x => x.Reenable)
                                        .GroupBy(x => x.PoolID);

            // Iterate over each pool and enable agents in parallel
            await Task.WhenAll(agentsByPool.Select(async abp => {
                foreach (var a in abp)
                {
                    logger.LogInformation("Enabling agent {AgentName} {AgentPoolId} {AgentServer}", a.AgentName, abp.Key, sc.Client.BaseAddress);
                    var agent = await sc.Client.GetAgentAsync(abp.Key, a.AgentId);
                    agent.Enabled = true;

                    await sc.Client.UpdateAgentAsync(abp.Key, a.AgentId, agent);
                }
            }));
        }
    }
}
