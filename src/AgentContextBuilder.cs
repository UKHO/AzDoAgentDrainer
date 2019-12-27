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
        private Func<TaskAgentHttpClient, ILogger, Task<List<WrappedAgent>>> discoverAgents;
        private ILogger logger = NullLogger.Instance;

        private string WhereErrorMessage = "AgentContextBuilder only allows one `Where` per pipeline";

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

        public AgentContextBuilder WherePoolId(int PoolID)
        {
            if(discoverAgents != null)
            {
                logger.LogError(WhereErrorMessage);
                throw new Exception(WhereErrorMessage);
            }

            // Cheap naive guard
            if (vssConnections.Count > 1)
            {
                logger.LogError("WherePoolId can only be used when only one server has been added");
                throw new Exception("WherePoolId can only be used when only one server has been added");
            }            
            
            discoverAgents = async (client, logger) => await Approaches.GetAgentsByPoolID(PoolID, logger, client);
            logger.LogDebug("{Approach} added as approach", "GetAgentsByPoolID");

            return this;
        }
     
        public AgentContextBuilder WhereComputerName(string ComputerName)
        {
            if (discoverAgents != null)
            {
                logger.LogError(WhereErrorMessage);
                throw new Exception(WhereErrorMessage);
            }
            
            discoverAgents = async (client, logger) => await Approaches.GetAgentsByComputerName(ComputerName, logger, client);
            logger.LogDebug("{Approach} added as approach", "GetAgentsByComputerName");

            return this;
        }

        public AgentContextBuilder WhereComputerNameContains(string searchstring)
        {
            if (discoverAgents != null)
            {
                logger.LogError(WhereErrorMessage);
                throw new Exception(WhereErrorMessage);
            }

            discoverAgents = async (client, logger) => await Approaches.GetAgentsBySearchString(searchstring, logger, client);
            logger.LogDebug("{Approach} added as approach", "GetAgentsBySearchString");

            return this;
        }

        public async Task<Context> Build()
        {
            var serverContext = new List<ServerContext>();

            foreach (var connection in vssConnections)
            {
                var client = connection.GetClient<TaskAgentHttpClient>();                
                var agents = await discoverAgents(client, logger);

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
    }
}
