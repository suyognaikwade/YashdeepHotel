using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Yashdeep.Application.Common.Interfaces;
using Yashdeep.Domain.Common;
using Yashdeep.Domain.Entities;
using Yashdeep.Persistence.Cloud.Configurations;

namespace Yashdeep.Persistence.Cloud;

public class CloudDbContext : DbContext, ICloudDbContext
{
    private readonly ITenantContext? _tenantContext;

    public CloudDbContext(DbContextOptions<CloudDbContext> options)
        : base(options)
    {
    }

    public CloudDbContext(DbContextOptions<CloudDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Outlet> Outlets => Set<Outlet>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<Device> Devices => Set<Device>();

    public Guid? CurrentTenantId => _tenantContext?.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationConfiguration());
        modelBuilder.ApplyConfiguration(new BranchConfiguration());
        modelBuilder.ApplyConfiguration(new OutletConfiguration());
        modelBuilder.ApplyConfiguration(new TerminalConfiguration());
        modelBuilder.ApplyConfiguration(new DeviceConfiguration());

        // Configure TenantId Global Query Filters for ITenantScopedEntity when ITenantContext is present
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScopedEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var tenantIdProperty = Expression.Property(parameter, nameof(ITenantScopedEntity.TenantId));

                // Clause 1: Soft delete filter (if applicable)
                Expression? filter = null;
                if (typeof(ISoftDeletableEntity).IsAssignableFrom(entityType.ClrType))
                {
                    var isDeletedProperty = Expression.Property(parameter, nameof(ISoftDeletableEntity.IsDeleted));
                    filter = Expression.Equal(isDeletedProperty, Expression.Constant(false));
                }

                // Clause 2: Tenant isolation filter
                var currentTenantIdExpr = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
                var hasTenantExpr = Expression.Property(currentTenantIdExpr, nameof(Nullable<Guid>.HasValue));
                var tenantValueExpr = Expression.Property(currentTenantIdExpr, nameof(Nullable<Guid>.Value));

                var tenantMatchesExpr = Expression.Equal(tenantIdProperty, tenantValueExpr);
                var tenantFilterExpr = Expression.OrElse(
                    Expression.Not(hasTenantExpr),
                    tenantMatchesExpr
                );

                filter = filter == null ? tenantFilterExpr : Expression.AndAlso(filter, tenantFilterExpr);

                var lambda = Expression.Lambda(filter, parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is ITenantScopedEntity tenantEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    if (tenantEntity.TenantId == Guid.Empty)
                    {
                        if (_tenantContext?.TenantId.HasValue == true)
                        {
                            tenantEntity.TenantId = _tenantContext.TenantId.Value;
                        }
                    }
                    else if (_tenantContext?.TenantId.HasValue == true && tenantEntity.TenantId != _tenantContext.TenantId.Value)
                    {
                        throw new InvalidOperationException($"Cross-tenant persistence violation: Entity TenantId ({tenantEntity.TenantId}) does not match current tenant context ({_tenantContext.TenantId.Value}).");
                    }
                }
            }

            if (entry.Entity is IAuditableEntity auditableEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    if (auditableEntity.CreatedAtUtc == default)
                    {
                        auditableEntity.CreatedAtUtc = nowUtc;
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditableEntity.UpdatedAtUtc = nowUtc;
                }
            }

            if (entry.Entity is ISoftDeletableEntity softDeletableEntity)
            {
                if (entry.State == EntityState.Deleted)
                {
                    entry.State = EntityState.Modified;
                    softDeletableEntity.IsDeleted = true;
                    softDeletableEntity.DeletedAtUtc = nowUtc;
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
