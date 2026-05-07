using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Azurenaut.Orchestration.Delivery;

public class DeliveryRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string PizzaDescription { get; set; } = string.Empty;
    public int DeliveryDurationMinutes { get; set; } = 0;
}

public class DeliveryBatchRequest
{
    public List<DeliveryRequest> Deliveries { get; set; } = new();
}

public static class DeliveryOrchestrator
{
    [Function(nameof(DeliverPizza))]
    public static async Task<string> DeliverPizza(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(DeliverPizza));
        
        // Get the delivery request from input
        var deliveryRequest = context.GetInput<DeliveryRequest>()!;
        
        logger.LogInformation("Starting delivery for order {orderId} to {customer} at {address}. Estimated delivery time: {minutes} minutes", 
            deliveryRequest.OrderId, deliveryRequest.CustomerName, deliveryRequest.DeliveryAddress, deliveryRequest.DeliveryDurationMinutes);

        // Step 1: Assign a driver to the delivery
        var driverAssignment = await context.CallActivityAsync<string>(
            nameof(AssignDriverActivity.AssignDriver), deliveryRequest);
        logger.LogInformation("Driver assigned: {driver}", driverAssignment);

        // Step 2: Driver picks up the pizza from the restaurant
        var pickup = await context.CallActivityAsync<string>(
            nameof(PickupPizzaActivity.PickupPizza), deliveryRequest);
        logger.LogInformation("Pizza picked up: {pickup}", pickup);

        // Step 3: Drive to customer location (with random delay between 2-3 minutes)
        var random = new Random(deliveryRequest.OrderId.GetHashCode());
        int deliveryMinutes = random.Next(2, 4); // Random duration between 2-3 minutes
        
        logger.LogInformation("Driving to customer... (ETA: {minutes} minutes)", deliveryMinutes);
        await context.CreateTimer(context.CurrentUtcDateTime.AddMinutes(deliveryMinutes), CancellationToken.None);

        var driving = await context.CallActivityAsync<string>(
            nameof(DriveToCustomerActivity.DriveToCustomer), deliveryRequest);
        logger.LogInformation("Arrived at location: {driving}", driving);

        // Step 4: Deliver the pizza to the customer
        var delivery = await context.CallActivityAsync<string>(
            nameof(DeliverToCustomerActivity.DeliverToCustomer), deliveryRequest);
        logger.LogInformation("Pizza delivered: {delivery}", delivery);

        // Step 5: Return to restaurant
        var returnTrip = await context.CallActivityAsync<string>(
            nameof(ReturnToRestaurantActivity.ReturnToRestaurant), driverAssignment);
        logger.LogInformation("Driver returned: {return}", returnTrip);

        var completionMessage = $"✅ Order {deliveryRequest.OrderId} delivered to {deliveryRequest.CustomerName} at {deliveryRequest.DeliveryAddress}";
        logger.LogInformation("Delivery complete: {message}", completionMessage);

        return completionMessage;
    }

    [Function(nameof(DeliverPizzasInParallel))]
    public static async Task<List<string>> DeliverPizzasInParallel(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(DeliverPizzasInParallel));
        
        // Get the batch request from input
        var batchRequest = context.GetInput<DeliveryBatchRequest>()!;
        var deliveries = batchRequest.Deliveries;
        
        logger.LogInformation("Starting parallel delivery for {count} deliveries with max 3 concurrent.", deliveries.Count);

        var completedDeliveries = new List<string>();
        const int maxConcurrent = 3;

        // Process deliveries in batches of up to 3 concurrent
        for (int i = 0; i < deliveries.Count; i += maxConcurrent)
        {
            var batch = deliveries.Skip(i).Take(maxConcurrent).ToList();
            logger.LogInformation("Processing batch of {batchSize} deliveries (deliveries {start}-{end} of {total})", 
                batch.Count, i + 1, Math.Min(i + batch.Count, deliveries.Count), deliveries.Count);

            // Start all deliveries in this batch in parallel
            var tasks = batch.Select(delivery => 
                context.CallSubOrchestratorAsync<string>(nameof(DeliverPizza), delivery)
            ).ToList();

            // Wait for all deliveries in this batch to complete
            var results = await Task.WhenAll(tasks);
            completedDeliveries.AddRange(results);

            logger.LogInformation("Completed batch. Total deliveries done: {completed} of {total}", 
                completedDeliveries.Count, deliveries.Count);
        }

        logger.LogInformation("All {count} deliveries completed!", completedDeliveries.Count);
        return completedDeliveries;
    }
}
