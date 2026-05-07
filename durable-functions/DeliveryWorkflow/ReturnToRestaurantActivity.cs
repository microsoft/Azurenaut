using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Azurenaut.Orchestration.Delivery;

public static class ReturnToRestaurantActivity
{
    [Function(nameof(ReturnToRestaurant))]
    public static string ReturnToRestaurant([ActivityTrigger] string driverInfo, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(ReturnToRestaurant));
        
        logger.LogInformation("{driver} returning to restaurant", driverInfo);
        
        return $"{driverInfo} returned to restaurant and is ready for next delivery";
    }
}
