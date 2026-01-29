using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;
using Azurenaut.Services.Foundry;
using System.Text.Json;
using System.IO;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.VisualBasic;

namespace Azurenaut;

public class StartAgent
{
    private readonly ILogger<StartAgent> _logger;
    private readonly IAgentService _agentService;

    public StartAgent(ILogger<StartAgent> logger, IAgentService agentService)
    {
        _logger = logger;
        _agentService = agentService;
    }

    /// <summary>
    /// Configures and initializes an AI assistant agent environment by creating or retrieving an existing agent and thread.
    /// This Azure Function processes HTTP requests containing agent configuration details, establishes the assistant environment,
    /// and returns the agent's response along with the agent thread information.
    /// </summary>
    /// <param name="req">
    /// HTTP request containing a JSON payload with an AgentThread object. The request body should include:
    /// - AgentId: Optional identifier for an existing agent (if null or invalid, a new agent is created)
    /// - ThreadId: Optional identifier for an existing conversation thread (if null, a new thread is created)
    /// </param>
    /// <returns>
    /// Returns an OkObjectResult with a ClientResponse object containing:
    /// - Response: The assistant's response message
    /// - AgentThread: Object with AgentId and ThreadId for maintaining conversation state
    /// 
    /// Sample return:
    /// {
    ///   "Response": "Hello! I'm your AI assistant. How can I help you today?",
    ///   "AgentThread": {
    ///     "AgentId": "asst_abc123xyz",
    ///     "ThreadId": "thread_def456uvw"
    ///   }
    /// }
    /// </returns>
    [Function("StartAgent")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        // _logger.LogInformation("C# HTTP trigger function processed a request.");
        // //string msg = _agentService.Echo("Hello, world!");

        // parse http request with content type application/json with T<AgentThread>
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        AgentThread requestAgentThread = JsonSerializer.Deserialize<AgentThread>(requestBody);

        // Get or create an Agent if the AgentId is valid, else a new agent is created and AngentId and ThreadId are returned
        var configureAssistant = await _agentService.ConfigureAssistantEnvironment(requestAgentThread.AgentId, requestAgentThread.ThreadId);

        return new OkObjectResult
        (
            new ClientResponse
            {
                Response = configureAssistant.Response,
                AgentThread = configureAssistant.AgentThread
            }
        );

    }

}



