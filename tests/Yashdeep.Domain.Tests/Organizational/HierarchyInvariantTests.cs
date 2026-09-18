using Yashdeep.Domain.Common.Exceptions;
using Yashdeep.Domain.Organizational;
using Yashdeep.Domain.Organizational.Enums;
using Yashdeep.Domain.Organizational.Events;
using Yashdeep.Domain.ValueObjects;
using Xunit;

namespace Yashdeep.Domain.Tests.Organizational;

public class HierarchyInvariantTests
{
    private readonly Address _testAddress = new("123 Main St", "Suite 4", "Pune", "Maharashtra", "411001", "India");
    private readonly TaxNumber _validTaxNumber = new("27AAAAA0000A1Z5");
    private readonly LicenseNumber _validLicenseNumber = new("FLIII-2026-9999");

    [Fact]
    public void ValidHierarchy_CreationFromTenantToTerminalAndDevice_Succeeds()
    {
        // 1. Tenant
        var tenant = Tenant.Create("Yashdeep Hospitality Group Pvt Ltd", "Yashdeep Group", "admin@yashdeep.com", "+919876543210");
        Assert.NotNull(tenant.Id);
        Assert.Equal(TenantStatus.Pending, tenant.Status);
        tenant.Activate();
        Assert.Equal(TenantStatus.Active, tenant.Status);

        // 2. Organization
        var org = Organization.Create(
            tenant.Id,
            "Yashdeep Hotels & Restaurants LLP",
            "Yashdeep Dining",
            _validTaxNumber,
            _validLicenseNumber);
        Assert.Equal(tenant.Id, org.TenantId);
        Assert.Equal(OrganizationStatus.Active, org.Status);

        // 3. Branch
        var branch = Branch.Create(
            org,
            "Bhenda Prime Branch",
            "BHD01",
            _testAddress);
        Assert.Equal(tenant.Id, branch.TenantId);
        Assert.Equal(org.Id, branch.OrganizationId);
        Assert.Equal("BHD01", branch.BranchCode);

        // 4. Outlet
        var outlet = Outlet.Create(
            branch,
            "AC Family Restaurant & Bar",
            OutletType.AcBar);
        Assert.Equal(tenant.Id, outlet.TenantId);
        Assert.Equal(branch.Id, outlet.BranchId);
        Assert.Equal(OutletType.AcBar, outlet.OutletType);

        // 5. Terminal
        var terminal = Terminal.Create(
            outlet,
            "POS-01",
            "Main Bar POS Counter");
        Assert.Equal(tenant.Id, terminal.TenantId);
        Assert.Equal(outlet.Id, terminal.OutletId);
        Assert.Equal("POS-01", terminal.TerminalCode);

        // 6. Device
        var device = Device.Register(
            tenant.Id,
            "DEV-POS-01",
            "CPU-INTEL-8821-X99",
            "Bar Counter Touch Terminal");
        device.AssignToTerminal(terminal);

        Assert.Equal(tenant.Id, device.TenantId);
        Assert.Equal(DeviceScope.TerminalLevel, device.Scope);
        Assert.Equal(terminal.Id, device.TerminalId);
        Assert.Equal(outlet.Id, device.OutletId);
        device.Activate();
        Assert.Equal(DeviceStatus.Active, device.Status);

        // Domain events verification
        Assert.Contains(tenant.DomainEvents, e => e is TenantCreatedEvent);
        Assert.Contains(org.DomainEvents, e => e is OrganizationCreatedEvent);
        Assert.Contains(branch.DomainEvents, e => e is BranchCreatedEvent);
        Assert.Contains(outlet.DomainEvents, e => e is OutletCreatedEvent);
        Assert.Contains(terminal.DomainEvents, e => e is TerminalCreatedEvent);
        Assert.Contains(device.DomainEvents, e => e is DeviceRegisteredEvent);
        Assert.Contains(device.DomainEvents, e => e is DeviceAssignedToScopeEvent);
    }

    [Fact]
    public void CrossTenantOwnership_DeviceAssignedToDifferentTenantTerminal_ThrowsInvalidOwnershipException()
    {
        var tenant1 = Tenant.Create("Tenant One", "T1", "t1@test.com", "1111111111");
        var tenant2 = Tenant.Create("Tenant Two", "T2", "t2@test.com", "2222222222");

        var org2 = Organization.Create(tenant2.Id, "Org 2", "O2", _validTaxNumber, _validLicenseNumber);
        var branch2 = Branch.Create(org2, "Branch 2", "BR2", _testAddress);
        var outlet2 = Outlet.Create(branch2, "Outlet 2", OutletType.MainDining);
        var terminal2 = Terminal.Create(outlet2, "TERM-2", "Terminal 2");

        var device1 = Device.Register(tenant1.Id, "DEV-1", "HW-111", "Device 1");

        var ex = Assert.Throws<InvalidOwnershipException>(() => device1.AssignToTerminal(terminal2));
        Assert.Contains("Cross-tenant assignment blocked", ex.Message);
    }

    [Fact]
    public void InvalidStateTransitions_TerminatedTenant_ThrowsInvalidStateTransitionException()
    {
        var tenant = Tenant.Create("Test Tenant", "Trade", "email@test.com", "1234567890");
        tenant.Terminate();
        Assert.Equal(TenantStatus.Terminated, tenant.Status);

        Assert.Throws<InvalidStateTransitionException>(() => tenant.Activate());
        Assert.Throws<InvalidStateTransitionException>(() => tenant.Suspend());
    }

    [Fact]
    public void InvalidStateTransitions_RevokedDevice_ThrowsInvalidStateTransitionException()
    {
        var tenantId = TenantId.New();
        var device = Device.Register(tenantId, "DEV-01", "HW-999", "Waitron Tablet");
        device.Revoke();

        Assert.Throws<InvalidStateTransitionException>(() => device.Activate());
        Assert.Throws<InvalidStateTransitionException>(() => device.AssignScope(DeviceScope.BranchLevel, BranchId.New(), null, null));
    }

    [Fact]
    public void StronglyTypedIdentifiers_EmptyGuid_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new TenantId(Guid.Empty));
        Assert.Throws<DomainException>(() => new OrganizationId(Guid.Empty));
        Assert.Throws<DomainException>(() => new BranchId(Guid.Empty));
        Assert.Throws<DomainException>(() => new OutletId(Guid.Empty));
        Assert.Throws<DomainException>(() => new TerminalId(Guid.Empty));
        Assert.Throws<DomainException>(() => new DeviceId(Guid.Empty));
    }

    [Fact]
    public void TaxNumber_InvalidGstinFormat_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new TaxNumber("INVALID_GSTIN"));
        Assert.Throws<DomainException>(() => new TaxNumber(""));
    }

    [Fact]
    public void DeviceScope_ScopeHierarchyMismatch_ThrowsDomainException()
    {
        var tenantId = TenantId.New();
        var branchId = BranchId.New();
        var outletId = OutletId.New();
        var terminalId = TerminalId.New();

        Assert.Throws<DomainException>(() => Device.Register(
            tenantId, "DEV-X", "HW-X", "Name",
            scope: DeviceScope.TerminalLevel,
            branchId: branchId,
            outletId: null,
            terminalId: null));

        Assert.Throws<DomainException>(() => Device.Register(
            tenantId, "DEV-Y", "HW-Y", "Name",
            scope: DeviceScope.BranchLevel,
            branchId: branchId,
            outletId: outletId,
            terminalId: null));
    }

    [Fact]
    public void ValueObjectEquality_StructuralComparison_WorksAsExpected()
    {
        var id1 = TenantId.Create(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var id2 = TenantId.Create(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var id3 = TenantId.Create(Guid.Parse("22222222-2222-2222-2222-222222222222"));

        Assert.Equal(id1, id2);
        Assert.True(id1 == id2);
        Assert.NotEqual(id1, id3);
        Assert.True(id1 != id3);

        var addr1 = new Address("123 St", null, "Pune", "MH", "411001");
        var addr2 = new Address("123 St", null, "Pune", "MH", "411001");
        Assert.Equal(addr1, addr2);
    }
}
