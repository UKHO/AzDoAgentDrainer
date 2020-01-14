
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using System;
using System.Collections.Generic;

namespace AzDoAgentDrainer
{
    public class Agent
    {
        public string ComputerName { get; set; }
        public DateTime CreatedOn { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public int PoolID { get; set; }
        public string ProvisioningState { get; set; }
        public bool Reenable { get; } = false;
        public int Status { get; set; }
        public string OSDescription { get; set; }
        public IDictionary<string, string> Capabilities { get; set; }

        public Agent(TaskAgent taskAgent, int PoolId)
        {
            this.ComputerName = taskAgent.SystemCapabilities["Agent.ComputerName"];
            this.CreatedOn = taskAgent.CreatedOn;
            this.Id = taskAgent.Id;
            this.Name = taskAgent.Name;
            this.Reenable = taskAgent.Enabled ?? false;
            this.PoolID = PoolId;
            this.ProvisioningState = taskAgent.ProvisioningState;
            this.Status = (int)taskAgent.Status;
            this.OSDescription = taskAgent.OSDescription;
            this.Capabilities = taskAgent.SystemCapabilities;
        }
    }    
}

