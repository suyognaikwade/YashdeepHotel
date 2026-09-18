using Microsoft.EntityFrameworkCore;
using Yashdeep.Application.Common.Interfaces;
using Yashdeep.Domain.Entities;

namespace Yashdeep.Persistence.Local;

/// <summary>
/// EF Core DbContext for the local edge SQLite database encrypted via SQLCipher.
/// Represents an edge operational working set separate from cloud PostgreSQL master persistence.
/// </summary>
public class LocalDbContext : DbContext
{
    private readonly Guid? _currentTenantId;
    private readonly ITenantContext? _tenantContext;

    public Guid CurrentTenantId => _tenantContext?.TenantId ?? _currentTenantId ?? Guid.Empty;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Outlet> Outlets => Set<Outlet>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<LocalSettings> LocalSettings => Set<LocalSettings>();

    public LocalDbContext(DbContextOptions<LocalDbContext> options, Guid? currentTenantId = null, ITenantContext? tenantContext = null)
        : base(options)
    {
        _currentTenantId = currentTenantId;
        _tenantContext = tenantContext;
    }

    public LocalDbContext(DbContextOptions<LocalDbContext> options, ITenantContext tenantContext)
        : this(options, null, tenantContext)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tenant Configuration
        modelBuilder.Entity<Tenant>(builder =>
        {
            builder.ToTable("Tenants");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
            builder.Property(t => t.Code).IsRequired().HasMaxLength(50);
            builder.HasIndex(t => t.Code).IsUnique();
        });

        // Branch Configuration
        modelBuilder.Entity<Branch>(builder =>
        {
            builder.ToTable("Branches");
            builder.HasKey(b => b.Id);
            builder.Property(b => b.Name).IsRequired().HasMaxLength(200);
            builder.Property(b => b.Code).IsRequired().HasMaxLength(50);
            builder.HasOne(b => b.Tenant)
                   .WithMany(t => t.Branches)
                   .HasForeignKey(b => b.TenantId)
                   .OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(b => new { b.TenantId, b.Code }).IsUnique();
        });

        // Outlet Configuration
        modelBuilder.Entity<Outlet>(builder =>
        {
            builder.ToTable("Outlets");
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Name).IsRequired().HasMaxLength(200);
            builder.Property(o => o.Code).IsRequired().HasMaxLength(50);
            builder.Property(o => o.OutletType).IsRequired().HasMaxLength(50);
            builder.HasOne(o => o.Branch)
                   .WithMany(b => b.Outlets)
                   .HasForeignKey(o => o.BranchId)
                   .OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(o => new { o.TenantId, o.BranchId, o.Code }).IsUnique();
        });

        // Terminal Configuration
        modelBuilder.Entity<Terminal>(builder =>
        {
            builder.ToTable("Terminals");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.DeviceName).IsRequired().HasMaxLength(100);
            builder.Property(t => t.MacAddress).HasMaxLength(50);
            builder.Property(t => t.IPAddress).HasMaxLength(50);
            builder.HasIndex(t => new { t.TenantId, t.BranchId, t.DeviceName });
        });

        // Device Configuration
        modelBuilder.Entity<Device>(builder =>
        {
            builder.ToTable("Devices");
            builder.HasKey(d => d.Id);
            builder.Property(d => d.HardwareId).IsRequired().HasMaxLength(256);
            builder.HasIndex(d => d.HardwareId).IsUnique();
        });

        // User Configuration
        modelBuilder.Entity<User>(builder =>
        {
            builder.ToTable("Users");
            builder.HasKey(u => u.Id);
            builder.Property(u => u.Username).IsRequired().HasMaxLength(100);
            builder.Property(u => u.Email).HasMaxLength(256);
            builder.Property(u => u.PasswordHash).IsRequired();
            builder.HasIndex(u => new { u.TenantId, u.Username }).IsUnique();
        });

        // Role Configuration
        modelBuilder.Entity<Role>(builder =>
        {
            builder.ToTable("Roles");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
            builder.Property(r => r.NormalizedName).IsRequired().HasMaxLength(100);
            builder.HasIndex(r => new { r.TenantId, r.NormalizedName }).IsUnique();
        });

        // UserRole Configuration
        modelBuilder.Entity<UserRole>(builder =>
        {
            builder.ToTable("UserRoles");
            builder.HasKey(ur => new { ur.UserId, ur.RoleId });
            builder.HasOne(ur => ur.User)
                   .WithMany(u => u.UserRoles)
                   .HasForeignKey(ur => ur.UserId)
                   .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(ur => ur.Role)
                   .WithMany(r => r.UserRoles)
                   .HasForeignKey(ur => ur.RoleId)
                   .OnDelete(DeleteBehavior.Cascade);
        });

        // LocalSettings Configuration
        modelBuilder.Entity<LocalSettings>(builder =>
        {
            builder.ToTable("LocalSettings");
            builder.HasKey(ls => ls.Id);
        });

        // Global Query Filters for multi-tenant isolation on edge device when tenant context is active.
        // Registered unconditionally on model creation using dynamic property evaluation to prevent cached-model tenant data leakage.
        modelBuilder.Entity<Branch>().HasQueryFilter(b => CurrentTenantId == Guid.Empty || b.TenantId == CurrentTenantId);
        modelBuilder.Entity<Outlet>().HasQueryFilter(o => CurrentTenantId == Guid.Empty || o.TenantId == CurrentTenantId);
        modelBuilder.Entity<Terminal>().HasQueryFilter(t => CurrentTenantId == Guid.Empty || t.TenantId == CurrentTenantId);
        modelBuilder.Entity<Device>().HasQueryFilter(d => CurrentTenantId == Guid.Empty || d.TenantId == CurrentTenantId);
        modelBuilder.Entity<User>().HasQueryFilter(u => CurrentTenantId == Guid.Empty || u.TenantId == CurrentTenantId);
        modelBuilder.Entity<Role>().HasQueryFilter(r => CurrentTenantId == Guid.Empty || r.TenantId == CurrentTenantId);
        modelBuilder.Entity<UserRole>().HasQueryFilter(ur => CurrentTenantId == Guid.Empty || ur.TenantId == CurrentTenantId);
        modelBuilder.Entity<LocalSettings>().HasQueryFilter(ls => CurrentTenantId == Guid.Empty || ls.TenantId == CurrentTenantId);
    }
}
