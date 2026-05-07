using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Azurenaut.Orchestration.PizzaMaking;

public static class BoxPizzaActivity
{
    [Function(nameof(BoxPizza))]
    public static async Task<string> BoxPizza([ActivityTrigger] string cutPizza, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(BoxPizza));
        logger.LogInformation("Boxing pizza for delivery.");
        
        // Simulate boxing with random delay (5-10 seconds)
        var random = new Random();
        var delaySeconds = random.Next(5, 11);
        logger.LogInformation("Boxing will take {seconds} seconds.", delaySeconds);
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        
        return $"📦 {cutPizza} - Ready for delivery! 🍕";
    }
}
