using System;
using System.Collections.Generic;

namespace AzureVmAgentsService.Models
{
    public class ScheduldedEvents
    {
        public string EventId { get; set; }
        public string EventStatus { get; set; }
        public string EventType { get; set; }
        public string ResourceType { get; set; }
        public List<string> Resources { get; set; }       
        public string NotBefore { get; set; }
    }
}