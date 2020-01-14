using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzDoAgentDrainer
{
    public class AgentContextBuilder
    {
        private List<VssConnection> vssConnections = new List<VssConnection>();                
        private ILogger logger = NullLogger.Instance;
        private Func<List<WrappedAgent>, IEnumerable<WrappedAgent>> filter;     

        public AgentContextBuilder AddServer(Uri AzDoUri, string pat)
        {
            vssConnections.Add(new VssConnection(AzDoUri, new VssBasicCredential(string.Empty, pat)));            
            return this;
        }

        public AgentContextBuilder AddLogger(ILogger logger)
        {
            this.logger = logger;
            return this;
        }

        public AgentContextBuilder SelectAgents(Func<List<WrappedAgent>, IEnumerable<WrappedAgent>> filter)
        {
            this.filter = filter;
            return this;
        }

        public async Task<Context> Build()
        {
            var serverContext = new List<ServerContext>();

            if(filter == null) {
                throw new Exception("SelectAgents must be configured");
            }

            foreach (var connection in vssConnections)
            {
                var client = connection.GetClient<TaskAgentHttpClient>();
                var agents = await GetAgents(client, logger);

                agents = filter(agents).ToList();

                if(agents.Any())
                {
                    logger.LogDebug("{server} and associated agents added to server context", connection.Uri);
                    serverContext.Add(new ServerContext() { Client = client, Agents = agents });
                }
                else
                {
                    logger.LogInformation("{server} removed as no agents matched", connection.Uri);
                }
            }

            // Check we have agents at all. There should be at least one.
            if (!serverContext.Any())
            {
                logger.LogError("No matching agents found in any server");
                throw new Exception("No matching agents found in any server");
            }
            else
            {
                logger.LogInformation("Context built {ServerCount} {AgentCount}", serverContext.Count, serverContext.Aggregate(0, (acc, x) => acc + x.Agents.Count));;
            };            

            return new Context(serverContext, logger);
        }


        private static async Task<List<WrappedAgent>> GetAgents(TaskAgentHttpClient client, ILogger logger)
        {
            var matchingWrappedAgents = new List<WrappedAgent>();
            var pools = await client.GetAgentPoolsAsync();

            foreach (var p in pools.Where(x => x.IsHosted == false))
            {
                var agents = await client.GetAgentsAsync(p.Id, includeCapabilities: true);
                var wrappedAgents = agents.Select(x => new WrappedAgent { PoolID = p.Id, AgentId = x.Id, Agent = x, AgentName = x.Name, ComputerName = x.SystemCapabilities["Agent.ComputerName"] });

                matchingWrappedAgents = matchingWrappedAgents.Concat(wrappedAgents).ToList();
            }

            return matchingWrappedAgents;
        }
    }
}
