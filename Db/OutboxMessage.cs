namespace TransactionOutboxDemo.Db;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public bool Processed { get; set; }
}
