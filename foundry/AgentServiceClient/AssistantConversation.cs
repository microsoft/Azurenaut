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

    /// <summary>
    /// Processes conversational messages with an AI assistant agent by sending user messages to an existing thread,
    /// initiating a run, and retrieving the complete conversation history including the assistant's response.
    /// This Azure Function enables ongoing chat interactions with the assistant within an established thread context.
    /// </summary>
    /// <param name="req">
    /// HTTP request containing a JSON payload with an AgentThread object that includes:
    /// - ThreadId: Required identifier of the existing conversation thread
    /// - AgentId: Required identifier of the assistant agent
    /// - Message: Required text message from the user to send to the assistant
    /// </param>
    /// <returns>
    /// Returns an IActionResult based on the processing outcome:
    /// 
    /// Success (HTTP 200 OK): ClientResponse with the assistant's reply and conversation details
    /// {
    ///   "Response": "Based on the information you provided, I recommend...",
    ///   "AgentThread": {
    ///     "ThreadId": "thread_abc123xyz",
    ///     "AgentId": "asst_def456uvw",
    ///     "RunId": "run_ghi789rst",
    ///     "Messages": [
    ///       {
    ///         "Role": "user",
    ///         "Content": "What's the weather like?"
    ///       },
    ///       {
    ///         "Role": "assistant",
    ///         "Content": "Based on the information you provided, I recommend..."
    ///       }
    ///     ]
    ///   }
    /// }
    /// 
    /// Failure (HTTP 400 Bad Request): Error message if message sending or run execution fails
    /// Failure (HTTP 404 Not Found): Error message if no messages found in the thread
    /// </returns>
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

    /// <summary>
    /// Sends a user message to an existing assistant thread and initiates a run to generate the assistant's response.
    /// This private helper method handles the two-step process of adding a message to the thread and starting
    /// the assistant execution to process the message.
    /// </summary>
    /// <param name="agentThread">
    /// AgentThread object containing:
    /// - ThreadId: The identifier of the thread to send the message to
    /// - AgentId: The identifier of the assistant agent that will process the message
    /// - Message: The user's message content to be added to the thread
    /// </param>
    /// <param name="messageContent">
    /// The text content of the user's message to send (typically matches agentThread.Message)
    /// </param>
    /// <returns>
    /// Returns a ClientResponse object with the operation result:
    /// 
    /// Success:
    /// {
    ///   "Response": "Run started successfully",
    ///   "AgentThread": {
    ///     "ThreadId": "thread_abc123xyz",
    ///     "AgentId": "asst_def456uvw",
    ///     "RunId": "run_ghi789rst"
    ///   }
    /// }
    /// 
    /// Failure (message send failed):
    /// {
    ///   "Response": "Failed to send message.",
    ///   "AgentThread": null
    /// }
    /// 
    /// Failure (run start failed):
    /// {
    ///   "Response": "Failed to start run.",
    ///   "AgentThread": null
    /// }
    /// </returns>
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
