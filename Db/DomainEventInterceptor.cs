using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TransactionOutboxDemo.Domain;
using TransactionOutboxDemo.Domain.Events;

namespace TransactionOutboxDemo.Db;

public class DomainEventInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
    
    private static bool _isUpdatingOutbox = false;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        SaveDomainEvents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SaveDomainEvents(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        UpdateOutboxMessagesWithEntityIds(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        UpdateOutboxMessagesWithEntityIds(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private static void SaveDomainEvents(DbContext? context)
    {
        if (context == null) return;

        var entities = context.ChangeTracker
            .Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var outboxMessages = new List<OutboxMessage>();
        var entityEventMap = new Dictionary<Entity, List<IDomainEvent>>();

        foreach (var entity in entities)
        {
            var events = entity.DomainEvents.ToList();
            entityEventMap[entity] = events;

            foreach (var domainEvent in events)
            {
                var outboxMessage = new OutboxMessage
                {
                    Id = domainEvent.EventId,
                    EventType = domainEvent.EventType,
                    Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions),
                    CreatedAt = domainEvent.OccurredOn,
                    Processed = false
                };

                outboxMessages.Add(outboxMessage);
            }

            entity.ClearDomainEvents();
        }

        if (outboxMessages.Any())
        {
            context.Set<OutboxMessage>().AddRange(outboxMessages);
            // Store mapping for later use in SavedChanges
            if (context is OrderDbContext orderContext)
            {
                orderContext._EntityEventMap = entityEventMap;
            }
        }
    }

    private static void UpdateOutboxMessagesWithEntityIds(DbContext? context)
    {
        if (context == null) return;
        if (context is not OrderDbContext orderContext) return;
        if (orderContext._EntityEventMap == null) return;
        if (_isUpdatingOutbox) return; // Prevent recursive calls

        // Get the outbox messages that were just saved
        var eventIds = orderContext._EntityEventMap.Values.SelectMany(e => e).Select(e => e.EventId).ToList();
        var outboxMessages = orderContext.OutboxMessages
            .Where(m => eventIds.Contains(m.Id))
            .ToList();

        bool hasUpdates = false;

        foreach (var (entity, events) in orderContext._EntityEventMap)
        {
            if (entity is Order order && order.Id > 0)
            {
                foreach (var domainEvent in events)
                {
                    if (domainEvent is OrderCreatedEvent orderCreatedEvent)
                    {
                        orderCreatedEvent.OrderId = order.Id;
                        var message = outboxMessages.FirstOrDefault(m => m.Id == domainEvent.EventId);
                        if (message != null)
                        {
                            message.Payload = JsonSerializer.Serialize(orderCreatedEvent, orderCreatedEvent.GetType(), JsonOptions);
                            hasUpdates = true;
                        }
                    }
                }
            }
        }

        // Save changes again if we updated any payloads
        if (hasUpdates)
        {
            try
            {
                _isUpdatingOutbox = true;
                orderContext.SaveChanges();
            }
            catch
            {
                // If save fails, the outbox processor will handle it with the initial payload
            }
            finally
            {
                _isUpdatingOutbox = false;
            }
        }

        orderContext._EntityEventMap = null;
    }
}
