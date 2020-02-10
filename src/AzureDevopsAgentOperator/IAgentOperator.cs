using System.Threading.Tasks;

namespace AzureDevopsAgentOperator
{
    public interface IAgentOperator{        
        Task DrainAsync();
        Task EnableAsync();
        Task EnableAllAsync();
        Task DeleteAllAsync();
    }
}
