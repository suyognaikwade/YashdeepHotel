using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Yashdeep.Application.Interfaces;
using Yashdeep.Persistence.Cloud;

namespace Yashdeep.Server.Api.Controllers;

[ApiController]
[Route("api/v1/tenant-data")]
[Authorize]
public class TenantDataController : ControllerBase
{
    private readonly CloudDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public TenantDataController(CloudDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    [HttpGet]
    public async Task<IActionResult> GetTenantData(CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated)
            return Unauthorized(new { message = "Tenant context is not authenticated." });

        var tenantUsers = await _dbContext.Users
            .Select(u => new { u.UserId, u.TenantId, u.Username, u.Email, u.FullName, u.IsActive })
            .ToListAsync(cancellationToken);

        var tenantOrgs = await _dbContext.Organizations
            .Select(o => new { o.OrganizationId, o.TenantId, o.LegalName, o.ExciseLicenseNumber })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            CurrentTenantId = _tenantContext.TenantId,
            CurrentUserId = _tenantContext.UserId,
            Users = tenantUsers,
            Organizations = tenantOrgs
        });
    }
}
