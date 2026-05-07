# Pizza Workflow

This Durable Functions project orchestrates pizza making workflows with support for both individual and parallel batch processing.

## Features

- **Individual Pizza Making**: Make one pizza at a time through the complete 7-step process
- **Parallel Batch Processing**: Make multiple pizzas in parallel with a maximum of 5 concurrent operations
- **Realistic Timing**: Each activity has a random duration of 5-10 seconds to simulate real pizza making

## Pizza Making Process

Each pizza goes through these 7 steps:
1. **Prepare Dough** (5-10 seconds)
2. **Add Sauce** (5-10 seconds)
3. **Add Cheese** (5-10 seconds)
4. **Add Toppings** (5-10 seconds) - varies by pizza type
5. **Bake Pizza** (5-10 seconds)
6. **Cut Pizza** (5-10 seconds)
7. **Box Pizza** (5-10 seconds)

Total time per pizza: 35-70 seconds

## Available Pizza Types

- **Margherita** - fresh basil
- **Pepperoni** - pepperoni slices
- **Hawaiian** - ham + pineapple
- **Veggie** - bell peppers + mushrooms + onions
- **Supreme** - pepperoni + sausage + peppers + onions + mushrooms

## Usage

### Make a Single Pizza

```bash
# GET request
GET http://localhost:7071/api/PizzaMaking_HttpStart?type=Margherita

# POST request
POST http://localhost:7071/api/PizzaMaking_HttpStart?type=Supreme
```

### Make Multiple Pizzas in Parallel (Max 5 Concurrent)

```bash
POST http://localhost:7071/api/PizzaMakingParallel_HttpStart
Content-Type: application/json

{
  "PizzaTypes": ["Margherita", "Pepperoni", "Hawaiian", "Supreme", "Veggie"]
}
```

## Parallel Processing Behavior

The `MakePizzasInParallel` orchestrator:
- Processes pizzas in batches of up to 5 concurrent operations
- If you request 8 pizzas, it will process 5 in parallel, then 3 more
- If you request 12 pizzas, it will process in 3 batches: 5+5+2
- Each batch waits for all pizzas in that batch to complete before starting the next batch

### Example Timing

**8 Pizzas**:
- Batch 1 (5 pizzas): 35-70 seconds
- Batch 2 (3 pizzas): 35-70 seconds  
- **Total**: 70-140 seconds

**12 Pizzas**:
- Batch 1 (5 pizzas): 35-70 seconds
- Batch 2 (5 pizzas): 35-70 seconds
- Batch 3 (2 pizzas): 35-70 seconds
- **Total**: 105-210 seconds

## HTTP Endpoints

- `PizzaMaking_HttpStart` - Start single pizza making orchestration
- `PizzaMakingParallel_HttpStart` - Start parallel batch pizza making orchestration

## Activities

All activities are in separate files with random timing:
- `PrepareDoughActivity.cs`
- `AddSauceActivity.cs`
- `AddCheeseActivity.cs`
- `AddToppingsActivity.cs`
- `BakePizzaActivity.cs`
- `CutPizzaActivity.cs`
- `BoxPizzaActivity.cs`

## Orchestrators

- `MakePizza` - Orchestrates a single pizza through all 7 steps
- `MakePizzasInParallel` - Orchestrates multiple pizzas with max 5 concurrent sub-orchestrators
