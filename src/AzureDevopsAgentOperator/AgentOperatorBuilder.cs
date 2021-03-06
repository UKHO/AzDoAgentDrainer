﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzureDevopsAgentOperator
{
    public class AgentOperatorBuilder
    {
        private List<VssConnection> vssConnections = new List<VssConnection>();                
        private ILogger logger = NullLogger.Instance;
        private Func<IEnumerable<Agent>, IEnumerable<Agent>> filter;     

        public AgentOperatorBuilder AddInstance(Uri AzDoUri, string pat)
        {
            vssConnections.Add(new VssConnection(AzDoUri, new VssBasicCredential(string.Empty, pat)));            
            return this;
        }

        public AgentOperatorBuilder AddLogger(ILogger logger)
        {
            this.logger = logger;
            return this;
        }

        public AgentOperatorBuilder SelectAgents(Func<IEnumerable<Agent>, IEnumerable<Agent>> filter)
        {
            this.filter = filter;
            return this;
        }

        public async Task<AgentOperator> Build()
        {
            var azureDevopsInstances = new List<AzureDevopsInstance>();

            if(filter == null) {
                throw new Exception("SelectAgents must be configured");
            }

            foreach (var connection in vssConnections)
            {
                var client = connection.GetClient<TaskAgentHttpClient>();
                var agents = await GetAllAgentsForInstance(client, logger);

                agents = filter(agents).ToList();

                if(agents.Any())
                {
                    logger.LogDebug("{server} and associated agents added to server context", connection.Uri);
                    azureDevopsInstances.Add(new AzureDevopsInstance() { Client = client, Agents = agents});
                }
                else
                {
                    logger.LogInformation("{server} removed as no agents matched", connection.Uri);
                }
            }

            // Check we have agents at all. There should be at least one.
            if (!azureDevopsInstances.Any())
            {
                logger.LogError("No matching agents found in any server");
                throw new Exception("No matching agents found in any server");
            }
            else
            {
                logger.LogInformation("Context built {ServerCount} {AgentCount}", azureDevopsInstances.Count, azureDevopsInstances.Aggregate(0, (acc, x) => acc + x.Agents.Count()));;
            };            

            return new AgentOperator(azureDevopsInstances, logger);
        }


        private async Task<IEnumerable<Agent>> GetAllAgentsForInstance(TaskAgentHttpClient client, ILogger logger)
        {
            IEnumerable<Agent> allAgents = new List<Agent>();
            var pools = await client.GetAgentPoolsAsync();

            foreach (var p in pools.Where(x => x.IsHosted == false))
            {
                var agentsInPool = await client.GetAgentsAsync(p.Id, includeCapabilities: true);
                var convertedAgents = agentsInPool.Select(x => new Agent(x, p.Id));

                allAgents = allAgents.Concat(convertedAgents);
            }

            return allAgents;
        }
    }
}
