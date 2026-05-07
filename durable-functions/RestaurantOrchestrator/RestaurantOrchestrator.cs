using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Azurenaut.Orchestration.PizzaMaking;
using Azurenaut.Orchestration.Delivery;

namespace Azurenaut.Orchestration.Restaurant;

public class PizzaOrder
{
    public string OrderId { get; set; } = string.Empty;
    public string PizzaType { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public int DelaySeconds { get; set; } = 0;
}

public class RestaurantOrderBatch
{
    public List<PizzaOrder> Orders { get; set; } = new();
}

public static class RestaurantOrchestrator
{
    [Function(nameof(ProcessRestaurantOrders))]
    public static async Task<List<string>> ProcessRestaurantOrders(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(ProcessRestaurantOrders));
        
        // Get the order batch from input
        var orderBatch = context.GetInput<RestaurantOrderBatch>()!;
        
        logger.LogInformation("Restaurant received {count} pizza orders", 
            orderBatch.Orders.Count);

        var completedOrders = new List<string>();
        const int maxConcurrentOrders = 3;

        // Process orders in batches of up to 3 concurrent orders
        for (int i = 0; i < orderBatch.Orders.Count; i += maxConcurrentOrders)
        {
            // Get the next batch of orders (up to 3)
            var batchOrders = orderBatch.Orders
                .Skip(i)
                .Take(maxConcurrentOrders)
                .ToList();

            logger.LogInformation("Processing batch of {count} orders concurrently (orders {start}-{end})", 
                batchOrders.Count, i + 1, i + batchOrders.Count);

            // Process this batch of orders in parallel
            var orderTasks = batchOrders.Select(async order =>
            {
                logger.LogInformation("Order {orderId} for {customer}: {pizzaType} - waiting {delay} seconds before starting", 
                    order.OrderId, order.CustomerName, order.PizzaType, order.DelaySeconds);

                // Wait for the specified delay before processing this order
                if (order.DelaySeconds > 0)
                {
                    await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(order.DelaySeconds), CancellationToken.None);
                }

                logger.LogInformation("Starting pizza order {orderId} for {customer}: {pizzaType}", 
                    order.OrderId, order.CustomerName, order.PizzaType);

                // Call the PizzaMakingOrchestrator sub-orchestrator
                var pizzaResult = await context.CallSubOrchestratorAsync<string>(
                    "MakePizza", 
                    order.PizzaType);

                logger.LogInformation("Pizza ready for order {orderId}: {result}", order.OrderId, pizzaResult);

                // Now send for delivery by calling the DeliveryOrchestrator sub-orchestrator
                // Generate a deterministic delivery duration based on order ID (1-5 minutes)
                var deliveryDuration = (Math.Abs(order.OrderId.GetHashCode()) % 5) + 1;
                
                var deliveryRequest = new DeliveryRequest
                {
                    OrderId = order.OrderId,
                    CustomerName = order.CustomerName,
                    DeliveryAddress = order.DeliveryAddress,
                    PizzaDescription = pizzaResult,
                    DeliveryDurationMinutes = deliveryDuration
                };

                var deliveryResult = await context.CallSubOrchestratorAsync<string>(
                    "DeliverPizza",
                    deliveryRequest);

                var completedOrder = $"Order {order.OrderId} ({order.CustomerName}): {pizzaResult} → {deliveryResult}";
                
                logger.LogInformation("Completed order {orderId}: {result}", order.OrderId, completedOrder);
                
                return completedOrder;
            });

            // Wait for all orders in this batch to complete
            var batchResults = await Task.WhenAll(orderTasks);
            completedOrders.AddRange(batchResults);

            logger.LogInformation("Batch complete. {count} orders finished", batchResults.Length);
        }

        logger.LogInformation("All {count} orders completed", 
            completedOrders.Count);

        return completedOrders;
    }
}
