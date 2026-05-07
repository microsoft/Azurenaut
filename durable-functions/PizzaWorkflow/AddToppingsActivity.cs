using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Azurenaut.Orchestration.PizzaMaking;

public static class AddToppingsActivity
{
    [Function(nameof(AddToppings))]
    public static async Task<string> AddToppings([ActivityTrigger] object input, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(AddToppings));
        
        // Parse input
        var inputJson = JsonSerializer.Deserialize<JsonElement>(input.ToString()!);
        string pizza = inputJson.GetProperty("Pizza").GetString()!;
        string pizzaType = inputJson.GetProperty("Order").GetString()!;
        
        // Determine toppings based on pizza type
        string toppings = pizzaType.ToLower() switch
        {
            "margherita" => "fresh basil",
            "pepperoni" => "pepperoni slices",
            "hawaiian" => "ham + pineapple",
            "veggie" => "bell peppers + mushrooms + onions",
            "supreme" => "pepperoni + sausage + peppers + onions + mushrooms",
            _ => "oregano"
        };
        
        logger.LogInformation("Adding toppings: {toppings} for {pizzaType} pizza.", toppings, pizzaType);
        
        // Simulate adding toppings with random delay (5-10 seconds)
        var random = new Random();
        var delaySeconds = random.Next(5, 11);
        logger.LogInformation("Adding toppings will take {seconds} seconds.", delaySeconds);
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        
        return $"{pizza} + {toppings}";
    }
}
