using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TransactionOutboxDemo.Db;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        
        builder.HasKey(o => o.Id);
        
        builder.Property(o => o.EventType)
            .IsRequired()
            .HasMaxLength(255);
        
        builder.Property(o => o.Payload)
            .IsRequired()
            .HasColumnType("jsonb");
        
        builder.Property(o => o.CreatedAt)
            .IsRequired();
        
        builder.Property(o => o.Processed)
            .IsRequired()
            .HasDefaultValue(false);
        
        builder.HasIndex(o => new { o.Processed, o.CreatedAt });
    }
}
