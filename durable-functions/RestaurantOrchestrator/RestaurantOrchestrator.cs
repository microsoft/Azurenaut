using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Azurenaut.Orchestration.PizzaMaking;
using Azurenaut.Orchestration.Delivery;

namespace Azurenaut.Orchestration.Restaurant;

public class PizzaOrder
{
    public string OrderId { get; set; } = string.Empty;
    public List<string> PizzaTypes { get; set; } = new();
    public string CustomerName { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
}

public static class RestaurantOrchestrator
{
    [Function(nameof(ProcessRestaurantOrders))]
    public static async Task<string> ProcessRestaurantOrders(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(ProcessRestaurantOrders));

        var order = context.GetInput<PizzaOrder>()!;

        logger.LogInformation("Restaurant received order {orderId} for {customer} with {count} pizza(s)",
            order.OrderId, order.CustomerName, order.PizzaTypes.Count);

        // Make all pizzas in the order concurrently
        var pizzaTasks = order.PizzaTypes.Select(pizzaType =>
            context.CallSubOrchestratorAsync<string>("MakePizza", pizzaType));

        var pizzaResults = await Task.WhenAll(pizzaTasks);

        logger.LogInformation("All {count} pizza(s) ready for order {orderId}", pizzaResults.Length, order.OrderId);

        // Deliver all pizzas together
        var deliveryDuration = (Math.Abs(order.OrderId.GetHashCode()) % 5) + 1;
        var pizzaDescription = string.Join(", ", pizzaResults);

        var deliveryRequest = new DeliveryRequest
        {
            OrderId = order.OrderId,
            CustomerName = order.CustomerName,
            DeliveryAddress = order.DeliveryAddress,
            PizzaDescription = pizzaDescription,
            DeliveryDurationMinutes = deliveryDuration
        };

        var deliveryResult = await context.CallSubOrchestratorAsync<string>("DeliverPizza", deliveryRequest);

        var completedOrder = $"Order {order.OrderId} ({order.CustomerName}): {pizzaDescription} → {deliveryResult}";

        logger.LogInformation("Completed order {orderId}: {result}", order.OrderId, completedOrder);

        return completedOrder;
    }
}
