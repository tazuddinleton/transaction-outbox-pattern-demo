using System.Text.Json;
using Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace ServiceA.Outbox;

/// Simple dispatcher polling the Outbox table and publishing via MassTransit.
/// We publish to an exchange (not a queue) so multiple services can bind their own queues with routing keys.
public class OutboxDispatcher : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly IPublishEndpoint _publish;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(3);

    public OutboxDispatcher(IServiceProvider services, ILogger<OutboxDispatcher> logger, IPublishEndpoint publish)
    {
        _services = services;
        _logger = logger;
        _publish = publish;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Dispatch(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Outbox dispatch failed"); }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task Dispatch(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();

        var messages = await db.OutboxMessages
            .Where(x => !x.Processed)
            .OrderBy(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var msg in messages)
        {
            try
            {
                switch (msg.EventType)
                {
                    case nameof(OrderCreated):
                        var evt = JsonSerializer.Deserialize<OrderCreated>(msg.Payload, JsonOptions);
                        if (evt != null)
                        {
                            // Routing key comes from the outbox record (e.g., order.created)
                            await _publish.Publish(evt, ctx => ctx.SetRoutingKey(msg.RoutingKey), ct);
                        }
                        break;
                    default:
                        _logger.LogWarning("Unknown event type {Type}", msg.EventType);
                        break;
                }

                msg.Processed = true;
                msg.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish outbox message {Id}", msg.Id);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}

