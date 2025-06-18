using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Foundry;
using System.Text.Json;
using System.IO;

namespace SampleAgent
{
    public class HttpTrigger
    {
        private readonly ILogger<HttpTrigger> _logger;
        private readonly IAgentService _agentService;

        public HttpTrigger(ILogger<HttpTrigger> logger, IAgentService agentService)
        {
            _logger = logger;
            _agentService = agentService;
        }

        [Function("HttpTrigger")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            //string msg = _agentService.Echo("Hello, world!");

            // parse http request with content type application/json with T<AgentThread>
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            AgentThread requestAgentThread = JsonSerializer.Deserialize<AgentThread>(requestBody);

            // Get or create an Agent if the AgentId is valid, else a new agent is created and AngentId and ThreadId are returned
            var configureAssistant = await ConfigureAssistantEnvironment(requestAgentThread.AgentId, requestAgentThread.ThreadId);

            return new OkObjectResult
            (
                new ClientResponse
                {
                    Response = configureAssistant.Response,
                    AgentThread = configureAssistant.AgentThread
                }
            );
        }

        private async Task<ClientResponse> ConfigureAssistantEnvironment(string agentId, string threadId)
        {
            _logger.LogInformation("Starting Agent configuration.");

            var agentFoundryResponse = await _agentService.GetOrCreateAgentAsync(agentId);
            if (string.IsNullOrEmpty(agentFoundryResponse.AgentThread.AgentId))
            {
                _logger.LogError("Failed to create or retrieve agent.");
                return new ClientResponse
                {
                    Response = "Failed to create or retrieve agent.",
                    AgentThread = null
                };
                
            }

            var threadFoundryResponse = await _agentService.GetOrCreateThreadAsync(threadId);
            if (string.IsNullOrEmpty(threadFoundryResponse.AgentThread.ThreadId))
            {
                _logger.LogError("Failed to create or retrieve thread.");
                return new ClientResponse
                {
                    Response = "Failed to create or retrieve thread.",
                    AgentThread = null
                };
            }

            _logger.LogInformation("Agent and Thread retrieved or created.");
            // You can implement logic for GET requests here if needed
            return new ClientResponse
            {
                Response = "Agent and thread successfully configured.",
                AgentThread = new AgentThread
                {
                    AgentId = agentFoundryResponse.AgentThread.AgentId,
                    ThreadId = threadFoundryResponse.AgentThread.ThreadId
                }
            };
        }
    }

}
