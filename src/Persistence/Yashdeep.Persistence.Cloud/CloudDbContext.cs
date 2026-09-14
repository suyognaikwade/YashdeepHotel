using Microsoft.EntityFrameworkCore;
using Yashdeep.Application.Interfaces;
using Yashdeep.Domain.Entities;

namespace Yashdeep.Persistence.Cloud;

public class CloudDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public CloudDbContext(DbContextOptions<CloudDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(builder =>
        {
            builder.HasKey(t => t.TenantId);
            builder.Property(t => t.LegalName).IsRequired().HasMaxLength(200);
            builder.Property(t => t.TradeName).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Organization>(builder =>
        {
            builder.HasKey(o => o.OrganizationId);
            builder.Property(o => o.LegalName).IsRequired().HasMaxLength(200);
            builder.HasQueryFilter(o => o.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<User>(builder =>
        {
            builder.HasKey(u => u.UserId);
            builder.Property(u => u.Username).IsRequired().HasMaxLength(100);
            builder.HasIndex(u => new { u.TenantId, u.Username }).IsUnique();
            builder.HasMany(u => u.Roles);
            builder.HasMany(u => u.RefreshTokens);
            builder.HasQueryFilter(u => u.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<Role>(builder =>
        {
            builder.HasKey(r => r.RoleId);
            builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
            builder.HasMany(r => r.Permissions);
            builder.HasQueryFilter(r => r.TenantId == _tenantContext.TenantId);
        });

        modelBuilder.Entity<Permission>(builder =>
        {
            builder.HasKey(p => p.PermissionId);
            builder.Property(p => p.Code).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<RefreshToken>(builder =>
        {
            builder.HasKey(rt => rt.RefreshTokenId);
            builder.Property(rt => rt.TokenHash).IsRequired();
            builder.HasQueryFilter(rt => rt.TenantId == _tenantContext.TenantId);
        });
    }
}
