using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Azurenaut.Orchestration.Delivery;

public static class PickupPizzaActivity
{
    [Function(nameof(PickupPizza))]
    public static string PickupPizza([ActivityTrigger] DeliveryRequest request, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(PickupPizza));
        
        logger.LogInformation("Driver picking up order {orderId}: {pizza}", 
            request.OrderId, request.PizzaDescription);
        
        return $"Picked up order {request.OrderId}: {request.PizzaDescription}";
    }
}
