using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzDoAgentDrainer
{
    public static class Approaches
    {
        public static async Task<List<WrappedAgent>> GetAgentsByPoolID(int poolID, ILogger logger, TaskAgentHttpClient client)
        {
            logger.LogInformation("Discovering agents by {Approach} {PoolID}", "GetAgentsByPoolID", poolID);
            var agents = await client.GetAgentsAsync(poolId: poolID);

            agents.ForEach(a => logger.LogInformation("Agent discovered: {AgentName} {AgentEnabled} {AgentPoolId} {AgentServer}", a.Name, a.Enabled, poolID, client.BaseAddress));

            return agents.Select(a => new WrappedAgent { PoolID = poolID, AgentId = a.Id, Agent = a, AgentName = a.Name }).ToList();
        }

        public static async Task<List<WrappedAgent>> GetAgentsByComputerName(string computerName, ILogger logger, TaskAgentHttpClient client)
        {
            logger.LogInformation("Discovering agents by {Approach} {ComputerName}", "GetAgentsByComputerName", computerName);
            var matchingWrappedAgents = new List<WrappedAgent>();
            var pools = await client.GetAgentPoolsAsync();

            foreach (var p in pools)
            {
                var demandString = $"Agent.ComputerName -equals {computerName}";
                var parsed = Demand.TryParse(demandString, out Demand d);
                if (!parsed)
                {
                    logger.LogError("Failed to parse demand string {demandString}", demandString);
                    throw new Exception($"Failed to parse demand string '{demandString}'");
                }
                var demands = new List<Demand>() { d };

                // Get all agents which match the demands for this pool
                var agentsMatchingDemands = await client.GetAgentsAsync(p.Id, demands: demands);
                agentsMatchingDemands.ForEach(a => logger.LogInformation("Agent discovered: {AgentName} {AgentEnabled} {AgentPoolId} {AgentServer}", a.Name, a.Enabled, p.Id, client.BaseAddress));

                // Turn this into a list of wrappedAgents
                var wrappedAgents = agentsMatchingDemands.Select(x => new WrappedAgent { PoolID = p.Id, AgentId = x.Id, Agent = x, AgentName = x.Name }).ToList();

                // Add the list to the list of all.
                matchingWrappedAgents = matchingWrappedAgents.Concat(wrappedAgents).ToList();
            }

            return matchingWrappedAgents;
        }
    }
}
