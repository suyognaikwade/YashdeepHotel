using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Yashdeep.Application.Models;
using Yashdeep.Application.Services;

namespace Yashdeep.Server.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceRegistrationService _deviceService;

    public DevicesController(IDeviceRegistrationService deviceService)
    {
        _deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
    }

    private Guid GetTenantIdFromHeaderOrRequest(Guid requestTenantId)
    {
        if (HttpContext.Items.TryGetValue("TenantId", out var item) && item is Guid tenantId && tenantId != Guid.Empty)
        {
            if (requestTenantId != Guid.Empty && requestTenantId != tenantId)
            {
                throw new UnauthorizedAccessException("Cross-tenant device access header mismatch.");
            }
            return tenantId;
        }

        if (requestTenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Missing tenant identifier.");
        }

        return requestTenantId;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request)
    {
        try
        {
            var tenantId = GetTenantIdFromHeaderOrRequest(request.TenantId);
            var normalizedRequest = request with { TenantId = tenantId };
            var response = await _deviceService.RegisterDeviceAsync(normalizedRequest);
            return CreatedAtAction(nameof(GetStatus), new { id = response.DeviceId }, response);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }

    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromBody] ActivateDeviceRequest request)
    {
        try
        {
            var tenantId = GetTenantIdFromHeaderOrRequest(request.TenantId);
            var normalizedRequest = request with { TenantId = tenantId };
            var response = await _deviceService.ActivateDeviceAsync(normalizedRequest);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] SuspendDeviceRequest request)
    {
        try
        {
            var tenantId = GetTenantIdFromHeaderOrRequest(request.TenantId);
            var normalizedRequest = request with { DeviceId = id, TenantId = tenantId };
            await _deviceService.SuspendDeviceAsync(normalizedRequest);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, [FromBody] RevokeDeviceRequest request)
    {
        try
        {
            var tenantId = GetTenantIdFromHeaderOrRequest(request.TenantId);
            var normalizedRequest = request with { DeviceId = id, TenantId = tenantId };
            await _deviceService.RevokeDeviceAsync(normalizedRequest);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/recover")]
    public async Task<IActionResult> Recover(Guid id, [FromBody] RecoverDeviceRequest request)
    {
        try
        {
            var tenantId = GetTenantIdFromHeaderOrRequest(request.TenantId);
            var normalizedRequest = request with { DeviceId = id, TenantId = tenantId };
            await _deviceService.RecoverDeviceAsync(normalizedRequest);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/replace")]
    public async Task<IActionResult> Replace(Guid id, [FromBody] ReplaceDeviceRequest request)
    {
        try
        {
            var tenantId = GetTenantIdFromHeaderOrRequest(request.TenantId);
            var normalizedRequest = request with { ExistingDeviceId = id, TenantId = tenantId };
            var response = await _deviceService.ReplaceDeviceAsync(normalizedRequest);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetStatus(Guid id, [FromQuery] Guid tenantId)
    {
        try
        {
            var resolvedTenantId = GetTenantIdFromHeaderOrRequest(tenantId);
            var status = await _deviceService.GetDeviceStatusAsync(resolvedTenantId, id);
            return Ok(status);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }

    [HttpGet("branch/{branchId:guid}")]
    public async Task<IActionResult> GetBranchDevices(Guid branchId, [FromQuery] Guid tenantId)
    {
        try
        {
            var resolvedTenantId = GetTenantIdFromHeaderOrRequest(tenantId);
            var devices = await _deviceService.GetBranchDevicesAsync(resolvedTenantId, branchId);
            return Ok(devices);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }
}
