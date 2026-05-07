using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Azurenaut.Orchestration.Delivery;

public static class AssignDriverActivity
{
    private static readonly string[] Drivers = 
    {
        "Mike", "Sarah", "Carlos", "Emily", "Ahmed", "Lisa", "David", "Maria"
    };

    [Function(nameof(AssignDriver))]
    public static string AssignDriver([ActivityTrigger] DeliveryRequest request, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(AssignDriver));
        
        // Randomly assign a driver
        var random = new Random();
        var driver = Drivers[random.Next(Drivers.Length)];
        
        logger.LogInformation("Assigned driver {driver} to order {orderId}", driver, request.OrderId);
        
        return $"Driver {driver}";
    }
}
