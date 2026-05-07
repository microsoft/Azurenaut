using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Azurenaut.Orchestration.PizzaMaking;

public static class PrepareDoughActivity
{
    [Function(nameof(PrepareDough))]
    public static async Task<string> PrepareDough([ActivityTrigger] string pizzaType, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(PrepareDough));
        logger.LogInformation("Preparing dough for {pizzaType} pizza.", pizzaType);
        
        // Simulate dough preparation with random delay (5-10 seconds)
        var random = new Random();
        var delaySeconds = random.Next(5, 11);
        logger.LogInformation("Dough preparation will take {seconds} seconds.", delaySeconds);
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        
        return $"Fresh dough prepared for {pizzaType} pizza";
    }
}
