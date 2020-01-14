using Microsoft.TeamFoundation.DistributedTask.WebApi;
using System.Collections.Generic;

namespace AzDoAgentDrainer
{
    public class ServerContext
    {
        public TaskAgentHttpClient Client { get; set; }
        public IEnumerable<Agent> Agents { get; set; }
    }
}
