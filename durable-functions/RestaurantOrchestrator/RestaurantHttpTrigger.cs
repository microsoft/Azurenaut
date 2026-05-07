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

        // Parse the order from request body
        PizzaOrder? order;
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            order = JsonSerializer.Deserialize<PizzaOrder>(requestBody ?? "{}");

            if (order == null || string.IsNullOrWhiteSpace(order.OrderId) || order.PizzaTypes.Count == 0)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid order. Please provide an OrderId and at least one PizzaType.");
                return errorResponse;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse order");
            var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await errorResponse.WriteStringAsync($"Failed to parse order: {ex.Message}");
            return errorResponse;
        }

        // Start the restaurant orchestration
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            "ProcessRestaurantOrders",
            order);

        logger.LogInformation("Started restaurant orchestration with ID = '{instanceId}' for order {orderId} with {count} pizza(s).",
            instanceId, order.OrderId, order.PizzaTypes.Count);

        // Returns an HTTP 202 response with an instance management payload
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}
