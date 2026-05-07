# Restaurant Orchestrator

This Durable Functions project orchestrates the complete pizza restaurant workflow: receiving orders, making pizzas, and delivering them to customers.

## How It Works

1. The `RestaurantOrchestrator` receives a batch of pizza orders
2. Each order can specify a `delaySeconds` (5-120 seconds) before starting
3. After the delay, it calls the `PizzaMakingOrchestrator` as a **sub-orchestrator** to make the pizza
4. Once the pizza is boxed and ready, it calls the `DeliveryOrchestrator` as a **sub-orchestrator** to deliver it
5. Each delivery has a random duration (1-5 minutes)
6. Returns all completed orders with their delivery confirmations

This simulates a realistic end-to-end restaurant workflow from order to delivery.

## Project References

- **PizzaWorkflow** - Handles pizza making (dough, sauce, cheese, toppings, baking, cutting, boxing)
- **DeliveryWorkflow** - Handles delivery (driver assignment, pickup, driving, delivery, return)

## Testing

### Start the Functions Host

```bash
cd /Users/romerve/Github/Azurenaut/durable-functions/RestaurantOrchestrator
func start
```

The host will run on port **7072** (different from PizzaWorkflow on 7071).

### Send a Sample Order

```bash
curl -X POST http://localhost:7072/api/Restaurant_HttpStart \
  -H "Content-Type: application/json" \
  -d '{
    "orders": [
      {
        "orderId": "001",
        "customerName": "Alice",
        "pizzaType": "Margherita",
        "deliveryAddress": "123 Main St, Apt 5B",
        "delaySeconds": 15
      },
      {
        "orderId": "002",
        "customerName": "Bob",
        "pizzaType": "Pepperoni",
        "deliveryAddress": "456 Oak Avenue",
        "delaySeconds": 45
      },
      {
        "orderId": "003",
        "customerName": "Carol",
        "pizzaType": "Supreme",
        "deliveryAddress": "789 Elm Street",
        "delaySeconds": 8
      }
    ]
  }'
```

Or use the sample file with 14 orders:

```bash
curl -X POST http://localhost:7072/api/Restaurant_HttpStart \
  -H "Content-Type: application/json" \
  -d @sample-order.json
```

### Check Status

Use the `statusQueryGetUri` from the response to monitor progress:

```bash
curl http://localhost:7072/runtime/webhooks/durabletask/instances/{instanceId}
```

## Project Structure

- `RestaurantOrchestrator.cs` - Main orchestrator that processes order batches, coordinates pizza making AND delivery
- `RestaurantHttpTrigger.cs` - HTTP endpoint to receive orders
- References the `PizzaWorkflow` project to call `PizzaMakingOrchestrator`
- References the `DeliveryWorkflow` project to call `DeliveryOrchestrator`

## Workflow Flow

```
Order Received → [Delay] → Make Pizza (PizzaWorkflow) → Deliver Pizza (DeliveryWorkflow) → Complete
```

Each order goes through:
1. **Order delay** (configurable seconds)
2. **Pizza making** (7 steps: dough, sauce, cheese, toppings, bake, cut, box)
3. **Delivery** (5 steps: assign driver, pickup, drive, deliver, return) with 1-5 min random drive time

## Sample Request File

See `sample-order.json` for a complete example.
