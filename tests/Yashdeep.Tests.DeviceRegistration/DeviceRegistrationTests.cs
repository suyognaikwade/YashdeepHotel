using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Yashdeep.Application.Models;
using Yashdeep.Application.Services;
using Yashdeep.Domain.Entities;
using Yashdeep.Domain.Enums;
using Yashdeep.Domain.Events;
using Yashdeep.Infrastructure.Hardware;
using Yashdeep.Infrastructure.Persistence;
using Yashdeep.Infrastructure.Security;
using Yashdeep.Shared.Security;

namespace Yashdeep.Tests.DeviceRegistration;

public class DeviceRegistrationTests
{
    private readonly InMemoryDeviceRepository _repository;
    private readonly InMemoryAuditEventLogger _auditLogger;
    private readonly DeviceTokenService _tokenService;
    private readonly DeviceRegistrationService _service;

    public DeviceRegistrationTests()
    {
        _repository = new InMemoryDeviceRepository();
        _auditLogger = new InMemoryAuditEventLogger();
        _tokenService = new DeviceTokenService("Test-Secret-Key-1234567890-32BytesLong!!");
        _service = new DeviceRegistrationService(_repository, _tokenService, _auditLogger);
    }

    [Fact]
    public async Task TenantBoundDeviceRegistration_SucceedsAndBindsTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var provider = new WindowsDeviceIdentityProvider();
        var hwFingerprint = provider.GetHardwareFingerprint();

        var request = new RegisterDeviceRequest(
            tenantId,
            orgId,
            branchId,
            null,
            null,
            "Main-Counter-POS",
            hwFingerprint,
            "Windows",
            DeviceType.DesktopPos);

        // Act
        var response = await _service.RegisterDeviceAsync(request);

        // Assert
        Assert.NotEqual(Guid.Empty, response.DeviceId);
        Assert.Equal(tenantId, response.TenantId);
        Assert.Equal(branchId, response.BranchId);
        Assert.Equal("Pending", response.Status);
        Assert.False(string.IsNullOrWhiteSpace(response.ActivationCode));

        var storedDevice = await _repository.GetByIdAsync(response.DeviceId);
        Assert.NotNull(storedDevice);
        Assert.Equal(tenantId, storedDevice.TenantId);
        Assert.Equal(branchId, storedDevice.BranchId);

        var loggedEvents = _auditLogger.GetLoggedEvents();
        Assert.Contains(loggedEvents, e => e is DeviceRegisteredEvent re && re.DeviceId == response.DeviceId);
    }

    [Fact]
    public async Task BranchBoundDeviceRegistration_ActivationFailsIfBranchMismatch()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var branchId1 = Guid.NewGuid();
        var branchId2 = Guid.NewGuid();

        var provider = new TestDeviceIdentityProvider("TestWin");
        var reg = await _service.RegisterDeviceAsync(new RegisterDeviceRequest(
            tenantId, orgId, branchId1, null, null, "POS-1", provider.GetHardwareFingerprint(), "Windows", DeviceType.DesktopPos));

        // Act & Assert - Attempt activation under branch2
        var activateReq = new ActivateDeviceRequest(
            tenantId,
            branchId2, // Mismatched branch
            reg.DeviceId,
            reg.ActivationCode,
            provider.GetHardwareFingerprint());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ActivateDeviceAsync(activateReq));
        Assert.Contains("Branch mismatch", ex.Message);
    }

    [Fact]
    public async Task DuplicateDeviceRegistration_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var provider = new AndroidDeviceIdentityProvider();
        var hwFingerprint = provider.GetHardwareFingerprint();

        var request = new RegisterDeviceRequest(
            tenantId, orgId, branchId, null, null, "Tablet-1", hwFingerprint, "Android", DeviceType.MobilePos);

        await _service.RegisterDeviceAsync(request);

        // Act & Assert - Second registration with same hardware fingerprint
        var duplicateReq = new RegisterDeviceRequest(
            tenantId, orgId, branchId, null, null, "Tablet-2", hwFingerprint, "Android", DeviceType.MobilePos);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterDeviceAsync(duplicateReq));
        Assert.Contains("Duplicate device registration attempt", ex.Message);
    }

    [Fact]
    public async Task SuspendedDeviceRejection_PreventsActivationAndTokenIssuance()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var provider = new TestDeviceIdentityProvider();
        var reg = await _service.RegisterDeviceAsync(new RegisterDeviceRequest(
            tenantId, orgId, branchId, null, null, "Terminal-1", provider.GetHardwareFingerprint(), "Windows", DeviceType.DesktopPos));

        await _service.SuspendDeviceAsync(new SuspendDeviceRequest(tenantId, reg.DeviceId, "Suspicious activity detected"));

        var status = await _service.GetDeviceStatusAsync(tenantId, reg.DeviceId);
        Assert.Equal(DeviceLifecycleState.Suspended, status.State);

        // Act & Assert - Attempt activation while suspended
        var activateReq = new ActivateDeviceRequest(
            tenantId, branchId, reg.DeviceId, reg.ActivationCode, provider.GetHardwareFingerprint());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ActivateDeviceAsync(activateReq));
        Assert.Contains("Suspended device rejection", ex.Message);

        var loggedEvents = _auditLogger.GetLoggedEvents();
        Assert.Contains(loggedEvents, e => e is DeviceSuspendedEvent se && se.DeviceId == reg.DeviceId);
    }

    [Fact]
    public async Task RevokedDeviceRejection_PreventsAnyActivationOrOperation()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var provider = new TestDeviceIdentityProvider();
        var reg = await _service.RegisterDeviceAsync(new RegisterDeviceRequest(
            tenantId, orgId, branchId, null, null, "KDS-1", provider.GetHardwareFingerprint(), "Windows", DeviceType.Kds));

        await _service.RevokeDeviceAsync(new RevokeDeviceRequest(tenantId, reg.DeviceId, "Lost or stolen device"));

        var status = await _service.GetDeviceStatusAsync(tenantId, reg.DeviceId);
        Assert.Equal(DeviceLifecycleState.Revoked, status.State);

        // Act & Assert - Attempt activation while revoked
        var activateReq = new ActivateDeviceRequest(
            tenantId, branchId, reg.DeviceId, reg.ActivationCode, provider.GetHardwareFingerprint());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ActivateDeviceAsync(activateReq));
        Assert.Contains("Revoked device rejection", ex.Message);

        var loggedEvents = _auditLogger.GetLoggedEvents();
        Assert.Contains(loggedEvents, e => e is DeviceRevokedEvent re && re.DeviceId == reg.DeviceId);
    }

    [Fact]
    public async Task DeviceReplacement_RetiresOldDeviceAndRegistersNewDevice()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var oldProvider = new WindowsDeviceIdentityProvider(systemUuid: "OLD-WIN-UUID");
        var newProvider = new WindowsDeviceIdentityProvider(systemUuid: "NEW-WIN-UUID");

        var oldReg = await _service.RegisterDeviceAsync(new RegisterDeviceRequest(
            tenantId, orgId, branchId, null, null, "Old-POS", oldProvider.GetHardwareFingerprint(), "Windows", DeviceType.DesktopPos));

        await _service.ActivateDeviceAsync(new ActivateDeviceRequest(
            tenantId, branchId, oldReg.DeviceId, oldReg.ActivationCode, oldProvider.GetHardwareFingerprint()));

        // Act - Replace old device
        var replaceReq = new ReplaceDeviceRequest(
            tenantId,
            oldReg.DeviceId,
            newProvider.GetHardwareFingerprint(),
            "New-POS-Terminal",
            "Hardware hardware upgrade");

        var newReg = await _service.ReplaceDeviceAsync(replaceReq);

        // Assert
        Assert.NotEqual(oldReg.DeviceId, newReg.DeviceId);

        var oldStatus = await _service.GetDeviceStatusAsync(tenantId, oldReg.DeviceId);
        Assert.Equal(DeviceLifecycleState.Retired, oldStatus.State);
        Assert.Equal(newReg.DeviceId, oldStatus.ReplacedByDeviceId);

        var newStatus = await _service.GetDeviceStatusAsync(tenantId, newReg.DeviceId);
        Assert.Equal(DeviceLifecycleState.Pending, newStatus.State);
        Assert.Equal(newProvider.GetHardwareFingerprint(), newStatus.HardwareFingerprint);

        var loggedEvents = _auditLogger.GetLoggedEvents();
        Assert.Contains(loggedEvents, e => e is DeviceReplacedEvent rep && rep.DeviceId == oldReg.DeviceId && rep.ReplacementDeviceId == newReg.DeviceId);
    }

    [Fact]
    public async Task UnauthorizedDeviceActivation_FailsWhenInvalidCodeProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var provider = new TestDeviceIdentityProvider();
        var reg = await _service.RegisterDeviceAsync(new RegisterDeviceRequest(
            tenantId, orgId, branchId, null, null, "Bar-POS", provider.GetHardwareFingerprint(), "Windows", DeviceType.BarStation));

        // Act & Assert - Invalid code
        var activateReq = new ActivateDeviceRequest(
            tenantId, branchId, reg.DeviceId, "000000", provider.GetHardwareFingerprint());

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.ActivateDeviceAsync(activateReq));
        Assert.Contains("Unauthorized device activation", ex.Message);
    }

    [Fact]
    public async Task CrossTenantDeviceAccess_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var provider = new TestDeviceIdentityProvider();
        var reg = await _service.RegisterDeviceAsync(new RegisterDeviceRequest(
            tenant1, orgId, branchId, null, null, "POS-Tenant1", provider.GetHardwareFingerprint(), "Windows", DeviceType.DesktopPos));

        // Act & Assert - Accessing tenant1's device using tenant2 context
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.GetDeviceStatusAsync(tenant2, reg.DeviceId));

        Assert.Contains("Cross-tenant device access denied", ex.Message);
    }

    [Fact]
    public void HardwareFingerprinting_DoesNotRelyOnMacAddressAlone()
    {
        // Arrange
        var info1 = new Yashdeep.Shared.Hardware.DeviceHardwareInfo("UUID-1", "CPU-1", "VOL-1", "MAC-A", "Windows", "Host1");
        var info2 = new Yashdeep.Shared.Hardware.DeviceHardwareInfo("UUID-1", "CPU-1", "VOL-1", "MAC-B", "Windows", "Host1");
        var info3 = new Yashdeep.Shared.Hardware.DeviceHardwareInfo("UUID-2", "CPU-1", "VOL-1", "MAC-A", "Windows", "Host1");

        // Act
        var fp1 = info1.GenerateFingerprint();
        var fp2 = info2.GenerateFingerprint();
        var fp3 = info3.GenerateFingerprint();

        // Assert
        // MAC address is part of the fingerprint calculation, but changing MAC alone changes hash while UUID/CPU remain bound.
        Assert.NotEqual(fp1, fp2);
        Assert.NotEqual(fp1, fp3);
        Assert.StartsWith("HW-WIN", fp1);
    }

    [Fact]
    public void TokenService_IssuesAndValidatesDeviceTokenWithoutCloudDbCredentials()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var payload = new DeviceTokenPayload(
            deviceId, tenantId, orgId, branchId, null, null, "HW-FINGERPRINT", "Active", DateTime.UtcNow, DateTime.UtcNow.AddHours(1));

        // Act
        var token = _tokenService.IssueToken(payload);
        var result = _tokenService.ValidateToken(token);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotNull(result.Payload);
        Assert.Equal(deviceId, result.Payload.DeviceId);
        Assert.Equal(tenantId, result.Payload.TenantId);
        Assert.False(token.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.False(token.Contains("Postgres", StringComparison.OrdinalIgnoreCase));
    }
}
