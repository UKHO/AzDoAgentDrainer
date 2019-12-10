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
        private Func<TaskAgentHttpClient, Task<List<WrappedAgent>>> discoverAgents;

        public AgentContextBuilder AddServer(Uri AzDoUri, string pat)
        {
            vssConnections.Add(new VssConnection(AzDoUri, new VssBasicCredential(string.Empty, pat)));
            
            return this;
        }

        public AgentContextBuilder WherePoolId(int PoolID)
        {
            if(discoverAgents != null)
            {
                throw new Exception("A `Where` clause has already been defined.");
            }

            // Cheap naive guard
            if (vssConnections.Count > 1)
            {
                throw new Exception("WherePoolId cannot be used where more than one server has been added");
            }

            discoverAgents = async (client) => await Approaches.GetAgentsByPoolID(PoolID, client);

            return this;
        }
     
        public AgentContextBuilder WhereComputerName(string ComputerName)
        {
            if (discoverAgents != null)
            {
                throw new Exception("A `Where` clause has already been defined.");
            }
           
            discoverAgents = async (client) => await Approaches.GetAgentsByComputerName(ComputerName, client);
            
            return this;
        }

        public async Task<Context> Build()
        {
            var serverContext = new List<ServerContext>();

            foreach (var connection in vssConnections)
            {
                var client = connection.GetClient<TaskAgentHttpClient>();
                var agents = await discoverAgents(client);

                if(agents.Any())
                {
                    serverContext.Add(new ServerContext() { Client = client, Agents = agents });
                }                
            }

            // Check we have agents
            if (!serverContext.Any( x=> x.Agents.Any()))
            {
                Console.WriteLine("No matching agents found in any server");
                throw new Exception("No matching agents found in any server");
            };

            return new Context(serverContext);
        }
    }
}
