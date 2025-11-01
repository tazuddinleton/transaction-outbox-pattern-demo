namespace TransactionOutboxDemo.Domain.Events;

public class OrderCreatedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public string EventType => nameof(OrderCreatedEvent);
    
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = null!;
    public string CustomerEmail { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
}
