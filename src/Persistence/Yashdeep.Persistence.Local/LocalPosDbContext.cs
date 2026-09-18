using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yashdeep.Domain.Entities.Audit;
using Yashdeep.Domain.Entities.Billing;
using Yashdeep.Domain.Entities.Inventory;
using Yashdeep.Domain.Entities.Orders;
using Yashdeep.Domain.Entities.Sync;
using Yashdeep.Domain.ValueObjects;

namespace Yashdeep.Persistence.Local;

public class LocalPosDbContext : DbContext
{
    private readonly Guid? _currentTenantId;

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<KotRecord> KotRecords => Set<KotRecord>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillTaxLine> BillTaxLines => Set<BillTaxLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Yashdeep.Domain.Outbox.OutboxMessage> SyncOutboxMessages => Set<Yashdeep.Domain.Outbox.OutboxMessage>();

    public LocalPosDbContext(DbContextOptions<LocalPosDbContext> options, Guid? currentTenantId = null)
        : base(options)
    {
        _currentTenantId = currentTenantId;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Order & OrderItem Configuration
        modelBuilder.Entity<Order>(builder =>
        {
            builder.ToTable("PosOrders");
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Id).ValueGeneratedNever();

            builder.Property(o => o.TableNumber).IsRequired().HasMaxLength(32);
            builder.Property(o => o.OrderNumber).IsRequired().HasMaxLength(64);
            builder.Property(o => o.WaiterName).HasMaxLength(128);

            builder.Property(o => o.Section).HasConversion<int>().IsRequired();
            builder.Property(o => o.OrderType).HasConversion<int>().IsRequired();
            builder.Property(o => o.Status).HasConversion<int>().IsRequired();

            builder.Property(o => o.BusinessDate)
                .HasConversion(
                    d => d.ToDateTime(TimeOnly.MinValue),
                    dt => DateOnly.FromDateTime(dt))
                .IsRequired();

            builder.HasMany(o => o.Items)
                .WithOne()
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(o => o.Kots)
                .WithOne()
                .HasForeignKey(k => k.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(o => new { o.TenantId, o.BranchId, o.BusinessDate });
        });

        modelBuilder.Entity<OrderItem>(builder =>
        {
            builder.ToTable("PosOrderItems");
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Id).ValueGeneratedNever();

            builder.Property(i => i.ItemCode).HasMaxLength(32);
            builder.Property(i => i.EnglishName).IsRequired().HasMaxLength(256);
            builder.Property(i => i.MarathiName).HasMaxLength(256);
            builder.Property(i => i.Department).HasConversion<int>().IsRequired();
            builder.Property(i => i.Quantity).HasColumnType("decimal(18,4)").IsRequired();

            // Money Value Objects
            builder.ComplexProperty(i => i.UnitPrice, cp =>
            {
                cp.Property(p => p.Amount).HasColumnName("UnitPriceAmount").HasColumnType("decimal(18,2)").IsRequired();
                cp.Property(p => p.Currency).HasColumnName("UnitPriceCurrency").HasMaxLength(8).IsRequired();
            });

            builder.ComplexProperty(i => i.SubTotal, cp =>
            {
                cp.Property(p => p.Amount).HasColumnName("SubTotalAmount").HasColumnType("decimal(18,2)").IsRequired();
                cp.Property(p => p.Currency).HasColumnName("SubTotalCurrency").HasMaxLength(8).IsRequired();
            });
        });

        // KotRecord & KotLineItem Configuration
        modelBuilder.Entity<KotRecord>(builder =>
        {
            builder.ToTable("PosKotRecords");
            builder.HasKey(k => k.Id);
            builder.Property(k => k.Id).ValueGeneratedNever();

            builder.Property(k => k.KotNumber).IsRequired().HasMaxLength(64);
            builder.Property(k => k.TicketType).HasConversion<int>().IsRequired();
            builder.Property(k => k.TableNumber).HasMaxLength(32);
            builder.Property(k => k.WaiterName).HasMaxLength(128);

            builder.HasOne<Order>()
                .WithMany(o => o.Kots)
                .HasForeignKey(k => k.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.OwnsMany(k => k.LineItems, line =>
            {
                line.ToTable("PosKotLineItems");
                line.WithOwner().HasForeignKey("KotRecordId");
                line.Property<int>("Id").ValueGeneratedOnAdd();
                line.HasKey("Id");

                line.Property(l => l.ItemCode).HasMaxLength(32);
                line.Property(l => l.EnglishName).IsRequired().HasMaxLength(256);
                line.Property(l => l.MarathiName).HasMaxLength(256);
                line.Property(l => l.Quantity).HasColumnType("decimal(18,4)").IsRequired();
            });

            builder.HasIndex(k => new { k.TenantId, k.BranchId, k.KotNumber });
        });

        // Bill, BillTaxLine, & Payment Configuration
        modelBuilder.Entity<BillTaxLine>(tax =>
        {
            tax.ToTable("PosBillTaxLines");
            tax.Property<int>("Id").ValueGeneratedOnAdd();
            tax.HasKey("Id");

            tax.Property(t => t.TaxName).IsRequired().HasMaxLength(64);
            tax.Property(t => t.RatePercentage).HasColumnType("decimal(18,2)").IsRequired();

            tax.ComplexProperty(t => t.TaxAmount, cp =>
            {
                cp.Property(p => p.Amount).HasColumnName("TaxAmount").HasColumnType("decimal(18,2)").IsRequired();
                cp.Property(p => p.Currency).HasColumnName("TaxCurrency").HasMaxLength(8).IsRequired();
            });
        });

        modelBuilder.Entity<Bill>(builder =>
        {
            builder.ToTable("PosBills");
            builder.HasKey(b => b.Id);
            builder.Property(b => b.Id).ValueGeneratedNever();

            builder.Property(b => b.InvoiceNumber).IsRequired().HasMaxLength(64);
            builder.Property(b => b.TableNumber).HasMaxLength(32);
            builder.Property(b => b.WaiterName).HasMaxLength(128);
            builder.Property(b => b.Status).HasConversion<int>().IsRequired();
            builder.Property(b => b.PaymentStatus).HasConversion<int>().IsRequired();

            builder.Property(b => b.BusinessDate)
                .HasConversion(
                    d => d.ToDateTime(TimeOnly.MinValue),
                    dt => DateOnly.FromDateTime(dt))
                .IsRequired();

            // Money Value Objects
            builder.ComplexProperty(b => b.SubTotal, cp =>
            {
                cp.Property(p => p.Amount).HasColumnName("SubTotalAmount").HasColumnType("decimal(18,2)").IsRequired();
                cp.Property(p => p.Currency).HasColumnName("SubTotalCurrency").HasMaxLength(8).IsRequired();
            });

            builder.ComplexProperty(b => b.FoodSubTotal, cp =>
            {
                cp.Property(p => p.Amount).HasColumnName("FoodSubTotalAmount").HasColumnType("decimal(18,2)").IsRequired();
                cp.Property(p => p.Currency).HasColumnName("FoodSubTotalCurrency").HasMaxLength(8).IsRequired();
            });

            builder.ComplexProperty(b => b.LiquorSubTotal, cp =>
            {
                cp.Property(p => p.Amount).HasColumnName("LiquorSubTotalAmount").HasColumnType("decimal(18,2)").IsRequired();
                cp.Property(p => p.Currency).HasColumnName("LiquorSubTotalCurrency").HasMaxLength(8).IsRequired();
            });

            builder.ComplexProperty(b => b.TotalDiscount, cp =>
            {
                cp.Property(p => p.Amount).HasColumnName("TotalDiscountAmount").HasColumnType("decimal(18,2)").IsRequired();
                cp.Property(p => p.Currency).HasColumnName("TotalDiscountCurrency").HasMaxLength(8).IsRequired();
            });

            builder.ComplexProperty(b => b.TotalTax, cp =>
            {
                cp.Property(p => p.Amount).HasColumnName("TotalTaxAmount").HasColumnType("decimal(18,2)").IsRequired();
                cp.Property(p => p.Currency).HasColumnName("TotalTaxCurrency").HasMaxLength(8).IsRequired();
            });

            builder.ComplexProperty(b => b.ServiceCharge, cp =>
            {
                cp.Property(p => p.Amount).HasColumnName("ServiceChargeAmount").HasColumnType("decimal(18,2)").IsRequired();
                cp.Property(p => p.Currency).HasColumnName("ServiceChargeCurrency").HasMaxLength(8).IsRequired();
            });

            builder.ComplexProperty(b => b.GrandTotal, cp =>
            {
                cp.Property(p => p.Amount).HasColumnName("GrandTotalAmount").HasColumnType("decimal(18,2)").IsRequired();
                cp.Property(p => p.Currency).HasColumnName("GrandTotalCurrency").HasMaxLength(8).IsRequired();
            });

            builder.ComplexProperty(b => b.TotalPaid, cp =>
            {
                cp.Property(p => p.Amount).HasColumnName("TotalPaidAmount").HasColumnType("decimal(18,2)").IsRequired();
                cp.Property(p => p.Currency).HasColumnName("TotalPaidCurrency").HasMaxLength(8).IsRequired();
            });

            builder.HasMany(b => b.TaxLines)
                .WithOne()
                .HasForeignKey("BillId")
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(b => b.Payments)
                .WithOne()
                .HasForeignKey(p => p.BillId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(b => new { b.TenantId, b.BranchId, b.BusinessDate, b.DailySequenceNumber });
            builder.HasIndex(b => new { b.TenantId, b.BranchId, b.InvoiceNumber }).IsUnique();
        });

        modelBuilder.Entity<Payment>(builder =>
        {
            builder.ToTable("PosPayments");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();

            builder.Property(p => p.Method).HasConversion<int>().IsRequired();
            builder.Property(p => p.TransactionReference).HasMaxLength(128);
            builder.Property(p => p.CashierUserId).HasMaxLength(128);

            builder.ComplexProperty(p => p.Amount, cp =>
            {
                cp.Property(p => p.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
                cp.Property(p => p.Currency).HasColumnName("Currency").HasMaxLength(8).IsRequired();
            });

            builder.HasIndex(p => new { p.TenantId, p.BranchId, p.BillId });
        });

        // StockMovement Configuration
        modelBuilder.Entity<StockMovement>(builder =>
        {
            builder.ToTable("PosStockMovements");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();

            builder.Property(s => s.ItemCode).HasMaxLength(32);
            builder.Property(s => s.ItemName).IsRequired().HasMaxLength(256);
            builder.Property(s => s.MovementType).HasConversion<int>().IsRequired();
            builder.Property(s => s.Quantity).HasColumnType("decimal(18,4)").IsRequired();

            builder.HasIndex(s => new { s.TenantId, s.BranchId, s.ReferenceTransactionId });
        });

        // AuditEvent Configuration
        modelBuilder.Entity<AuditEvent>(builder =>
        {
            builder.ToTable("PosAuditEvents");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();

            builder.Property(a => a.EventType).IsRequired().HasMaxLength(128);
            builder.Property(a => a.Action).IsRequired().HasMaxLength(128);
            builder.Property(a => a.PerformedBy).HasMaxLength(128);
            builder.Property(a => a.DetailsJson).IsRequired();

            builder.HasIndex(a => new { a.TenantId, a.BranchId, a.ReferenceId });
        });

        // POS OutboxMessage Configuration
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("PosOutboxMessages");
            builder.HasKey(e => e.EventId);
            builder.Property(e => e.EventId).ValueGeneratedNever();

            builder.Property(e => e.EventType).IsRequired().HasMaxLength(128);
            builder.Property(e => e.AggregateType).IsRequired().HasMaxLength(128);
            builder.Property(e => e.PayloadJson).IsRequired();
            builder.Property(e => e.PayloadHash).IsRequired().HasMaxLength(64);
            builder.Property(e => e.Status).HasConversion<int>().IsRequired();

            builder.HasIndex(e => new { e.TenantId, e.BranchId, e.Status });
        });

        // Sync OutboxMessage Configuration
        modelBuilder.Entity<Yashdeep.Domain.Outbox.OutboxMessage>(builder =>
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

            builder.HasIndex(e => new { e.DeviceId, e.SequenceNumber }).IsUnique();
            builder.HasIndex(e => new { e.TenantId, e.DeviceId, e.Status });
            builder.HasIndex(e => new { e.Status, e.CreatedUtc });
        });

        // Multi-tenant Query Filter
        if (_currentTenantId.HasValue && _currentTenantId != Guid.Empty)
        {
            modelBuilder.Entity<Order>().HasQueryFilter(o => o.TenantId == _currentTenantId.Value);
            modelBuilder.Entity<KotRecord>().HasQueryFilter(k => k.TenantId == _currentTenantId.Value);
            modelBuilder.Entity<Bill>().HasQueryFilter(b => b.TenantId == _currentTenantId.Value);
            modelBuilder.Entity<Payment>().HasQueryFilter(p => p.TenantId == _currentTenantId.Value);
            modelBuilder.Entity<StockMovement>().HasQueryFilter(s => s.TenantId == _currentTenantId.Value);
            modelBuilder.Entity<AuditEvent>().HasQueryFilter(a => a.TenantId == _currentTenantId.Value);
            modelBuilder.Entity<OutboxMessage>().HasQueryFilter(m => m.TenantId == _currentTenantId.Value);
            modelBuilder.Entity<Yashdeep.Domain.Outbox.OutboxMessage>().HasQueryFilter(m => m.TenantId == _currentTenantId.Value);
        }
    }
}
