using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yashdeep.Domain.Entities;

namespace Yashdeep.Persistence.Cloud.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");

        builder.HasKey(o => o.OrganizationId);

        // Define alternate key for composite relationships (TenantId + OrganizationId)
        builder.HasAlternateKey(o => new { o.TenantId, o.OrganizationId });

        builder.Property(o => o.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(o => o.LegalEntityName)
            .HasMaxLength(200);

        builder.Property(o => o.TaxRegistrationNumber)
            .HasMaxLength(50);

        builder.Property(o => o.StateExciseLicenseNo)
            .HasMaxLength(100);

        builder.Property(o => o.CreatedAtUtc)
            .IsRequired();

        builder.Property(o => o.ConcurrencyToken)
            .IsRowVersion();

        builder.HasQueryFilter(o => !o.IsDeleted);

        builder.HasOne(o => o.Tenant)
            .WithMany(t => t.Organizations)
            .HasForeignKey(o => o.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => new { o.TenantId, o.Code })
            .IsUnique();
    }
}
