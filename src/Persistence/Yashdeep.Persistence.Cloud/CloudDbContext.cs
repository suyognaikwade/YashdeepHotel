using Microsoft.EntityFrameworkCore;
using Yashdeep.Application.Common.Interfaces;
using Yashdeep.Domain.Sync;

namespace Yashdeep.Persistence.Cloud
{
    public class CloudDbContext : DbContext
    {
        private readonly ITenantContext? _tenantContext;

        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

        public CloudDbContext(DbContextOptions<CloudDbContext> options, ITenantContext? tenantContext = null)
            : base(options)
        {
            _tenantContext = tenantContext;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<InboxMessage>(entity =>
            {
                entity.ToTable("InboxMessages");

                entity.HasKey(e => new { e.TenantId, e.EventId });

                entity.Property(e => e.EventId).IsRequired();
                entity.Property(e => e.TenantId).IsRequired();
                entity.Property(e => e.BranchId).IsRequired();
                entity.Property(e => e.DeviceId).IsRequired();
                entity.Property(e => e.EventType).HasMaxLength(128).IsRequired();
                entity.Property(e => e.AggregateType).HasMaxLength(128).IsRequired();
                entity.Property(e => e.AggregateId).IsRequired();
                entity.Property(e => e.SequenceNumber).IsRequired();
                entity.Property(e => e.PayloadHash).HasMaxLength(64).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.ReceivedAtUtc).IsRequired();

                entity.HasIndex(e => new { e.TenantId, e.DeviceId, e.SequenceNumber });

                if (_tenantContext != null && _tenantContext.TenantId != Guid.Empty)
                {
                    entity.HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
                }
            });
        }
    }
}
