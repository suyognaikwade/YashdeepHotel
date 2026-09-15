using Yashdeep.Domain.ValueObjects;
using Yashdeep.Shared.Time;

namespace Yashdeep.Domain.Entities.Billing;

public class BillTaxLine
{
    public string TaxName { get; }
    public decimal RatePercentage { get; }
    public Money TaxAmount { get; }

    public BillTaxLine(string taxName, decimal ratePercentage, Money taxAmount)
    {
        if (ratePercentage < 0)
            throw new ArgumentOutOfRangeException(nameof(ratePercentage), "Tax rate percentage cannot be negative.");

        TaxName = taxName ?? throw new ArgumentNullException(nameof(taxName));
        RatePercentage = ratePercentage;
        TaxAmount = taxAmount;
    }
}

public class Bill
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid OrderId { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public string InvoiceNumber { get; private set; }
    public long DailySequenceNumber { get; private set; }
    public string TableNumber { get; private set; }
    public string WaiterName { get; private set; }

    public BillStatus Status { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }

    public Money SubTotal { get; private set; } = Money.Zero;
    public Money FoodSubTotal { get; private set; } = Money.Zero;
    public Money LiquorSubTotal { get; private set; } = Money.Zero;
    public Money TotalDiscount { get; private set; } = Money.Zero;
    public Money TotalTax { get; private set; } = Money.Zero;
    public Money ServiceCharge { get; private set; } = Money.Zero;
    public Money GrandTotal { get; private set; } = Money.Zero;
    public Money TotalPaid { get; private set; } = Money.Zero;
    public Money BalanceDue => new(Math.Max(0m, GrandTotal.Amount - TotalPaid.Amount), GrandTotal.Currency);

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? SettledAtUtc { get; private set; }

    private readonly List<BillTaxLine> _taxLines = new();
    public IReadOnlyCollection<BillTaxLine> TaxLines => _taxLines.AsReadOnly();

    private readonly List<Payment> _payments = new();
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();

    private Bill()
    {
        InvoiceNumber = string.Empty;
        TableNumber = string.Empty;
        WaiterName = string.Empty;
    }

    public Bill(
        Guid id,
        Guid tenantId,
        Guid branchId,
        Guid locationId,
        Guid orderId,
        DateOnly businessDate,
        string invoiceNumber,
        long dailySequenceNumber,
        string tableNumber,
        string waiterName,
        Money foodSubTotal,
        Money liquorSubTotal,
        IDateTimeProvider timeProvider,
        decimal discountPercentage = 0m,
        decimal foodCgstPercent = 2.5m,
        decimal foodSgstPercent = 2.5m,
        decimal liquorVatPercent = 0m)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (branchId == Guid.Empty) throw new ArgumentException("BranchId is required.", nameof(branchId));
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId is required.", nameof(orderId));
        if (discountPercentage < 0 || discountPercentage > 100) throw new ArgumentOutOfRangeException(nameof(discountPercentage), "Discount percentage must be between 0 and 100.");
        if (foodCgstPercent < 0) throw new ArgumentOutOfRangeException(nameof(foodCgstPercent), "CGST percentage cannot be negative.");
        if (foodSgstPercent < 0) throw new ArgumentOutOfRangeException(nameof(foodSgstPercent), "SGST percentage cannot be negative.");
        if (liquorVatPercent < 0) throw new ArgumentOutOfRangeException(nameof(liquorVatPercent), "VAT percentage cannot be negative.");

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        TenantId = tenantId;
        BranchId = branchId;
        LocationId = locationId;
        OrderId = orderId;
        BusinessDate = businessDate;
        InvoiceNumber = string.IsNullOrWhiteSpace(invoiceNumber) ? throw new ArgumentException("Invoice number cannot be empty.", nameof(invoiceNumber)) : invoiceNumber;
        DailySequenceNumber = dailySequenceNumber;
        TableNumber = tableNumber ?? string.Empty;
        WaiterName = waiterName ?? string.Empty;
        Status = BillStatus.Printed;
        PaymentStatus = PaymentStatus.Unpaid;
        CreatedAtUtc = timeProvider.UtcNow;

        FoodSubTotal = foodSubTotal;
        LiquorSubTotal = liquorSubTotal;
        SubTotal = foodSubTotal + liquorSubTotal;

        CalculateTaxesAndTotals(discountPercentage, foodCgstPercent, foodSgstPercent, liquorVatPercent);
    }

    private void CalculateTaxesAndTotals(
        decimal discountPercentage,
        decimal foodCgstPercent,
        decimal foodSgstPercent,
        decimal liquorVatPercent)
    {
        decimal subTotalAmt = SubTotal.Amount;
        decimal discountAmt = Math.Round(subTotalAmt * (discountPercentage / 100m), 2, MidpointRounding.AwayFromZero);
        TotalDiscount = new Money(discountAmt, SubTotal.Currency);

        decimal foodRatio = subTotalAmt > 0 ? FoodSubTotal.Amount / subTotalAmt : 0m;
        decimal liquorRatio = subTotalAmt > 0 ? LiquorSubTotal.Amount / subTotalAmt : 0m;

        decimal netFoodTaxable = Math.Max(0m, FoodSubTotal.Amount - (discountAmt * foodRatio));
        decimal netLiquorTaxable = Math.Max(0m, LiquorSubTotal.Amount - (discountAmt * liquorRatio));

        _taxLines.Clear();
        decimal totalTaxAmt = 0m;

        if (netFoodTaxable > 0 && foodCgstPercent > 0)
        {
            decimal cgst = Math.Round(netFoodTaxable * (foodCgstPercent / 100m), 2, MidpointRounding.AwayFromZero);
            _taxLines.Add(new BillTaxLine("CGST", foodCgstPercent, new Money(cgst, SubTotal.Currency)));
            totalTaxAmt += cgst;
        }

        if (netFoodTaxable > 0 && foodSgstPercent > 0)
        {
            decimal sgst = Math.Round(netFoodTaxable * (foodSgstPercent / 100m), 2, MidpointRounding.AwayFromZero);
            _taxLines.Add(new BillTaxLine("SGST", foodSgstPercent, new Money(sgst, SubTotal.Currency)));
            totalTaxAmt += sgst;
        }

        if (netLiquorTaxable > 0 && liquorVatPercent > 0)
        {
            decimal vat = Math.Round(netLiquorTaxable * (liquorVatPercent / 100m), 2, MidpointRounding.AwayFromZero);
            _taxLines.Add(new BillTaxLine("VAT", liquorVatPercent, new Money(vat, SubTotal.Currency)));
            totalTaxAmt += vat;
        }

        TotalTax = new Money(totalTaxAmt, SubTotal.Currency);

        decimal rawGrandTotal = (subTotalAmt - discountAmt) + totalTaxAmt;
        GrandTotal = new Money(Math.Round(rawGrandTotal, 0, MidpointRounding.AwayFromZero), SubTotal.Currency);
    }

    public void AddPayment(Payment payment, IDateTimeProvider timeProvider, bool allowOverpayment = false)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(payment);

        if (Status == BillStatus.Cancelled)
            throw new InvalidOperationException("Cannot add payment to a cancelled bill.");

        if (Status == BillStatus.Paid || PaymentStatus == PaymentStatus.Paid)
            throw new InvalidOperationException("Bill is already fully paid.");

        if (payment.TenantId != TenantId || payment.BranchId != BranchId)
            throw new InvalidOperationException($"Payment tenant/branch mismatch. Bill ({TenantId}/{BranchId}) vs Payment ({payment.TenantId}/{payment.BranchId})");

        decimal proposedTotalPaid = TotalPaid.Amount + payment.Amount.Amount;
        if (!allowOverpayment && proposedTotalPaid > GrandTotal.Amount)
        {
            throw new InvalidOperationException($"Payment amount {payment.Amount.Amount} exceeds outstanding balance {BalanceDue.Amount}. Split payments must reconcile exactly.");
        }

        _payments.Add(payment);
        TotalPaid = new Money(proposedTotalPaid, GrandTotal.Currency);

        if (TotalPaid.Amount >= GrandTotal.Amount)
        {
            PaymentStatus = PaymentStatus.Paid;
            Status = BillStatus.Paid;
            SettledAtUtc = timeProvider.UtcNow;
        }
        else
        {
            PaymentStatus = PaymentStatus.PartiallyPaid;
        }
    }
}
