namespace Contracts;

using TransactionOutboxDemo.Domain;

public abstract record IntegrationEvent : DomainEvent
{
    // Inherits EventId, OccurredOn, RoutingKey from DomainEvent
}

public record OrderCreated(Guid OrderId, string CustomerEmail, decimal TotalAmount, DateTime OrderDate) 
    : IntegrationEvent
{
    public override string RoutingKey => "order.created";
}
