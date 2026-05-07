using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Azurenaut.Orchestration.PizzaMaking;

public static class AddSauceActivity
{
    [Function(nameof(AddSauce))]
    public static async Task<string> AddSauce([ActivityTrigger] string dough, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(AddSauce));
        logger.LogInformation("Adding tomato sauce to the dough.");
        
        // Simulate adding sauce with random delay (5-10 seconds)
        var random = new Random();
        var delaySeconds = random.Next(5, 11);
        logger.LogInformation("Adding sauce will take {seconds} seconds.", delaySeconds);
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        
        return $"{dough} + tomato sauce";
    }
}
