using System.Threading.Tasks;
using Refit;

namespace AzureVmAgentsService.Models
{
    public interface IInstanceMetadataServiceAPI
    {
        [Headers("Metadata: True")]
        [Get("/metadata/instance/compute/name?api-version=2019-03-11&format=text")]
        Task<string> GetVMName();

        [Headers("Metadata: True")]
        [Get("/metadata/scheduledevents?api-version=2019-01-01")]
        Task<ScheduldedEventsResponse> GetScheduldedEvents();

        [Headers("Metadata: True")]
        [Post("/metadata/scheduledevents?api-version=2019-01-01")]
        Task<string> AcknowledgeScheduldedEvent([Body]object s);
    }


}
