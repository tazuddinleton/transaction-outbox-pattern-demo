namespace TransactionOutboxDemo.Domain.Events;

public record OrderCreatedEvent : DomainEvent
{
    public override string RoutingKey => "order.created";
    
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = null!;
    public string CustomerEmail { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
}
