using System.Text.Json;
using Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using ServiceA.Outbox;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OutboxDbContext>(opt =>
    opt.UseInMemoryDatabase("outbox-demo")); // swap for real DB in production

builder.Services.AddScoped<IOutboxPublisher, OutboxPublisher>();

// Configure MassTransit to use RabbitMQ topic exchange "app.events"
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h => { });

        // Use topic exchange for published messages so routing keys matter
        cfg.Publish<OrderCreated>(p =>
        {
            p.ExchangeType = ExchangeType.Topic;
        });

        // Set exchange name explicitly
        cfg.Message<OrderCreated>(m => m.SetEntityName("app.events"));
    });
});

builder.Services.AddHostedService<OutboxDispatcher>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

// Minimal API to create an order -> writes to Outbox (simulating Service A domain logic)
app.MapPost("/orders", async (string customerEmail, decimal totalAmount, IOutboxPublisher publisher) =>
{
    var orderId = Guid.NewGuid();
    var evt = new OrderCreated(orderId, customerEmail, totalAmount, DateTime.UtcNow);

    // Why outbox? Persist event with business data in one transaction, then dispatch asynchronously.
    await publisher.EnqueueAsync(evt);

    return Results.Ok(new { orderId, status = "queued" });
});

app.Run();
