namespace Contracts;

public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
    string RoutingKey { get; }
}

public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
    public abstract string RoutingKey { get; }
}

public record OrderCreated(Guid OrderId, string CustomerEmail, decimal TotalAmount, DateTime OrderDate) 
    : IntegrationEvent
{
    public override string RoutingKey => "order.created";
}
