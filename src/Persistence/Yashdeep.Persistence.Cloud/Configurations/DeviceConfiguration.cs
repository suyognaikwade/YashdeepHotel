using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yashdeep.Domain.Entities;

namespace Yashdeep.Persistence.Cloud.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");

        builder.HasKey(d => d.DeviceId);

        builder.Property(d => d.DeviceIdentifier)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.HardwareFingerprint)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.Type)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(d => d.PublicEd25519Key)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(d => d.CreatedAtUtc)
            .IsRequired();

        builder.Property(d => d.ConcurrencyToken)
            .IsRowVersion();

        builder.HasQueryFilter(d => !d.IsDeleted);

        // Enforce composite FK to Terminal (TenantId + OrganizationId + BranchId + TerminalId)
        builder.HasOne(d => d.Terminal)
            .WithMany(t => t.Devices)
            .HasPrincipalKey(t => new { t.TenantId, t.OrganizationId, t.BranchId, t.TerminalId })
            .HasForeignKey(d => new { d.TenantId, d.OrganizationId, d.BranchId, d.TerminalId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Tenant)
            .WithMany()
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.TenantId, d.DeviceIdentifier })
            .IsUnique();

        builder.HasIndex(d => new { d.TenantId, d.HardwareFingerprint })
            .IsUnique();
    }
}
