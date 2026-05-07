using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Azurenaut.Orchestration.PizzaMaking;

public static class BakePizzaActivity
{
    [Function(nameof(BakePizza))]
    public static async Task<string> BakePizza([ActivityTrigger] string preppedPizza, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(BakePizza));
        logger.LogInformation("Baking pizza at 450°F for 12-15 minutes.");
        
        // Simulate baking with random delay (5-10 seconds)
        var random = new Random();
        var delaySeconds = random.Next(5, 11);
        logger.LogInformation("Baking will take {seconds} seconds.", delaySeconds);
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        
        return $"Baked pizza: ({preppedPizza})";
    }
}
