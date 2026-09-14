using Yashdeep.Application.Pos.DTOs;
using Yashdeep.Application.Pos.Workflows;
using Yashdeep.Domain.ValueObjects;

namespace Yashdeep.Application.Pos.UI;

public class PosTerminalUiController
{
    private readonly CompletePosWorkflowUseCase _workflowUseCase;
    private readonly List<PosOrderItemRequest> _currentCart = new();

    public Guid TenantId { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; } = Guid.NewGuid();
    public Guid LocationId { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; } = Guid.NewGuid();
    public string ActiveTableNumber { get; set; } = "T-01";
    public SectionTier ActiveSection { get; set; } = SectionTier.Ac;
    public DateOnly ActiveBusinessDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public Guid CaptainUserId { get; set; } = Guid.NewGuid();
    public string WaiterName { get; set; } = "Captain Ramesh";
    public string CashierUserId { get; set; } = "CASHIER_01";
    public decimal DiscountPercentage { get; set; } = 0m;

    public IReadOnlyCollection<PosOrderItemRequest> CurrentCart => _currentCart.AsReadOnly();

    public PosTerminalUiController(CompletePosWorkflowUseCase workflowUseCase)
    {
        _workflowUseCase = workflowUseCase ?? throw new ArgumentNullException(nameof(workflowUseCase));
    }

    /// <summary>
    /// Keyboard shortcut '1' / Touch Button: Add Item to Cart
    /// </summary>
    public void AddItemToCart(
        Guid menuItemId,
        string itemCode,
        string englishName,
        string marathiName,
        DepartmentType department,
        decimal quantity,
        decimal unitPrice,
        int? unitVolumeMl = null)
    {
        _currentCart.Add(new PosOrderItemRequest(
            menuItemId,
            itemCode,
            englishName,
            marathiName,
            department,
            quantity,
            unitPrice,
            unitVolumeMl
        ));
    }

    /// <summary>
    /// Clear active cart contents
    /// </summary>
    public void ClearCart()
    {
        _currentCart.Clear();
    }

    /// <summary>
    /// Keyboard shortcut 'Enter' / Touch Button 'Process Checkout & Settle'
    /// Executes full end-to-end POS vertical slice atomically.
    /// </summary>
    public async Task<PosWorkflowResult> ProcessCheckoutAndSettleAsync(
        List<ProcessPaymentRequest> payments,
        CancellationToken cancellationToken = default)
    {
        if (_currentCart.Count == 0)
            throw new InvalidOperationException("Cannot process checkout with an empty cart.");

        string orderNumber = $"ORD-{ActiveBusinessDate:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpperInvariant()}";

        var command = new CompletePosWorkflowCommand(
            TenantId,
            BranchId,
            LocationId,
            DeviceId,
            ActiveTableNumber,
            ActiveSection,
            OrderType.DineIn,
            orderNumber,
            ActiveBusinessDate,
            CaptainUserId,
            WaiterName,
            CashierUserId,
            _currentCart.ToList(),
            DiscountPercentage,
            payments
        );

        var result = await _workflowUseCase.ExecuteAsync(command, cancellationToken);
        _currentCart.Clear();
        return result;
    }
}
