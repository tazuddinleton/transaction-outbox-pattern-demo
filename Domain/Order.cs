using TransactionOutboxDemo.Controllers;
using TransactionOutboxDemo.Domain.Events;

namespace TransactionOutboxDemo.Domain;

public class Order : Entity
{
    public int Id { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string CustomerName { get; set; } = null!;
    public string CustomerEmail { get; set; } = null!;
    public List<OrderItem> OrderItems { get; set; } = new();
    
    public static Order Create(string customerName, string customerEmail, List<OrderItem> orderItems)
    {
        var order = new Order
        {
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            OrderDate = DateTime.UtcNow,
            OrderItems = orderItems,
            TotalAmount = orderItems.Sum(oi => oi.Price * oi.Quantity)
        };
        
        order.AddDomainEvent(new OrderCreatedEvent
        {
            OrderId = 0, // Will be set after save
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            TotalAmount = order.TotalAmount,
            OrderDate = order.OrderDate
        });
        
        return order;
    }
}