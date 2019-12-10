using Microsoft.TeamFoundation.DistributedTask.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzDoAgentDrainer
{
    public static class Approaches
    {
        public static async Task<List<WrappedAgent>> GetAgentsByPoolID(int poolID, TaskAgentHttpClient client)
        {
            var agents = await client.GetAgentsAsync(poolId: poolID);

            Console.WriteLine($"Agents found for PoolID {poolID}");
            agents.ForEach(a => Console.WriteLine($"Agent Name: {a.Name} Enabled: {a.Enabled}"));

            return agents.Select(a => new WrappedAgent { PoolID = poolID, AgentId = a.Id, Agent = a, AgentName = a.Name }).ToList();
        }

        public static async Task<List<WrappedAgent>> GetAgentsByComputerName(string computerName, TaskAgentHttpClient client)
        {
            var matchingWrappedAgents = new List<WrappedAgent>();

            var pools = await client.GetAgentPoolsAsync();

            foreach (var p in pools)
            {
                var demandString = $"Agent.ComputerName -equals {computerName}";
                var parsed = Demand.TryParse(demandString, out Demand d);
                if (!parsed)
                {
                    Console.WriteLine($"Failed to parse demand string '{demandString}'");
                    throw new Exception($"Failed to parse demand string '{demandString}'");
                }
                var demands = new List<Demand>() { d };

                // Get all agents which match the demands for this pool
                var agentsMatchingDemands = await client.GetAgentsAsync(p.Id, demands: demands);
                agentsMatchingDemands.ForEach(a => Console.WriteLine($"Agent Name: {a.Name} Enabled: {a.Enabled} Pool: {p.Id}"));

                // Turn this into a list of wrappedAgents
                var wrappedAgents = agentsMatchingDemands.Select(x => new WrappedAgent { PoolID = p.Id, AgentId = x.Id, Agent = x, AgentName = x.Name }).ToList();

                // Add the list to the list of all.
                matchingWrappedAgents = matchingWrappedAgents.Concat(wrappedAgents).ToList();
            }

            return matchingWrappedAgents;
        }
    }
}
