using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yashdeep.Domain.Entities;

namespace Yashdeep.Persistence.Cloud.Configurations;

public class OutletConfiguration : IEntityTypeConfiguration<Outlet>
{
    public void Configure(EntityTypeBuilder<Outlet> builder)
    {
        builder.ToTable("outlets");

        builder.HasKey(o => o.OutletId);

        // Define alternate keys for composite parentage FK constraints
        builder.HasAlternateKey(o => new { o.TenantId, o.OrganizationId, o.BranchId, o.OutletId });

        builder.Property(o => o.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(o => o.Type)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(o => o.CreatedAtUtc)
            .IsRequired();

        builder.Property(o => o.ConcurrencyToken)
            .IsRowVersion();

        builder.HasQueryFilter(o => !o.IsDeleted);

        // Enforce composite FK to Branch (TenantId + OrganizationId + BranchId)
        builder.HasOne(o => o.Branch)
            .WithMany(b => b.Outlets)
            .HasPrincipalKey(b => new { b.TenantId, b.OrganizationId, b.BranchId })
            .HasForeignKey(o => new { o.TenantId, o.OrganizationId, o.BranchId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Tenant)
            .WithMany()
            .HasForeignKey(o => o.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => new { o.TenantId, o.BranchId, o.Code })
            .IsUnique();
    }
}
