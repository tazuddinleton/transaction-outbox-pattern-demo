namespace Contracts;

public record OrderCreated(Guid OrderId, string CustomerEmail, decimal TotalAmount, DateTime OccurredOn);
