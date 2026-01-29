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
    /// Starts or retrieves an AI agent based on the provided AgentId and ThreadId. 
    /// </summary>
    /// <param name="req"></param>
    /// <returns>ClientResponse containing the response message and AgentThread details.</returns>
    /// <remarks>
    /// Sample reponse:
    /// ClientResponse
    /// {
    ///   "Response": "Agent configured successfully.",
    ///   "AgentThread": {
    ///     "AgentId": "agent-123",
    ///     "ThreadId": "thread-456"
    ///   }
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



