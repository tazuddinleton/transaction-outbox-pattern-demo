using System.Text.Json;
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
                var eventType = Type.GetType(msg.ClrType);
                if (eventType == null)
                {
                    _logger.LogWarning("Unknown CLR type {Type} for message {MessageId}", msg.ClrType, msg.Id);
                    continue;
                }

                var evt = JsonSerializer.Deserialize(msg.Payload, eventType, JsonOptions);
                if (evt == null)
                {
                    _logger.LogWarning("Failed to deserialize payload for {Type} message {MessageId}", msg.ClrType, msg.Id);
                    continue;
                }

                await _publish.Publish(evt, eventType, ctx => ctx.SetRoutingKey(msg.RoutingKey), ct);

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

