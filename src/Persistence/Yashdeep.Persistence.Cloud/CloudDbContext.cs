using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Yashdeep.Domain.Contexts;
using Yashdeep.Domain.Contracts;
using Yashdeep.Domain.Entities;
using Yashdeep.Domain.Exceptions;

namespace Yashdeep.Persistence.Cloud;

public class CloudDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;
    private readonly IBranchContext _branchContext;

    public CloudDbContext(DbContextOptions<CloudDbContext> options, ITenantContext tenantContext, IBranchContext branchContext)
        : base(options)
    {
        _tenantContext = tenantContext;
        _branchContext = branchContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Outlet> Outlets => Set<Outlet>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<User> Users => Set<User>();
    public DbSet<TestOrderEntity> TestOrders => Set<TestOrderEntity>();

    public Guid CurrentTenantId => _tenantContext.IsTenantResolved ? _tenantContext.TenantId : Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var propertyMethodInfo = typeof(EF).GetMethod("Property")!.MakeGenericMethod(typeof(Guid));
                var tenantIdProperty = Expression.Call(propertyMethodInfo, parameter, Expression.Constant("TenantId"));
                var compareExpression = Expression.Equal(
                    tenantIdProperty,
                    Expression.Property(Expression.Constant(this), nameof(CurrentTenantId))
                );

                var lambda = Expression.Lambda(compareExpression, parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }

        modelBuilder.Entity<Tenant>(b =>
        {
            b.HasKey(t => t.Id);
            b.Property(t => t.Name).IsRequired();
        });

        modelBuilder.Entity<Organization>(b =>
        {
            b.HasKey(o => o.Id);
        });

        modelBuilder.Entity<Branch>(b =>
        {
            b.HasKey(br => br.Id);
        });

        modelBuilder.Entity<Outlet>(b =>
        {
            b.HasKey(o => o.Id);
        });

        modelBuilder.Entity<Terminal>(b =>
        {
            b.HasKey(t => t.Id);
        });

        modelBuilder.Entity<Device>(b =>
        {
            b.HasKey(d => d.Id);
        });

        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(u => u.Id);
            b.OwnsMany(u => u.BranchAssignments, ba =>
            {
                ba.WithOwner().HasForeignKey("UserId");
                ba.HasKey("UserId", "BranchId");
            });
            b.OwnsMany(u => u.RoleAssignments, ra =>
            {
                ra.WithOwner().HasForeignKey("UserId");
                ra.HasKey("UserId", "RoleName");
            });
        });

        modelBuilder.Entity<TestOrderEntity>(b =>
        {
            b.HasKey(o => o.Id);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceTenantBoundaries();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        EnforceTenantBoundaries();
        return base.SaveChanges();
    }

    private void EnforceTenantBoundaries()
    {
        if (!_tenantContext.IsTenantResolved)
        {
            var trackedTenantEntities = ChangeTracker.Entries<ITenantEntity>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
            if (trackedTenantEntities.Any())
            {
                throw new TenantAuthorizationException("Background service or unauthenticated context attempted persistence mutation without tenant context.");
            }
            return;
        }

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.TenantId == Guid.Empty)
                {
                    entry.Entity.TenantId = _tenantContext.TenantId;
                }
                else if (entry.Entity.TenantId != _tenantContext.TenantId)
                {
                    throw new TenantAuthorizationException($"Cross-tenant entity insertion blocked. Entity TenantId: {entry.Entity.TenantId}, Context TenantId: {_tenantContext.TenantId}");
                }
            }
            else if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
            {
                if (entry.Entity.TenantId != _tenantContext.TenantId)
                {
                    throw new TenantAuthorizationException($"Cross-tenant entity mutation blocked. Entity TenantId: {entry.Entity.TenantId}, Context TenantId: {_tenantContext.TenantId}");
                }
            }
        }
    }
}

public class TestOrderEntity : IBranchEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
