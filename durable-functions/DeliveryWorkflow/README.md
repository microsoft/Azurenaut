# Delivery Workflow

This Durable Functions project orchestrates pizza delivery with realistic timing, driver assignment, and support for parallel deliveries.

## Features

- **Individual Delivery**: Single delivery with deterministic or random timing
- **Parallel Batch Delivery**: Process up to 3 deliveries concurrently
- **Random Delivery Duration**: Each delivery takes 2-3 minutes (random)
- **Driver Pool**: Randomly assigns from 8 available drivers
- **Complete Workflow**: Pickup → Drive → Deliver → Return

## Delivery Process

Each delivery follows these steps:
1. **Assign Driver** - Randomly select from 8 drivers
2. **Pickup Pizza** - Driver picks up from restaurant
3. **Drive to Customer** - Random 2-3 minutes
4. **Deliver to Customer** - Hand off pizza
5. **Return to Restaurant** - Driver returns

**Delivery Duration**: Each delivery takes a random 2-3 minutes for the drive step.

## Usage

### Individual Delivery

```bash
POST http://localhost:7071/api/Delivery_HttpStart
Content-Type: application/json

{
  "OrderId": "D001",
  "CustomerName": "John Doe",
  "DeliveryAddress": "123 Main St, Apt 5B",
  "PizzaDescription": "Large Pepperoni Pizza"
}
```

### Parallel Deliveries (Max 3 Concurrent)

```bash
POST http://localhost:7071/api/DeliveryParallel_HttpStart
Content-Type: application/json

{
  "Deliveries": [
    {
      "OrderId": "PD001",
      "CustomerName": "Alice",
      "DeliveryAddress": "111 First Ave",
      "PizzaDescription": "Large Supreme"
    },
    {
      "OrderId": "PD002",
      "CustomerName": "Bob",
      "DeliveryAddress": "222 Second St",
      "PizzaDescription": "Medium Margherita"
    },
    {
      "OrderId": "PD003",
      "CustomerName": "Carol",
      "DeliveryAddress": "333 Third Blvd",
      "PizzaDescription": "Large Hawaiian"
    }
  ]
}
```

## Parallel Processing Behavior

The `DeliverPizzasInParallel` orchestrator:
- Processes deliveries in batches of up to 3 concurrent operations
- If you request 5 deliveries, it processes 3 in parallel, then 2 more
- If you request 7 deliveries, it processes in 3 batches: 3+3+1
- Each batch waits for all deliveries to complete before starting the next

### Example Timing

**5 Deliveries**:
- Batch 1 (3 deliveries): 2-3 minutes each (parallel)
- Batch 2 (2 deliveries): 2-3 minutes each (parallel)
- **Total**: 4-6 minutes

**7 Deliveries**:
- Batch 1 (3 deliveries): 2-3 minutes
- Batch 2 (3 deliveries): 2-3 minutes
- Batch 3 (1 delivery): 2-3 minutes
- **Total**: 6-9 minutes

## Testing

### Start the Functions Host

```bash
cd /Users/romerve/Github/Azurenaut/durable-functions/DeliveryWorkflow
func start
```

The host runs on port **7071**.

### Send Requests

Use the examples in `/durable-functions/req.http` or curl:

```bash
# Single delivery
curl -X POST http://localhost:7071/api/Delivery_HttpStart \
  -H "Content-Type: application/json" \
  -d '{
    "OrderId": "001",
    "CustomerName": "John Doe",
    "DeliveryAddress": "123 Main St",
    "PizzaDescription": "Large Pepperoni"
  }'

# Parallel deliveries
curl -X POST http://localhost:7071/api/DeliveryParallel_HttpStart \
  -H "Content-Type: application/json" \
  -d '{
    "Deliveries": [
      {"OrderId": "PD1", "CustomerName": "Alice", "DeliveryAddress": "111 First Ave", "PizzaDescription": "Large Supreme"},
      {"OrderId": "PD2", "CustomerName": "Bob", "DeliveryAddress": "222 Second St", "PizzaDescription": "Medium Margherita"},
      {"OrderId": "PD3", "CustomerName": "Carol", "DeliveryAddress": "333 Third Blvd", "PizzaDescription": "Large Hawaiian"}
    ]
  }'
```

### Check Status

Use the `statusQueryGetUri` from the response:

```bash
curl http://localhost:7071/runtime/webhooks/durabletask/instances/{instanceId}
```

## HTTP Endpoints

- `Delivery_HttpStart` - Start single delivery orchestration
- `DeliveryParallel_HttpStart` - Start parallel batch delivery orchestration

## Project Structure

### Orchestrators
- `DeliveryOrchestrator.cs` 
  - `DeliverPizza` - Single delivery orchestrator
  - `DeliverPizzasInParallel` - Parallel batch delivery orchestrator

### Activities (Each in separate file)
1. `AssignDriverActivity.cs` - Random driver assignment (8 drivers available)
2. `PickupPizzaActivity.cs` - Driver picks up pizza
3. `DriveToCustomerActivity.cs` - Drive to customer (displays arrival message)
4. `DeliverToCustomerActivity.cs` - Deliver pizza to customer
5. `ReturnToRestaurantActivity.cs` - Driver returns to restaurant

### HTTP Triggers
- `DeliveryHttpTrigger.cs` - Single delivery endpoint
- `ParallelDeliveryHttpTrigger.cs` - Parallel delivery endpoint

## Driver Pool

Drivers are randomly assigned from:
- Mike
- Sarah
- Carlos
- Emily
- Ahmed
- Lisa
- David
- Maria

## Features Summary

- ✅ **Random Driver Assignment**: Assigns from a pool of 8 drivers
- ✅ **Random Delivery Time**: 2-3 minutes per delivery (deterministic per order ID)
- ✅ **Parallel Processing**: Up to 3 concurrent deliveries
- ✅ **Complete Workflow**: Pickup → Drive → Deliver → Return
- ✅ **Realistic Simulation**: Random delays simulate actual driving time
