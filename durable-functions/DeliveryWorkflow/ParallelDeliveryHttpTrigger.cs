using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Azurenaut.Orchestration.Delivery;
using System.Net;
using System.Text.Json;

namespace Azurenaut;

public static class ParallelDeliveryHttpTrigger
{
    [Function("DeliveryParallel_HttpStart")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("DeliveryParallel_HttpStart");

        // Parse the batch request from request body
        DeliveryBatchRequest? batchRequest;
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            batchRequest = JsonSerializer.Deserialize<DeliveryBatchRequest>(requestBody ?? "{}");
            
            if (batchRequest == null || batchRequest.Deliveries.Count == 0)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid batch request. Please provide deliveries.");
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

        // Start the parallel delivery orchestration
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(DeliveryOrchestrator.DeliverPizzasInParallel), 
            batchRequest);

        logger.LogInformation("Started parallel delivery orchestration with ID = '{instanceId}' for {count} deliveries.", 
            instanceId, batchRequest.Deliveries.Count);

        // Returns an HTTP 202 response with an instance management payload
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}
