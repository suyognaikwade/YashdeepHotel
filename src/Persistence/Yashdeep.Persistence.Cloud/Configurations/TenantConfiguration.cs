using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yashdeep.Domain.Entities;

namespace Yashdeep.Persistence.Cloud.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.TenantId);

        builder.Property(t => t.LegalName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.TradeName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.GSTIN)
            .HasMaxLength(50);

        builder.Property(t => t.ExciseLicenseNumber)
            .HasMaxLength(100);

        builder.Property(t => t.ContactEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(t => t.ContactPhone)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired();

        builder.Property(t => t.ConcurrencyToken)
            .IsRowVersion();

        builder.HasQueryFilter(t => !t.IsDeleted);

        builder.HasIndex(t => t.TradeName);
        builder.HasIndex(t => t.GSTIN);
        builder.HasIndex(t => t.ContactEmail);
    }
}
