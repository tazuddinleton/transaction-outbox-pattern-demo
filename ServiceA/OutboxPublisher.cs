using System.Text.Json;
using Contracts;

namespace ServiceA.Outbox;

/// Generic outbox writer for integration events so other services can reuse this pattern.
public interface IOutboxPublisher
{
    Task EnqueueAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IntegrationEvent;
}

public class OutboxPublisher : IOutboxPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly OutboxDbContext _dbContext;

    public OutboxPublisher(OutboxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnqueueAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IntegrationEvent
    {
        var clrType = @event.GetType().AssemblyQualifiedName
            ?? throw new InvalidOperationException("Event CLR type could not be resolved.");

        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = @event.GetType().Name,
            ClrType = clrType,
            RoutingKey = @event.RoutingKey,
            Payload = JsonSerializer.Serialize<object?>(@event, JsonOptions),
            CreatedAt = @event.OccurredOn,
            Processed = false
        };

        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync(ct);
    }
}

