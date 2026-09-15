using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Yashdeep.Application.Pos.Services;
using Yashdeep.Shared.Contracts;
using Yashdeep.Shared.Results;

namespace Yashdeep.Server.Api.Controllers;

[ApiController]
[Route("api/v1/pos")]
[Authorize]
public class PosController : ControllerBase
{
    private readonly IPosApplicationService _posService;

    public PosController(IPosApplicationService posService)
    {
        _posService = posService ?? throw new ArgumentNullException(nameof(posService));
    }

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContextIdentifiers(out var tenantId, out var branchId, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _posService.CreateOrderAsync(tenantId, branchId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("orders/{id:guid}/items")]
    public async Task<IActionResult> AddOrderItem([FromRoute] Guid id, [FromBody] AddOrderItemRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContextIdentifiers(out var tenantId, out var branchId, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _posService.AddOrderItemAsync(tenantId, branchId, id, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("orders/{id:guid}/kot")]
    public async Task<IActionResult> GenerateKot([FromRoute] Guid id, [FromBody] GenerateKotRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContextIdentifiers(out var tenantId, out var branchId, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _posService.GenerateKotAsync(tenantId, branchId, id, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("orders/{id:guid}/bill")]
    public async Task<IActionResult> GenerateBill([FromRoute] Guid id, [FromBody] GenerateBillRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContextIdentifiers(out var tenantId, out var branchId, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _posService.GenerateBillAsync(tenantId, branchId, id, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("bills/{id:guid}/payments")]
    public async Task<IActionResult> RecordPayment([FromRoute] Guid id, [FromBody] RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContextIdentifiers(out var tenantId, out var branchId, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _posService.RecordPaymentAsync(tenantId, branchId, id, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("orders/{id:guid}/complete")]
    public async Task<IActionResult> CompleteOrder([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetContextIdentifiers(out var tenantId, out var branchId, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _posService.CompleteOrderAsync(tenantId, branchId, id, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("stock-movements")]
    public async Task<IActionResult> RecordStockMovement([FromBody] RecordStockMovementRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContextIdentifiers(out var tenantId, out var branchId, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _posService.RecordStockMovementAsync(tenantId, branchId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("audits")]
    public async Task<IActionResult> PersistAudit([FromBody] PersistAuditRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContextIdentifiers(out var tenantId, out var branchId, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _posService.PersistAuditAsync(tenantId, branchId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("sync/queue")]
    public async Task<IActionResult> QueueSyncEvent([FromBody] QueueSyncEventRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContextIdentifiers(out var tenantId, out var branchId, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _posService.QueueSyncEventAsync(tenantId, branchId, request, cancellationToken);
        return HandleResult(result);
    }

    private bool TryGetContextIdentifiers(out Guid tenantId, out Guid branchId, out IActionResult? errorResult)
    {
        tenantId = Guid.Empty;
        branchId = Guid.Empty;
        errorResult = null;

        if (HttpContext.Items.TryGetValue("TenantId", out var tenantIdObj) && tenantIdObj is Guid tId)
        {
            tenantId = tId;
        }
        else
        {
            var tenantClaim = User.FindFirst("tenant_id")?.Value ?? User.FindFirst("TenantId")?.Value;
            if (!string.IsNullOrEmpty(tenantClaim) && Guid.TryParse(tenantClaim, out var parsedTenant))
            {
                tenantId = parsedTenant;
            }
        }

        if (HttpContext.Items.TryGetValue("BranchId", out var branchIdObj) && branchIdObj is Guid bId)
        {
            branchId = bId;
        }
        else
        {
            var branchClaim = User.FindFirst("branch_id")?.Value ?? User.FindFirst("BranchId")?.Value;
            if (!string.IsNullOrEmpty(branchClaim) && Guid.TryParse(branchClaim, out var parsedBranch))
            {
                branchId = parsedBranch;
            }
        }

        if (tenantId == Guid.Empty)
        {
            errorResult = Unauthorized(ApiResponse<object>.Fail(
                Error.Unauthorized("AUTH.MISSING_TENANT", "Authenticated tenant context is required.")
            ));
            return false;
        }

        if (branchId == Guid.Empty)
        {
            errorResult = BadRequest(ApiResponse<object>.Fail(
                Error.Validation("AUTH.MISSING_BRANCH", "Authenticated branch context is required.")
            ));
            return false;
        }

        return true;
    }

    private IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(ApiResponse<T>.Ok(result.Value!));
        }

        var error = result.Error;
        return error.Type switch
        {
            ErrorType.NotFound => NotFound(ApiResponse<T>.Fail(error)),
            ErrorType.Validation => BadRequest(ApiResponse<T>.Fail(error)),
            ErrorType.Unauthorized => Unauthorized(ApiResponse<T>.Fail(error)),
            ErrorType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, ApiResponse<T>.Fail(error)),
            ErrorType.Conflict => Conflict(ApiResponse<T>.Fail(error)),
            _ => StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<T>.Fail(error))
        };
    }
}
