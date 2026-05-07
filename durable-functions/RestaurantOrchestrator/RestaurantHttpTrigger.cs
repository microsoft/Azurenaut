using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Azurenaut.Orchestration.Restaurant;
using System.Net;
using System.Text.Json;

namespace Azurenaut;

public static class RestaurantHttpTrigger
{
    [Function("Restaurant_HttpStart")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("Restaurant_HttpStart");

        // Parse the order batch from request body
        RestaurantOrderBatch? orderBatch;
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            orderBatch = JsonSerializer.Deserialize<RestaurantOrderBatch>(requestBody ?? "{}");
            
            if (orderBatch == null || orderBatch.Orders.Count == 0)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid order batch. Please provide orders.");
                return errorResponse;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse order batch");
            var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await errorResponse.WriteStringAsync($"Failed to parse order: {ex.Message}");
            return errorResponse;
        }

        // Start the restaurant orchestration
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            "ProcessRestaurantOrders", 
            orderBatch);

        logger.LogInformation("Started restaurant orchestration with ID = '{instanceId}' for {count} orders.", 
            instanceId, orderBatch.Orders.Count);

        // Returns an HTTP 202 response with an instance management payload
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}
