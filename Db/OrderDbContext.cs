using Microsoft.EntityFrameworkCore;
using TransactionOutboxDemo.Controllers;
using TransactionOutboxDemo.Domain;

namespace TransactionOutboxDemo.Db;


public class OrderDbContext : DbContext
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    // Internal field for interceptor to track entity-event mappings
    internal Dictionary<Domain.Entity, List<Domain.IDomainEvent>>? _EntityEventMap;

    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderDbContext).Assembly);
    }
}