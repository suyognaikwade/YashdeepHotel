using Microsoft.EntityFrameworkCore;
using Yashdeep.Domain.Orders;
using Yashdeep.Domain.Outbox;

namespace Yashdeep.Persistence.Local;

public class LocalPosDbContext : DbContext
{
    public LocalPosDbContext(DbContextOptions<LocalPosDbContext> options)
        : base(options)
    {
    }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<LocalOrder> Orders => Set<LocalOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LocalOrder>(builder =>
        {
            builder.ToTable("Orders");
            builder.HasKey(e => e.OrderId);
            builder.Property(e => e.OrderId).ValueGeneratedNever();
            builder.Property(e => e.OrderNumber).HasMaxLength(64).IsRequired();
            builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        });

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");

            builder.HasKey(e => e.EventId);

            builder.Property(e => e.EventId)
                .ValueGeneratedNever()
                .IsRequired();

            builder.Property(e => e.AggregateType)
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(e => e.AggregateId)
                .IsRequired();

            builder.Property(e => e.TenantId)
                .IsRequired();

            builder.Property(e => e.BranchId)
                .IsRequired();

            builder.Property(e => e.DeviceId)
                .IsRequired();

            builder.Property(e => e.SequenceNumber)
                .IsRequired();

            builder.Property(e => e.EventType)
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(e => e.EventVersion)
                .IsRequired();

            builder.Property(e => e.CreatedUtc)
                .IsRequired();

            builder.Property(e => e.PayloadJson)
                .IsRequired();

            builder.Property(e => e.PayloadHash)
                .HasMaxLength(64)
                .IsRequired();

            builder.Property(e => e.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(e => e.RetryCount)
                .IsRequired();

            builder.Property(e => e.LastAttemptedUtc);
            builder.Property(e => e.NextRetryUtc);
            builder.Property(e => e.LastError);
            builder.Property(e => e.StackTrace);
            builder.Property(e => e.DeadLetteredUtc);

            // Per-device deterministic sequence ordering index
            builder.HasIndex(e => new { e.DeviceId, e.SequenceNumber })
                .IsUnique();

            // Status and tenant querying indexes
            builder.HasIndex(e => new { e.TenantId, e.DeviceId, e.Status });
            builder.HasIndex(e => new { e.Status, e.CreatedUtc });
        });
    }
}
