using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AzureDevopsAgentOperator;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AzureVmAgentsService.Models;

namespace AzureVmAgentsService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IInstanceMetadataServiceAPI _instanceMetadataServiceAPI;
        private IAgentOperator _agentsContext;

        public Worker(ILogger<Worker> logger, IInstanceMetadataServiceAPI scheduldedEventsAPI, IAgentOperator agentsContext)
        {
            _logger = logger;
            _instanceMetadataServiceAPI = scheduldedEventsAPI;
            _agentsContext = agentsContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Query the Instance Metadata Service for the VM name. This may be different to the computer name. The VMName is used in the schdeduled events to specify the machines that may be affcted.
            var _computerName = await _instanceMetadataServiceAPI.GetVMName();
            _logger.LogInformation("VMName ${wmvname}", _computerName);

            // Discover the initial documentIncarnation number for comparsion later on to see if it has changed
            var events = await _instanceMetadataServiceAPI.GetScheduldedEvents();
            var _documentIncarnation = events.DocumentIncarnation;

            // Ensure all agents are enabled on this server to begin with
            await _agentsContext.EnableAllAsync();            

            while (!stoppingToken.IsCancellationRequested)
            {
                var scheduldedEventsReponse = await _instanceMetadataServiceAPI.GetScheduldedEvents();

                if (_documentIncarnation != scheduldedEventsReponse.DocumentIncarnation) // Then a new event has occured and we need to check if something about to happen to this VM.
                {
                    _logger.LogInformation("DocumentIncarnation changed from {previous} to {current}", _documentIncarnation, scheduldedEventsReponse.DocumentIncarnation);
                    _documentIncarnation = scheduldedEventsReponse.DocumentIncarnation; // Update the DocumentIncarnation so we don't try to handle the SchdeduldedEvent again.

                    var relevantEvents = scheduldedEventsReponse.Events.Where(x => x.Resources.Exists(x => x.ToUpper() == _computerName.ToUpper()));
                    _logger.LogInformation("Found {eventcount}", relevantEvents.Count());
                    relevantEvents.ToList().ForEach(x => _logger.LogInformation($"EventId: {x.EventId}, EventType: {x.EventType}, NotBefore: {x.NotBefore}"));

                    if (relevantEvents.Any(x => string.Equals(x.EventType, "Reboot", System.StringComparison.OrdinalIgnoreCase) || string.Equals(x.EventType, "Redeploy", System.StringComparison.OrdinalIgnoreCase)))
                    {
                        await _agentsContext.DrainAsync();
                    }
                    else if (relevantEvents.Any(x => string.Equals(x.EventType, "Terminate", System.StringComparison.OrdinalIgnoreCase)))
                    {
                        // Remove the agent
                    }

                    _logger.LogInformation("Acknowlding {events}", relevantEvents);
                    relevantEvents.ToList().ForEach(x => _logger.LogInformation($"Acknowlding {x.EventId}"));
                    _ = await _instanceMetadataServiceAPI.AcknowledgeScheduldedEvent(new { StartRequests = relevantEvents });
                }
            }
            await Task.Delay(10000, stoppingToken);
        }
    }
}
