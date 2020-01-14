using System.Threading.Tasks;

namespace AzDoAgentDrainer
{
    public interface IDrainerContext{        
        Task DrainAsync();
        Task EnableAsync();
    }
}
