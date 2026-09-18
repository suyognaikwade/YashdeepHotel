using Yashdeep.Domain.ValueObjects;
using Yashdeep.Shared.Time;

namespace Yashdeep.Domain.Entities.Billing;

public class Payment
{
    public Guid Id { get; private set; }
    public Guid BillId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public Money Amount { get; private set; }
    public string TransactionReference { get; private set; }
    public DateTime PaidAtUtc { get; private set; }
    public string CashierUserId { get; private set; }

    private Payment()
    {
        TransactionReference = string.Empty;
        CashierUserId = string.Empty;
    }

    public Payment(
        Guid id,
        Guid billId,
        Guid tenantId,
        Guid branchId,
        PaymentMethod method,
        Money amount,
        string transactionReference,
        string cashierUserId,
        IDateTimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (branchId == Guid.Empty) throw new ArgumentException("BranchId is required.", nameof(branchId));
        if (billId == Guid.Empty) throw new ArgumentException("BillId is required.", nameof(billId));
        if (amount.Amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be greater than zero.");

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        BillId = billId;
        TenantId = tenantId;
        BranchId = branchId;
        Method = method;
        Amount = amount;
        TransactionReference = transactionReference ?? string.Empty;
        PaidAtUtc = timeProvider.UtcNow;
        CashierUserId = cashierUserId ?? string.Empty;
    }
}
