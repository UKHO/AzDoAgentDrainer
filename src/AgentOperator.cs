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
    public class AgentOperator : IAgentOperator
    {
        private readonly ILogger logger;
        private List<AzureDevopsInstance> AzureDevopsInstances { get; } = new List<AzureDevopsInstance>();

        public AgentOperator(List<AzureDevopsInstance> instances, ILogger logger)
        {
            this.AzureDevopsInstances = instances;
            this.logger = logger;
        }

        public async Task DrainAsync()
        {
            logger.LogInformation("Draining agents");
            // Iterate over each server in parallel
            await Task.WhenAll(AzureDevopsInstances.Select(sc => DrainByInstance(sc)));
            logger.LogDebug("Agents drained");
        }

        private async Task DrainByInstance(AzureDevopsInstance azInstance)
        {
            var agentsByPool = azInstance.Agents.GroupBy(p => p.PoolID);

            // Iterate over each pool and disable agents in parallel
            await Task.WhenAll(agentsByPool.Select(async abp =>
            {
                await Task.WhenAll(abp.Select(async agent =>
                {
                    if (agent.Reenable) // If an agent can be renabled it must be enabled
                    {
                        logger.LogInformation("Disabling {AgentName} {AgentPoolId} {AgentServer}", agent.Name, abp.Key, azInstance.Client.BaseAddress);

                        var realAgent = await azInstance.Client.GetAgentAsync(agent.PoolID, agent.Id);
                        realAgent.Enabled = false;
                        await azInstance.Client.UpdateAgentAsync(agent.PoolID, agent.Id, realAgent); // Update the agent so that it disabled
                    }
                    else
                    {
                        logger.LogInformation("{AgentName} already disabled {AgentPoolId} {AgentServer}", agent.Name, abp.Key, azInstance.Client.BaseAddress);
                    }                    

                    // Wait 15 seconds incase something was assigned to this agent in the time it took for the network traffic to send from client to server
                    await Task.Delay(15000);

                    // Check if any jobs are running on the agent. If they, check every 30 seconds until the job ends
                    await Policy
                            .HandleResult<TaskAgent>(x => x.AssignedRequest != null)
                            .WaitAndRetryAsync(40, x => TimeSpan.FromSeconds(30),
                                (result, timespan, content) =>
                                {
                                    if (result.Result.AssignedRequest != null)
                                    {
                                        logger.LogInformation("{agentName} waiting for {job} to finish {poolId}", agent.Name, result.Result.AssignedRequest.JobId, abp.Key);
                                    }
                                })
                            .ExecuteAsync(async () => await azInstance.Client.GetAgentAsync(poolId: abp.Key, agentId: agent.Id, includeAssignedRequest: true));
                }));
            }));
        }

        public async Task EnableAsync()
        {
            logger.LogInformation("Renabling Agents");
            // Iterate over each server in parallel
            await Task.WhenAll(AzureDevopsInstances.Select(sc => EnableByInstance(sc)));
            logger.LogDebug("Agents reenabled");
        }

        private async Task EnableByInstance(AzureDevopsInstance azInstance)
        {
            var agentsByPool = azInstance.Agents.Where(x => x.Reenable)
                                        .GroupBy(x => x.PoolID);

            // Iterate over each pool and enable agents in parallel
            await Task.WhenAll(agentsByPool.Select(async abp => {
                foreach (var agent in abp)
                {
                    logger.LogInformation("Enabling agent {AgentName} {AgentPoolId} {AgentServer}", agent.Name, abp.Key, azInstance.Client.BaseAddress);

                    var realAgent = await azInstance.Client.GetAgentAsync(agent.PoolID, agent.Id);
                    realAgent.Enabled = true;

                    await azInstance.Client.UpdateAgentAsync(abp.Key, agent.Id, realAgent);
                }
            }));
        }
    }
}
