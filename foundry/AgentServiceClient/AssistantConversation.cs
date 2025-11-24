using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Azurenaut.Services.Foundry;


namespace Azurenaut;

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

        _logger.LogInformation("Received request for thread {ThreadId} with AgentId {Message}", requestAgentThread.ThreadId, requestAgentThread.Message);
        var assistantConversationResponse = await SendThreadMessageAsync(requestAgentThread, requestAgentThread.Message);
        if (assistantConversationResponse.AgentThread == null)
        {
            _logger.LogError("Failed to send message or start run for thread {ThreadId}", requestAgentThread.ThreadId);
            return new BadRequestObjectResult("Failed to send message or start run.");
        }

        _logger.LogInformation("Message sent successfully to thread {ThreadId}", requestAgentThread.ThreadId);

        var conversationMessages = await _agentService.GetThreadMessagesAsync(requestAgentThread.ThreadId);
        if (conversationMessages == null)
        {
            _logger.LogWarning("No messages found in thread {ThreadId}", requestAgentThread.ThreadId);
            return new NotFoundObjectResult("No messages found in the thread.");
        }

        return new OkObjectResult
        (
            new ClientResponse
                {
                    Response = conversationMessages.Response,
                    AgentThread = new AgentThread
                    {
                        ThreadId = conversationMessages.AgentThread.ThreadId,
                        AgentId = conversationMessages.AgentThread.AgentId,
                        RunId = conversationMessages.AgentThread.RunId,
                        Messages = conversationMessages.AgentThread.Messages
                    }
                }
        );
    }

    private async Task<ClientResponse> SendThreadMessageAsync(AgentThread agentThread, string messageContent)
    {
        _logger.LogInformation("Sending message to thread {ThreadId}", agentThread.Message);

        var response = await _agentService.CreateThreadMessage(agentThread.ThreadId, agentThread.Message);
        if (response == null || string.IsNullOrEmpty(response.Response))
        {
            _logger.LogError("Failed to send message to thread {ThreadId}", agentThread.ThreadId);
            return new ClientResponse
            {
                Response = "Failed to send message.",
                AgentThread = null
            };
        }

        var run = await _agentService.StartRunAsync(agentThread.ThreadId, agentThread.AgentId);
        if (run == null || string.IsNullOrEmpty(run.Response))
        {
            _logger.LogError("Failed to start run for thread {ThreadId}", agentThread.ThreadId);
            return new ClientResponse
            {
                Response = "Failed to start run.",
                AgentThread = null
            };
        }

        return new ClientResponse
        {
            Response = run.Response,
            AgentThread = new AgentThread
            {
                ThreadId = run.AgentThread.ThreadId,
                AgentId = run.AgentThread.AgentId,
                RunId = run.AgentThread.RunId
            }
        };
    }
}
