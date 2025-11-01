namespace TransactionOutboxDemo.Domain;

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
    string EventType { get; }
}
