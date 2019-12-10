using Microsoft.TeamFoundation.DistributedTask.WebApi;

namespace AzDoAgentDrainer
{
    public class WrappedAgent
    {
        public int PoolID { get; set; }
        public int AgentId { get; set; }
        public string AgentName { get; set; }
        public TaskAgent Agent { get; set; }
        public bool Reenable { get; set; } = false;
    }
}
