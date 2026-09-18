using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yashdeep.Domain.Entities;

namespace Yashdeep.Persistence.Cloud.Configurations;

public class TerminalConfiguration : IEntityTypeConfiguration<Terminal>
{
    public void Configure(EntityTypeBuilder<Terminal> builder)
    {
        builder.ToTable("terminals");

        builder.HasKey(t => t.TerminalId);

        // Define alternate keys for composite parentage FK constraints
        builder.HasAlternateKey(t => new { t.TenantId, t.OrganizationId, t.BranchId, t.TerminalId });

        builder.Property(t => t.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.IPAddress)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired();

        builder.Property(t => t.ConcurrencyToken)
            .IsRowVersion();

        builder.HasQueryFilter(t => !t.IsDeleted);

        // Enforce composite FK to Branch (TenantId + OrganizationId + BranchId)
        builder.HasOne(t => t.Branch)
            .WithMany(b => b.Terminals)
            .HasPrincipalKey(b => new { b.TenantId, b.OrganizationId, b.BranchId })
            .HasForeignKey(t => new { t.TenantId, t.OrganizationId, t.BranchId })
            .OnDelete(DeleteBehavior.Restrict);

        // Optional FK to Outlet (composite when OutletId is present)
        builder.HasOne(t => t.Outlet)
            .WithMany(o => o.Terminals)
            .HasForeignKey(t => t.OutletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Tenant)
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.TenantId, t.BranchId, t.Code })
            .IsUnique();
    }
}
