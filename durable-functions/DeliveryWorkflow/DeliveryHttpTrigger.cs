using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Azurenaut.Orchestration.Delivery;
using System.Net;
using System.Text.Json;

namespace Azurenaut;

public static class DeliveryHttpTrigger
{
    [Function("Delivery_HttpStart")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("Delivery_HttpStart");

        // Parse the delivery request from request body
        DeliveryRequest? deliveryRequest;
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            deliveryRequest = JsonSerializer.Deserialize<DeliveryRequest>(requestBody ?? "{}");
            
            if (deliveryRequest == null || string.IsNullOrEmpty(deliveryRequest.OrderId))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid delivery request. Please provide order details.");
                return errorResponse;
            }

            // Set random delivery duration if not provided (1-5 minutes)
            if (deliveryRequest.DeliveryDurationMinutes == 0)
            {
                var random = new Random();
                deliveryRequest.DeliveryDurationMinutes = random.Next(1, 6); // 1 to 5 minutes
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse delivery request");
            var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await errorResponse.WriteStringAsync($"Failed to parse delivery request: {ex.Message}");
            return errorResponse;
        }

        // Start the delivery orchestration
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(DeliveryOrchestrator.DeliverPizza), 
            deliveryRequest);

        logger.LogInformation("Started delivery orchestration with ID = '{instanceId}' for order {orderId} (ETA: {minutes} minutes).", 
            instanceId, deliveryRequest.OrderId, deliveryRequest.DeliveryDurationMinutes);

        // Returns an HTTP 202 response with an instance management payload
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}
