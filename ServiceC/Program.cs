using Contracts;
using MassTransit;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h => { });

        // Bind this queue only to order.created
        cfg.ReceiveEndpoint("service-c-order-created", e =>
        {
            e.Bind("app.events", s =>
            {
                s.ExchangeType = RabbitMQ.Client.ExchangeType.Topic;
                s.RoutingKey = "order.created";
            });

            e.ConfigureConsumer<OrderCreatedConsumer>(context);
        });
    });
});

var host = builder.Build();
host.Run();

class OrderCreatedConsumer : IConsumer<OrderCreated>
{
    public Task Consume(ConsumeContext<OrderCreated> context)
    {
        Console.WriteLine($"[Service C] Received order.created: {context.Message.OrderId}");
        return Task.CompletedTask;
    }
}
