using Contracts;
using MassTransit;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderEventConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h => { });

        // Bind this queue to topic exchange with pattern order.*
        cfg.ReceiveEndpoint("service-b-order-events", e =>
        {
            e.Bind("app.events", s =>
            {
                s.ExchangeType = RabbitMQ.Client.ExchangeType.Topic;
                s.RoutingKey = "order.*";
            });

            e.ConfigureConsumer<OrderEventConsumer>(context);
        });
    });
});

var host = builder.Build();
host.Run();

class OrderEventConsumer : IConsumer<OrderCreated>
{
    public Task Consume(ConsumeContext<OrderCreated> context)
    {
        Console.WriteLine($"[Service B] Received order event (order.*): {context.Message.OrderId}");
        return Task.CompletedTask;
    }
}
