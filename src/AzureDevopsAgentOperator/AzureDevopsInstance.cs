using Microsoft.TeamFoundation.DistributedTask.WebApi;
using System.Collections.Generic;

namespace AzureDevopsAgentOperator
{
    public class AzureDevopsInstance
    {
        public TaskAgentHttpClient Client { get; set; }
        public IEnumerable<Agent> Agents { get; set; }
    }
}
