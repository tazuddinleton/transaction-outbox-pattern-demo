namespace TransactionOutboxDemo.Controllers;

public class CreateOrderRequest
{
    public string CustomerName { get; set; } = null!;
    public string CustomerEmail { get; set; } = null!;
    public List<OrderItemRequest> Items { get; set; } = new();
}

public class OrderItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}
