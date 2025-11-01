using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TransactionOutboxDemo.Db;
using TransactionOutboxDemo.Domain;

namespace TransactionOutboxDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly OrderDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrderController> _logger;

    public OrderController(OrderDbContext context, IUnitOfWork unitOfWork, ILogger<OrderController> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<Order>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        // Validate products exist and get prices
        var productIds = request.Items.Select(i => i.ProductId).ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p);

        if (products.Count != productIds.Distinct().Count())
        {
            return BadRequest("One or more products not found");
        }

        // Create order using factory method which raises domain events
        var orderItems = request.Items.Select(item => new OrderItem
        {
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            Price = products[item.ProductId].Price
        }).ToList();

        var order = Order.Create(request.CustomerName, request.CustomerEmail, orderItems);

        _context.Orders.Add(order);
        
        // Use Unit of Work to manage transaction
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

        _logger.LogInformation("Order {OrderId} created for {CustomerName}", order.Id, order.CustomerName);

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> GetOrder(int id)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound();
        }

        return order;
    }

    [HttpGet]
    public async Task<ActionResult<List<Order>>> GetAllOrders()
    {
        var orders = await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .ToListAsync();

        return orders;
    }
}