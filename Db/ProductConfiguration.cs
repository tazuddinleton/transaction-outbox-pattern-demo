using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransactionOutboxDemo.Domain;

namespace TransactionOutboxDemo.Db;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Price).HasPrecision(18, 2);
        
        // Seed data
        builder.HasData(
            new Product { Id = 1, Name = "Laptop", Price = 1299.99m },
            new Product { Id = 2, Name = "Mouse", Price = 29.99m },
            new Product { Id = 3, Name = "Keyboard", Price = 79.99m },
            new Product { Id = 4, Name = "Monitor", Price = 399.99m },
            new Product { Id = 5, Name = "USB Cable", Price = 9.99m }
        );
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.CustomerName).IsRequired().HasMaxLength(200);
        builder.Property(o => o.CustomerEmail).IsRequired().HasMaxLength(200);
        builder.Property(o => o.TotalAmount).HasPrecision(18, 2);
        builder.Property(o => o.OrderDate).IsRequired();
        
        builder.HasMany(o => o.OrderItems)
            .WithOne(oi => oi.Order)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}


public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.HasKey(oi => oi.Id);
        builder.Property(oi => oi.Quantity).IsRequired();
        builder.Property(oi => oi.Price).HasPrecision(18, 2);
        
        builder.HasOne(oi => oi.Product)
            .WithMany()
            .HasForeignKey(oi => oi.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}