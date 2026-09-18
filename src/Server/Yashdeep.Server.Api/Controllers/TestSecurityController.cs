using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yashdeep.Domain.Contexts;
using Yashdeep.Domain.Contracts;
using Yashdeep.Persistence.Cloud;

namespace Yashdeep.Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestSecurityController : ControllerBase
{
    private readonly IRequestContext _requestContext;
    private readonly CloudDbContext _dbContext;

    public TestSecurityController(IRequestContext requestContext, CloudDbContext dbContext)
    {
        _requestContext = requestContext;
        _dbContext = dbContext;
    }

    [HttpGet("tenant-data")]
    [Authorize(Policy = "TenantOnly")]
    public IActionResult GetTenantData([FromQuery] Guid? tenantId)
    {
        if (tenantId.HasValue && tenantId.Value != _requestContext.Tenant.TenantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Manipulated tenant identifier in query parameter rejected." });
        }

        return Ok(new
        {
            Message = "Tenant access granted.",
            TenantId = _requestContext.Tenant.TenantId,
            UserId = _requestContext.User.UserId
        });
    }

    [HttpGet("branch-data/{branchId}")]
    [Authorize(Policy = "BranchScoped")]
    public IActionResult GetBranchData([FromRoute] Guid branchId)
    {
        if (_requestContext.Branch.IsBranchResolved &&
            branchId != _requestContext.Branch.BranchId &&
            !_requestContext.Branch.IsOrganizationAccess &&
            !_requestContext.User.IsSystemAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Manipulated branch identifier in route rejected." });
        }

        return Ok(new
        {
            Message = "Branch access granted.",
            TenantId = _requestContext.Tenant.TenantId,
            BranchId = branchId,
            IsOrganizationAccess = _requestContext.Branch.IsOrganizationAccess
        });
    }

    [HttpGet("org-summary")]
    [Authorize(Policy = "OrganizationScoped")]
    public IActionResult GetOrganizationSummary()
    {
        return Ok(new
        {
            Message = "Organization access granted.",
            TenantId = _requestContext.Tenant.TenantId,
            AllowedBranchIds = _requestContext.Branch.AllowedBranchIds
        });
    }

    [HttpGet("admin-portal")]
    [Authorize(Policy = "SystemAdminOnly")]
    public IActionResult GetAdminPortal()
    {
        return Ok(new
        {
            Message = "System Admin access granted.",
            UserId = _requestContext.User.UserId
        });
    }

    [HttpPost("orders")]
    [Authorize(Policy = "BranchScoped")]
    public async Task<IActionResult> CreateOrder([FromBody] TestOrderRequest request)
    {
        if (request.TenantId != Guid.Empty && request.TenantId != _requestContext.Tenant.TenantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Manipulated tenant identifier in request body rejected." });
        }

        var entity = new TestOrderEntity
        {
            TenantId = _requestContext.Tenant.TenantId,
            BranchId = _requestContext.Branch.BranchId ?? request.BranchId,
            Description = request.Description,
            Amount = request.Amount
        };

        _dbContext.TestOrders.Add(entity);
        await _dbContext.SaveChangesAsync();

        return Ok(entity);
    }
}

public class TestOrderRequest : IBranchEntity
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
