using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Azurenaut.Orchestration.PizzaMaking;

namespace Azurenaut;

public static class PizzaMakingHttpTrigger
{
    [Function("PizzaMaking_HttpStart")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("PizzaMaking_HttpStart");

        // Get pizza type from query string or default to Margherita
        string pizzaType = req.Query["type"] ?? "Margherita";

        // Start the pizza making orchestration
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(PizzaMakingOrchestrator.MakePizza), pizzaType);

        logger.LogInformation("Started pizza making orchestration for {pizzaType} with ID = '{instanceId}'.", 
            pizzaType, instanceId);

        // Returns an HTTP 202 response with an instance management payload.
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}
