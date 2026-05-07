using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Azurenaut.Orchestration.PizzaMaking;

public static class CutPizzaActivity
{
    [Function(nameof(CutPizza))]
    public static async Task<string> CutPizza([ActivityTrigger] string bakedPizza, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(CutPizza));
        logger.LogInformation("Cutting pizza into 8 slices.");
        
        // Simulate cutting with random delay (5-10 seconds)
        var random = new Random();
        var delaySeconds = random.Next(5, 11);
        logger.LogInformation("Cutting will take {seconds} seconds.", delaySeconds);
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        
        return $"{bakedPizza} | Cut into 8 slices";
    }
}
