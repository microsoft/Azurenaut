using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Foundry;


namespace SampleAgent;

public class AssistantConversation
{
    private readonly ILogger<AssistantConversation> _logger;
    private readonly IAgentService _agentService;

    public AssistantConversation(ILogger<AssistantConversation> logger, IAgentService agentService)
    {
        _logger = logger;
        _agentService = agentService;
    }

    [Function("AssistantConversation")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
    {
        // parse http request with content type application/json with T<AgentThread>
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        AgentThread requestAgentThread = JsonSerializer.Deserialize<AgentThread>(requestBody);

        return new OkObjectResult("Welcome to Azure Functions!");
    }

    private async Task<ClientResponse> SendThreadMessageAsync(string threadId, string messageContent)
    {
        _logger.LogInformation("Sending message to thread {ThreadId}", threadId);

        var response = await _agentService.CreateThreadMessage(threadId, messageContent);
        if (response == null || string.IsNullOrEmpty(response.Response))
        {
            _logger.LogError("Failed to send message to thread {ThreadId}", threadId);
            return new ClientResponse
            {
                Response = "Failed to send message.",
                AgentThread = null
            };
        }

        return response;
    }
}
