using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Foundry
{
    public interface IAgentService
    {
        //Task<ClientResponse> GetOrCreateAgentAsync([Optional] string agentId);
        //Task<ClientResponse> GetOrCreateThreadAsync([Optional] string threadId);
        Task<ClientResponse> ConfigureAssistantEnvironment(string agentId, string threadId);
        Task<ClientResponse> CreateThreadMessage(string threadId, string messageContent);
        Task<ClientResponse> StartRunAsync(string threadId, string agentId, [Optional] string additionalInstructions);
        Task<ClientResponse> GetThreadMessagesAsync(string threadId);
    }
}