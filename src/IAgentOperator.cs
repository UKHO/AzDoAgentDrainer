using System.Threading.Tasks;

namespace AzDoAgentDrainer
{
    public interface IAgentOperator{        
        Task DrainAsync();
        Task EnableAsync();
        Task EnableAllAsync();
    }
}
