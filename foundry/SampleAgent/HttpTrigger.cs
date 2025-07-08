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

}
