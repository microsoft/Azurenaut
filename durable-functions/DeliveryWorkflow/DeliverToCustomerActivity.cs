using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Azurenaut.Orchestration.Delivery;

public static class DeliverToCustomerActivity
{
    [Function(nameof(DeliverToCustomer))]
    public static string DeliverToCustomer([ActivityTrigger] DeliveryRequest request, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(DeliverToCustomer));
        
        logger.LogInformation("Delivering to {customer} at {address}", 
            request.CustomerName, request.DeliveryAddress);
        
        return $"🍕 Delivered to {request.CustomerName} at {request.DeliveryAddress}. Enjoy your pizza!";
    }
}
