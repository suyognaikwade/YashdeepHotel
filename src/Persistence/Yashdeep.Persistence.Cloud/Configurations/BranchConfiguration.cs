using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yashdeep.Domain.Entities;

namespace Yashdeep.Persistence.Cloud.Configurations;

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches");

        builder.HasKey(b => b.BranchId);

        // Define alternate keys for composite parentage FK constraints
        builder.HasAlternateKey(b => new { b.TenantId, b.OrganizationId, b.BranchId });

        builder.Property(b => b.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.AddressLine1)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(b => b.AddressLine2)
            .HasMaxLength(256);

        builder.Property(b => b.City)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.State)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.PostalCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(b => b.TimeZone)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(b => b.CreatedAtUtc)
            .IsRequired();

        builder.Property(b => b.ConcurrencyToken)
            .IsRowVersion();

        builder.HasQueryFilter(b => !b.IsDeleted);

        // Enforce composite FK to Organization (TenantId + OrganizationId)
        builder.HasOne(b => b.Organization)
            .WithMany(o => o.Branches)
            .HasPrincipalKey(o => new { o.TenantId, o.OrganizationId })
            .HasForeignKey(b => new { b.TenantId, b.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Tenant)
            .WithMany()
            .HasForeignKey(b => b.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.TenantId, b.OrganizationId, b.Code })
            .IsUnique();
    }
}
