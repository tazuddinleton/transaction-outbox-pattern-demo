# Transaction Outbox Demo

A .NET 9.0 ASP.NET Core Web API demonstration of the **Transaction Outbox Pattern** for reliable event publishing in distributed systems.

## Overview

This project demonstrates how to implement the Transaction Outbox Pattern, which ensures that domain events are reliably published to a message broker (RabbitMQ) while maintaining transactional consistency with your database operations.

### What is the Transaction Outbox Pattern?

The Transaction Outbox Pattern solves the problem of reliably publishing events to a message broker when you need to ensure that:
- Domain events are saved atomically with business data
- Events are eventually published even if the message broker is temporarily unavailable
- No events are lost or duplicated

## Architecture

The solution consists of several key components:

1. **Domain Events**: Domain entities raise events when business operations occur
2. **EF Core Interceptor**: Automatically captures domain events and saves them to an outbox table in the same database transaction
3. **Outbox Table**: Stores domain events as JSON in PostgreSQL (using JSONB column type)
4. **Background Service**: Periodically polls the outbox table and publishes unprocessed messages to RabbitMQ
5. **Unit of Work**: Manages database transactions to ensure atomicity

### Flow

```
1. API Request → Create Order
2. Domain Event Raised (OrderCreatedEvent)
3. EF Core Interceptor Captures Event
4. Event Saved to Outbox Table (same transaction as Order)
5. Transaction Committed
6. Background Service Polls Outbox
7. Messages Published to RabbitMQ
8. Outbox Records Marked as Processed
```

## Technology Stack

- **.NET 9.0** - Application framework
- **ASP.NET Core** - Web API framework
- **Entity Framework Core 9.0** - ORM
- **PostgreSQL** - Database (with JSONB support)
- **RabbitMQ** - Message broker
- **Swagger/OpenAPI** - API documentation

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker](https://www.docker.com/get-started) and Docker Compose

## Setup Instructions

### 1. Clone the Repository

```bash
git clone <repository-url>
cd TransactionOutboxDemo
```

### 2. Start Infrastructure Services

Start PostgreSQL and RabbitMQ using Docker Compose:

```bash
docker-compose up -d
```

This will start:
- **PostgreSQL** on port `5432`
  - Database: `transactionoutbox`
  - Username: `postgres`
  - Password: `postgres`
- **RabbitMQ** on ports `5672` (AMQP) and `15672` (Management UI)
  - Username: `guest`
  - Password: `guest`

### 3. Run Database Migrations

```bash
dotnet ef database update
```

Or, if you prefer to create a new migration:

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

The application will also automatically ensure the database is created on first run using `EnsureCreated()`.

### 4. Run the Application

```bash
dotnet run
```

The API will be available at `https://localhost:5001` or `http://localhost:5000` (depending on your configuration).

### 5. Access Swagger UI

In development mode, Swagger UI is available at the root URL:
- `https://localhost:5001/` or `http://localhost:5000/`

## API Endpoints

### Orders

- **POST** `/api/order` - Create a new order
  ```json
  {
    "customerName": "John Doe",
    "customerEmail": "john@example.com",
    "items": [
      {
        "productId": 1,
        "quantity": 2
      },
      {
        "productId": 2,
        "quantity": 1
      }
    ]
  }
  ```

- **GET** `/api/order/{id}` - Get an order by ID
- **GET** `/api/order` - Get all orders

### Products

The database comes pre-seeded with sample products:
- Laptop ($1,299.99)
- Mouse ($29.99)
- Keyboard ($79.99)
- Monitor ($399.99)
- USB Cable ($9.99)

## How It Works

### 1. Domain Events

Entities inherit from the `Entity` base class and can raise domain events:

```csharp
var order = Order.Create(customerName, customerEmail, orderItems);
// This automatically raises an OrderCreatedEvent
```

### 2. Automatic Event Capture

The `DomainEventInterceptor` intercepts `SaveChanges` operations and:
- Collects all domain events from entities
- Creates `OutboxMessage` records
- Saves them in the same transaction
- Updates event payloads with entity IDs after save

### 3. Outbox Processing

The `OutboxProcessorService` background service:
- Polls for unprocessed outbox messages every 5 seconds
- Publishes messages to RabbitMQ exchange `domain-events`
- Marks messages as processed after successful publication
- Automatically retries failed messages on the next polling cycle

### 4. Transaction Management

The `UnitOfWork` pattern ensures that:
- Order creation and outbox message saving happen atomically
- If any part fails, the entire transaction is rolled back
- Events are never saved without their corresponding business data

## Monitoring

### RabbitMQ Management UI

Access the RabbitMQ Management UI to monitor published messages:

- URL: `http://localhost:15672`
- Username: `guest`
- Password: `guest`

Navigate to the **Exchanges** tab and look for the `domain-events` exchange.

### Database Inspection

You can query the outbox table to see pending and processed messages:

```sql
SELECT * FROM "OutboxMessages" ORDER BY "CreatedAt" DESC;
```

## Project Structure

```
TransactionOutboxDemo/
├── Controllers/          # API controllers
├── Db/                   # Database context, migrations, outbox
│   ├── DomainEventInterceptor.cs    # Captures domain events
│   ├── OrderDbContext.cs            # EF Core context
│   ├── OutboxMessage.cs             # Outbox entity
│   └── UnitOfWork.cs                # Transaction management
├── Domain/              # Domain models and events
│   ├── Entity.cs                    # Base entity class
│   ├── Events/                      # Domain event classes
│   └── Order.cs, Product.cs, etc.   # Domain entities
├── Services/            # Background services
│   └── OutboxProcessorService.cs    # Processes outbox messages
├── Program.cs           # Application startup
└── docker-compose.yml   # Infrastructure configuration
```

## Configuration

Configuration is stored in `appsettings.json` and `appsettings.Development.json`:

- **ConnectionStrings**: PostgreSQL connection string
- **RabbitMQ**: RabbitMQ connection settings (host, port, credentials)

## Testing the Pattern

1. **Create an Order**: Use Swagger UI or curl to POST a new order
2. **Check Outbox**: Query the `OutboxMessages` table to see the event was saved
3. **Check RabbitMQ**: View the Management UI to see the message was published
4. **Verify Processing**: Check that the outbox message is marked as processed

### Example Test Scenario

1. Start the application and ensure RabbitMQ is running
2. Create an order via the API
3. Within 5 seconds, the event should appear in RabbitMQ
4. Stop RabbitMQ and create another order
5. The order will be created, but the event will remain in the outbox as unprocessed
6. Start RabbitMQ again
7. Within 5 seconds, the pending event will be published

This demonstrates the reliability of the pattern: business operations succeed even if the message broker is unavailable, and events are eventually published.

## Key Features

✅ **Automatic Event Capture** - Domain events are automatically saved to the outbox via EF Core interceptor  
✅ **Transactional Consistency** - Events are saved in the same transaction as business data  
✅ **Reliable Publishing** - Background service ensures events are eventually published  
✅ **Failure Resilience** - Failed publishes are retried automatically  
✅ **Type Safety** - Strongly-typed domain events  
✅ **JSON Storage** - Events stored as JSONB in PostgreSQL for flexibility  

## Future Enhancements

- [ ] Add more domain events (OrderCancelled, OrderShipped, etc.)
- [ ] Implement idempotency checks
- [ ] Add metrics and monitoring
- [ ] Implement event versioning
- [ ] Add dead letter queue handling
- [ ] Support for multiple event types and routing

## License

This is a demo project for educational purposes.

## References

- [Transaction Outbox Pattern](https://microservices.io/patterns/data/transactional-outbox.html)
- [Domain Events Pattern](https://martinfowler.com/eaaDev/DomainEvent.html)
- [Unit of Work Pattern](https://martinfowler.com/eaaCatalog/unitOfWork.html)

