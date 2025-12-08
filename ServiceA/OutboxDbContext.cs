using Microsoft.EntityFrameworkCore;

namespace ServiceA.Outbox;

public class OutboxDbContext : DbContext
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public OutboxDbContext(DbContextOptions<OutboxDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.EventType).IsRequired().HasMaxLength(256);
            b.Property(x => x.RoutingKey).IsRequired().HasMaxLength(256);
            b.Property(x => x.Payload).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
            b.HasIndex(x => new { x.Processed, x.CreatedAt });
        });
    }
}

