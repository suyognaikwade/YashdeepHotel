using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Yashdeep.Application.Common.Interfaces;
using Yashdeep.Domain.Entities;
using Yashdeep.Domain.Enums;
using Yashdeep.Persistence.Cloud;
using Xunit;

namespace Yashdeep.Persistence.Cloud.Tests;

public class TestTenantContext : ITenantContext
{
    public Guid? TenantId { get; set; }
    public TenantStatus? Status { get; set; }
}

public class CloudPersistenceIntegrationTests
{
    private DbContextOptions<CloudDbContext> CreateInMemoryOptions(string dbName)
    {
        return new DbContextOptionsBuilder<CloudDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
    }

    [Fact]
    public async Task SaveChangesAsync_AutoStampsAuditMetadata_ForNewAndModifiedEntities()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(SaveChangesAsync_AutoStampsAuditMetadata_ForNewAndModifiedEntities));
        var tenantId = Guid.NewGuid();
        var tenantContext = new TestTenantContext { TenantId = tenantId, Status = TenantStatus.Active };

        using (var context = new CloudDbContext(options, tenantContext))
        {
            var tenant = new Tenant
            {
                TenantId = tenantId,
                LegalName = "Yashdeep Hospitality Group Ltd",
                TradeName = "Yashdeep Hotel",
                ContactEmail = "admin@yashdeep.com",
                ContactPhone = "+919876543210",
                Status = TenantStatus.Active
            };

            context.Tenants.Add(tenant);
            await context.SaveChangesAsync();

            Assert.NotEqual(default(DateTime), tenant.CreatedAtUtc);
            Assert.Null(tenant.UpdatedAtUtc);
        }

        using (var context = new CloudDbContext(options, tenantContext))
        {
            var tenant = await context.Tenants.FirstAsync(t => t.TenantId == tenantId);
            tenant.TradeName = "Yashdeep Hotel & Resort";
            await context.SaveChangesAsync();

            Assert.NotNull(tenant.UpdatedAtUtc);
        }
    }

    [Fact]
    public async Task SaveChangesAsync_EnforcesSoftDeletion_WhenEntityIsDeleted()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(SaveChangesAsync_EnforcesSoftDeletion_WhenEntityIsDeleted));
        var tenantId = Guid.NewGuid();
        var tenantContext = new TestTenantContext { TenantId = tenantId, Status = TenantStatus.Active };

        using (var context = new CloudDbContext(options, tenantContext))
        {
            var tenant = new Tenant
            {
                TenantId = tenantId,
                LegalName = "Yashdeep Hospitality Group Ltd",
                TradeName = "Yashdeep Hotel",
                ContactEmail = "admin@yashdeep.com",
                ContactPhone = "+919876543210"
            };
            context.Tenants.Add(tenant);
            await context.SaveChangesAsync();
        }

        using (var context = new CloudDbContext(options, tenantContext))
        {
            var tenant = await context.Tenants.FirstAsync(t => t.TenantId == tenantId);
            context.Tenants.Remove(tenant);
            await context.SaveChangesAsync();

            Assert.True(tenant.IsDeleted);
            Assert.NotNull(tenant.DeletedAtUtc);
        }

        // Verify Global Query Filter hides soft-deleted entity
        using (var context = new CloudDbContext(options, tenantContext))
        {
            var tenantCount = await context.Tenants.CountAsync(t => t.TenantId == tenantId);
            Assert.Equal(0, tenantCount);

            var rawTenantCount = await context.Tenants.IgnoreQueryFilters().CountAsync(t => t.TenantId == tenantId);
            Assert.Equal(1, rawTenantCount);
        }
    }

    [Fact]
    public async Task SaveChangesAsync_AutoInjectsTenantId_FromTenantContext_WhenEmpty()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(SaveChangesAsync_AutoInjectsTenantId_FromTenantContext_WhenEmpty));
        var tenantId = Guid.NewGuid();
        var tenantContext = new TestTenantContext { TenantId = tenantId, Status = TenantStatus.Active };

        using (var context = new CloudDbContext(options, tenantContext))
        {
            var org = new Organization
            {
                OrganizationId = Guid.NewGuid(),
                // TenantId left as Guid.Empty
                Code = "ORG01",
                Name = "Yashdeep Org 1"
            };

            context.Organizations.Add(org);
            await context.SaveChangesAsync();

            Assert.Equal(tenantId, org.TenantId);
        }
    }

    [Fact]
    public async Task SaveChangesAsync_ThrowsInvalidOperationException_OnCrossTenantPersistenceAttempt()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(SaveChangesAsync_ThrowsInvalidOperationException_OnCrossTenantPersistenceAttempt));
        var activeTenantId = Guid.NewGuid();
        var maliciousTenantId = Guid.NewGuid();
        var tenantContext = new TestTenantContext { TenantId = activeTenantId, Status = TenantStatus.Active };

        using (var context = new CloudDbContext(options, tenantContext))
        {
            var org = new Organization
            {
                OrganizationId = Guid.NewGuid(),
                TenantId = maliciousTenantId, // Mismatched tenantId
                Code = "ORG02",
                Name = "Malicious Org"
            };

            context.Organizations.Add(org);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
            Assert.Contains("Cross-tenant persistence violation", ex.Message);
        }
    }

    [Fact]
    public async Task GlobalQueryFilter_FiltersEntitiesByTenantContext()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(GlobalQueryFilter_FiltersEntitiesByTenantContext));
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        // Seed Tenant 1 data
        using (var context = new CloudDbContext(options, new TestTenantContext { TenantId = tenant1, Status = TenantStatus.Active }))
        {
            context.Organizations.Add(new Organization
            {
                OrganizationId = Guid.NewGuid(),
                TenantId = tenant1,
                Code = "ORG1",
                Name = "Tenant 1 Org"
            });
            await context.SaveChangesAsync();
        }

        // Seed Tenant 2 data
        using (var context = new CloudDbContext(options, new TestTenantContext { TenantId = tenant2, Status = TenantStatus.Active }))
        {
            context.Organizations.Add(new Organization
            {
                OrganizationId = Guid.NewGuid(),
                TenantId = tenant2,
                Code = "ORG2",
                Name = "Tenant 2 Org"
            });
            await context.SaveChangesAsync();
        }

        // Query under Tenant 1 Context
        using (var context = new CloudDbContext(options, new TestTenantContext { TenantId = tenant1, Status = TenantStatus.Active }))
        {
            var orgs = await context.Organizations.ToListAsync();
            Assert.Single(orgs);
            Assert.Equal("ORG1", orgs[0].Code);
            Assert.Equal(tenant1, orgs[0].TenantId);
        }

        // Query under Tenant 2 Context
        using (var context = new CloudDbContext(options, new TestTenantContext { TenantId = tenant2, Status = TenantStatus.Active }))
        {
            var orgs = await context.Organizations.ToListAsync();
            Assert.Single(orgs);
            Assert.Equal("ORG2", orgs[0].Code);
            Assert.Equal(tenant2, orgs[0].TenantId);
        }
    }

    [Fact]
    public async Task PersistenceModel_SupportsHierarchyAndParentageIntegrity()
    {
        // Arrange
        var options = CreateInMemoryOptions(nameof(PersistenceModel_SupportsHierarchyAndParentageIntegrity));
        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var outletId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        var tenantContext = new TestTenantContext { TenantId = tenantId, Status = TenantStatus.Active };

        using (var context = new CloudDbContext(options, tenantContext))
        {
            var tenant = new Tenant
            {
                TenantId = tenantId,
                LegalName = "Yashdeep Enterprises",
                TradeName = "Yashdeep Hotel",
                ContactEmail = "owner@yashdeep.com",
                ContactPhone = "+919876543210"
            };

            var org = new Organization
            {
                OrganizationId = orgId,
                TenantId = tenantId,
                Code = "HQ",
                Name = "Headquarters Org"
            };

            var branch = new Branch
            {
                BranchId = branchId,
                TenantId = tenantId,
                OrganizationId = orgId,
                Code = "BH01",
                Name = "Bhenda Branch",
                AddressLine1 = "Main Highway Rd",
                City = "Nanded",
                State = "Maharashtra",
                PostalCode = "431601"
            };

            var outlet = new Outlet
            {
                OutletId = outletId,
                TenantId = tenantId,
                OrganizationId = orgId,
                BranchId = branchId,
                Code = "BAR01",
                Name = "AC Permit Room Bar",
                Type = OutletType.AcBar
            };

            var terminal = new Terminal
            {
                TerminalId = terminalId,
                TenantId = tenantId,
                OrganizationId = orgId,
                BranchId = branchId,
                OutletId = outletId,
                Code = "TERM01",
                Name = "Main Bar Terminal",
                IPAddress = "192.168.1.50"
            };

            var device = new Device
            {
                DeviceId = deviceId,
                TenantId = tenantId,
                OrganizationId = orgId,
                BranchId = branchId,
                OutletId = outletId,
                TerminalId = terminalId,
                DeviceIdentifier = "DEV-POS-01",
                HardwareFingerprint = "CPU-INTEL-1234-MB-5678",
                Name = "Bar POS Screen",
                Type = DeviceType.MainPosTerminal,
                PublicEd25519Key = "4f8a...ed25519key"
            };

            context.Tenants.Add(tenant);
            context.Organizations.Add(org);
            context.Branches.Add(branch);
            context.Outlets.Add(outlet);
            context.Terminals.Add(terminal);
            context.Devices.Add(device);

            await context.SaveChangesAsync();
        }

        using (var context = new CloudDbContext(options, tenantContext))
        {
            var persistedDevice = await context.Devices
                .Include(d => d.Terminal)
                .ThenInclude(t => t!.Outlet)
                .ThenInclude(o => o!.Branch)
                .ThenInclude(b => b!.Organization)
                .ThenInclude(o => o!.Tenant)
                .FirstAsync(d => d.DeviceId == deviceId);

            Assert.NotNull(persistedDevice);
            Assert.Equal("DEV-POS-01", persistedDevice.DeviceIdentifier);
            Assert.Equal("TERM01", persistedDevice.Terminal?.Code);
            Assert.Equal("BAR01", persistedDevice.Terminal?.Outlet?.Code);
            Assert.Equal("BH01", persistedDevice.Terminal?.Outlet?.Branch?.Code);
            Assert.Equal("HQ", persistedDevice.Terminal?.Outlet?.Branch?.Organization?.Code);
            Assert.Equal("Yashdeep Enterprises", persistedDevice.Terminal?.Outlet?.Branch?.Organization?.Tenant?.LegalName);
        }
    }

    [Fact]
    public void Document_EnvironmentStrategyForPostgreSQL_VsProviderSimulatedTesting()
    {
        // Note: As specified in Task 4 contract, if a live PostgreSQL instance is unavailable,
        // provider-level / simulated testing (such as EF Core InMemory provider) is used for unit and relational schema configuration tests.
        // True PostgreSQL Row-Level Security (RLS) policies (`SET LOCAL app.current_tenant_id`) and PostgreSQL database kernel level constraints
        // require a live PostgreSQL 16 database server and must not be falsely claimed as verified without a PostgreSQL test environment.
        Assert.True(true);
    }
}
