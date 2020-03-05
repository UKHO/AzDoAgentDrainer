using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AzureDevopsAgentOperator;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AzureVmAgentsService.Models;
using Refit;
using System;

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
            _logger.LogInformation("Starting drainer");

            // Query the Instance Metadata Service for the VM name. This may be different to the computer name. The VMName is used in the schdeduled events to specify the machines that may be affcted.
            var _computerName = await _instanceMetadataServiceAPI.GetVMName();
            _logger.LogInformation("Azure VMName {wmvname}", _computerName);

            // Discover the initial documentIncarnation number for comparsion later on to see if it has changed
            var events = await _instanceMetadataServiceAPI.GetScheduldedEvents();
            var _documentIncarnation = events.DocumentIncarnation;

            // Ensure all agents are enabled on this server
            await _agentsContext.EnableAllAsync();

            // Provide a way to manually stop the drainer once the agents have been disabled and event responded to
            // Else tries to acknowledge the event multiple times
            var drainerShouldStop = false;

            while (!stoppingToken.IsCancellationRequested && !drainerShouldStop)
            {
                var scheduldedEventsResponse = new ScheduldedEventsResponse();
                try
                {
                    _logger.LogInformation("Checking for ScheduldedEvents");
                    scheduldedEventsResponse = await _instanceMetadataServiceAPI.GetScheduldedEvents();
                    _logger.LogInformation("Checked for ScheduldedEvents");
                }
                catch(ApiException apiEx)
                {
                    // Eat the exception and keep on going 
                    _logger.LogError("Error checking for ScheduldedEvents. Will continue to run. {errorStatus} {errorContent} ", apiEx.StatusCode, apiEx.Content);                    
                    // Spoof that the documentionIncartion is the same to avoid falling triggering the drainer below.
                    scheduldedEventsResponse.DocumentIncarnation = _documentIncarnation;
                }               

                if (_documentIncarnation != scheduldedEventsResponse.DocumentIncarnation) // Then a new event has occured and we need to check if something about to happen to this VM.
                {
                    _logger.LogInformation("DocumentIncarnation changed from {previous} to {current}", _documentIncarnation, scheduldedEventsResponse.DocumentIncarnation);
                    _documentIncarnation = scheduldedEventsResponse.DocumentIncarnation; // Update the DocumentIncarnation so we don't try to handle the SchdeduldedEvent again.

                    var relevantEvents = scheduldedEventsResponse.Events.Where(x => x.Resources.Exists(affectedServer => affectedServer.ToUpper() == _computerName.ToUpper()));
                    if (relevantEvents.Any())
                    {
                        _logger.LogInformation("Schedulded events for this server {eventcount}", relevantEvents.Count());
                        relevantEvents.ToList().ForEach(x => _logger.LogInformation("{eventId}, {eventType}, {notBefore}", x.EventId, x.EventType, x.NotBefore));                        

                        if (relevantEvents.Any(x => string.Equals(x.EventType, "Reboot", StringComparison.OrdinalIgnoreCase) || string.Equals(x.EventType, "Redeploy", StringComparison.OrdinalIgnoreCase)))
                        {
                            try
                            {
                                await _agentsContext.DrainAsync();
                                drainerShouldStop = true;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError("Error draining agent for reboot or redeploy", ex);
                                throw;
                            }                            
                        }
                        else if (relevantEvents.Any(x => string.Equals(x.EventType, "Terminate", StringComparison.OrdinalIgnoreCase)))
                        {
                            try
                            {
                                // Ensure all the agents are disabled before deleting them
                                await _agentsContext.DrainAsync();
                                await _agentsContext.DeleteAllAsync();
                                drainerShouldStop = true;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError("Error draining or deleting agent for a terminate", ex);
                                throw;
                            }                            
                        }

                        _logger.LogInformation("Acknowledging {events}", relevantEvents);
                        relevantEvents.ToList().ForEach(x => _logger.LogInformation($"Acknowledging {x.EventId}"));
                        try
                        {
                            _ = await _instanceMetadataServiceAPI.AcknowledgeScheduldedEvent(new { StartRequests = relevantEvents });
                        }
                        catch (ApiException apiEx)
                        {
                            _logger.LogError("Error acknowledging events {errorStatus} {errorContent}", apiEx.StatusCode, apiEx.Content);
                            throw;
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No relevant schedulded events for this server {computerName}", _computerName);
                        scheduldedEventsResponse.Events.ForEach(x => _logger.LogInformation("Event contained {eventType} {eventStatus} {resources}", x.EventType, x.EventStatus, x.Resources));
                    }
                }
                else{
                    _logger.LogInformation("DocumentIncarnation has not changed");
                }

                await Task.Delay(10000, stoppingToken);

                if (drainerShouldStop)
                {
                    _logger.LogInformation("drainerShouldStop has been set to true. Stopping drainer");
                }
            }
            _logger.LogWarning("Exited checking for ScheduldedEvents");
        }
    }
}
