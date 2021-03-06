﻿using Microsoft.TeamFoundation.DistributedTask.WebApi;
using System;
using System.Collections.Generic;

namespace AzureDevopsAgentOperator
{
    public class Agent
    {
        public string ComputerName { get; }
        public DateTime CreatedOn { get; }
        public int Id { get; }
        public string Name { get; }
        public int PoolID { get; }
        public string ProvisioningState { get; }
        public bool AllowOperations { get; private set; } = false;
        public int Status { get; }
        public string OSDescription { get; }
        public IDictionary<string, string> Capabilities { get; }

        public Agent(TaskAgent taskAgent, int PoolId)
        {
            this.ComputerName = taskAgent.SystemCapabilities["Agent.ComputerName"];
            this.CreatedOn = taskAgent.CreatedOn;
            this.Id = taskAgent.Id;
            this.Name = taskAgent.Name;
            this.AllowOperations = taskAgent.Enabled ?? false;
            this.PoolID = PoolId;
            this.ProvisioningState = taskAgent.ProvisioningState;
            this.Status = (int)taskAgent.Status;
            this.OSDescription = taskAgent.OSDescription;
            this.Capabilities = taskAgent.SystemCapabilities;
        }

        public void EnableAllowOperations()
        {
            this.AllowOperations = true;
        }
    }    
}

