using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Azurenaut.Orchestration.Delivery;

public static class DriveToCustomerActivity
{
    [Function(nameof(DriveToCustomer))]
    public static string DriveToCustomer([ActivityTrigger] DeliveryRequest request, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(DriveToCustomer));
        
        logger.LogInformation("Arrived at {address} for order {orderId}", 
            request.DeliveryAddress, request.OrderId);
        
        return $"Arrived at {request.DeliveryAddress}";
    }
}
