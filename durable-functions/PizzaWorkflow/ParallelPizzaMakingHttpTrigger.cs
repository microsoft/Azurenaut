using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Azurenaut.Orchestration.PizzaMaking;
using System.Net;
using System.Text.Json;

namespace Azurenaut;

public static class ParallelPizzaMakingHttpTrigger
{
    [Function("PizzaMakingParallel_HttpStart")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("PizzaMakingParallel_HttpStart");

        // Parse the batch request from request body
        PizzaBatchRequest? batchRequest;
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            batchRequest = JsonSerializer.Deserialize<PizzaBatchRequest>(requestBody ?? "{}");
            
            if (batchRequest == null || batchRequest.PizzaTypes.Count == 0)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid batch request. Please provide pizza types.");
                return errorResponse;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse batch request");
            var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await errorResponse.WriteStringAsync($"Failed to parse request: {ex.Message}");
            return errorResponse;
        }

        // Build a deterministic instance ID using the order number
        var orderNumber = batchRequest.OrderNumber ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var instanceId = $"MakePizzasInParallel-{orderNumber}";

        // Start the parallel pizza making orchestration
        await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(PizzaMakingOrchestrator.MakePizzasInParallel),
            batchRequest,
            new StartOrchestrationOptions { InstanceId = instanceId });

        logger.LogInformation("Started parallel pizza making orchestration with ID = '{instanceId}' for {count} pizzas.", 
            instanceId, batchRequest.PizzaTypes.Count);

        // Returns an HTTP 202 response with an instance management payload
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}
