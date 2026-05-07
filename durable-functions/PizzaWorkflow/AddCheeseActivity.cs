using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Azurenaut.Orchestration.PizzaMaking;

public static class AddCheeseActivity
{
    [Function(nameof(AddCheese))]
    public static async Task<string> AddCheese([ActivityTrigger] string saucedPizza, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(AddCheese));
        logger.LogInformation("Adding mozzarella cheese.");
        
        // Simulate adding cheese with random delay (5-10 seconds)
        var random = new Random();
        var delaySeconds = random.Next(5, 11);
        logger.LogInformation("Adding cheese will take {seconds} seconds.", delaySeconds);
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        
        return $"{saucedPizza} + mozzarella cheese";
    }
}
