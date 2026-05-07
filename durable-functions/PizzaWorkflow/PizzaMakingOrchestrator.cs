using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Azurenaut.Orchestration.PizzaMaking;

public class PizzaBatchRequest
{
    public List<string> PizzaTypes { get; set; } = new();
}

public static class PizzaMakingOrchestrator
{
    [Function(nameof(MakePizza))]
    public static async Task<string> MakePizza(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(MakePizza));
        logger.LogInformation("Starting pizza making workflow.");

        // Get pizza order details from input
        var pizzaOrder = context.GetInput<string>() ?? "Margherita";
        
        // Step 1: Prepare the dough
        var dough = await context.CallActivityAsync<string>(
            nameof(PrepareDoughActivity.PrepareDough), pizzaOrder);
        logger.LogInformation("Dough prepared: {dough}", dough);

        // Step 2: Add sauce
        var sauced = await context.CallActivityAsync<string>(
            nameof(AddSauceActivity.AddSauce), dough);
        logger.LogInformation("Sauce added: {sauced}", sauced);

        // Step 3: Add cheese
        var cheesed = await context.CallActivityAsync<string>(
            nameof(AddCheeseActivity.AddCheese), sauced);
        logger.LogInformation("Cheese added: {cheesed}", cheesed);

        // Step 4: Add toppings
        var topped = await context.CallActivityAsync<string>(
            nameof(AddToppingsActivity.AddToppings), new { Pizza = cheesed, Order = pizzaOrder });
        logger.LogInformation("Toppings added: {topped}", topped);

        // Step 5: Bake the pizza
        var baked = await context.CallActivityAsync<string>(
            nameof(BakePizzaActivity.BakePizza), topped);
        logger.LogInformation("Pizza baked: {baked}", baked);

        // Step 6: Cut the pizza
        var cut = await context.CallActivityAsync<string>(
            nameof(CutPizzaActivity.CutPizza), baked);
        logger.LogInformation("Pizza cut: {cut}", cut);

        // Step 7: Box the pizza
        var boxed = await context.CallActivityAsync<string>(
            nameof(BoxPizzaActivity.BoxPizza), cut);
        logger.LogInformation("Pizza boxed and ready: {boxed}", boxed);

        return boxed;
    }

    [Function(nameof(MakePizzasInParallel))]
    public static async Task<List<string>> MakePizzasInParallel(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        ILogger logger = context.CreateReplaySafeLogger(nameof(MakePizzasInParallel));
        
        // Get the batch request from input
        var batchRequest = context.GetInput<PizzaBatchRequest>()!;
        var pizzaTypes = batchRequest.PizzaTypes;
        
        logger.LogInformation("Starting parallel pizza making for {count} pizzas with max 5 concurrent.", pizzaTypes.Count);

        var completedPizzas = new List<string>();
        const int maxConcurrent = 5;

        // Process pizzas in batches of up to 5 concurrent
        for (int i = 0; i < pizzaTypes.Count; i += maxConcurrent)
        {
            var batch = pizzaTypes.Skip(i).Take(maxConcurrent).ToList();
            logger.LogInformation("Processing batch of {batchSize} pizzas (pizzas {start}-{end} of {total})", 
                batch.Count, i + 1, Math.Min(i + batch.Count, pizzaTypes.Count), pizzaTypes.Count);

            // Start all pizzas in this batch in parallel
            var tasks = batch.Select(pizzaType => 
                context.CallSubOrchestratorAsync<string>(nameof(MakePizza), pizzaType)
            ).ToList();

            // Wait for all pizzas in this batch to complete
            var results = await Task.WhenAll(tasks);
            completedPizzas.AddRange(results);

            logger.LogInformation("Completed batch. Total pizzas done: {completed} of {total}", 
                completedPizzas.Count, pizzaTypes.Count);
        }

        logger.LogInformation("All {count} pizzas completed!", completedPizzas.Count);
        return completedPizzas;
    }
}
