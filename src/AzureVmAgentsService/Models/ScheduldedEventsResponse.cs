using System.Collections.Generic;

namespace AzureVmAgentsService.Models
{
    public class ScheduldedEventsResponse
    {
        public int DocumentIncarnation { get; set; }
        public List<ScheduldedEvents> Events { get; set; }
    }


}
