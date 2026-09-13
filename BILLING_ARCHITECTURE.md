# Yashdeep Hotel MS - Billing & Payment Architecture Specification

> **Module**: Billing, Pricing, Taxes, Discounts, Payments, Split Tenders, Sequential Numbering, Cancellation, Refunds, Auditability & Day End
> **Status**: Approved Architectural Specification
> **Target Platform**: .NET 9 Web API, EF Core, SQLite (Edge POS), PostgreSQL 16 (Cloud SaaS), Blazor Hybrid (MAUI)

---

## 1. Executive Summary & Legacy Analysis

### 1.1 Legacy Implementation Overview
In the legacy VB.NET / Access Jet 4.0 system (`RSS.exe` / `dinurss.mdb`), billing operations were concentrated in the monolithic form `FRMENTRY.vb` and executed through procedural inline SQL queries via `ClassDB.vb`. Data was stored across several primary tables:
- `BILLFINAL`: Active daily bill summaries (Subtotal, Discount, Service Charge, CGST, SGST, Total, Paid state, In-time).
- `finalbill`: Active itemized bill line items (BillNo, Item, Qty, Rate, Amount, Department, Section).
- `GrandBill` & `GrandBillDetails`: Cumulative historical mirrors of settled bills.
- `BILLFINAL_Dayend` & `finalbill_Dayend`: Archive tables populated during the daily "Day End" closing procedure (`FrmDtpDayend`).
- `CancelKot`: Unsettled or cancelled order lines.
- `Receipt` & `Payment`: General ledger voucher records.
- `TABLE_NO_GROP`: Dining section classifications (`Family`, `Ac`, `Hall`, `Restaurant`, `Garden`, `Parcel`).

### 1.2 Flaws & Failure Modes in Legacy Architecture

| Legacy Flaw | Impact & Failure Mode | Modern Architectural Solution |
| :--- | :--- | :--- |
| **Concurrency Lock Collisions (`0x80004005`)** | Multi-terminal simultaneous bill settlement locked `dinurss.mdb`, causing crashes during peak dining hours. | Offline-First Edge POS with SQLite (WAL mode) + Async Outbox Synchronization to PostgreSQL 16. |
| **Data Inconsistency across Dual Tables** | `BILLFINAL` and `GrandBill` often diverged when network glitches interrupted multi-statement updates. | Single-source-of-truth domain aggregate pattern with transactional integrity (EF Core DbContext / ACID). |
| **Single Payment Tender Limit** | `BILLFINAL.PAID` (0/1) only recorded binary payment state without split tenders (e.g. Cash + UPI). | Multi-Tender Payment Aggregate (`PaymentTransaction`) supporting split payment modes. |
| **Integer Truncation & Currency Flaws** | Bill amounts stored as `INTEGER` or `DOUBLE PRECISION`, risking rounding drift in GST calculations. | Precise `decimal(18,2)` representation for all monetary values using domain `Money` value objects. |
| **Destructive Day End Schema Moving** | Moving rows from `BILLFINAL` to `BILLFINAL_Dayend` destroyed temporal querying and required table locks. | Unified immutable temporal database structure; state transitions handled via indexed status flags. |
| **Lack of Immutable Audit Trails** | Bill edits (`FRMCORRECTIONBILL`) overwrote records directly without preserving historical revisions. | Append-only event-sourced `BillingAuditEvent` stream for all modifications, cancellations, and reprints. |

---

## 2. Core Domain Architecture & Clean Entities

The modern billing system is designed around the **DDD Aggregate Pattern**, where `BillAggregate` acts as the root boundary ensuring consistency for line items, taxes, discounts, and payment transactions.

```
                              ┌──────────────────────────┐
                              │     BillAggregate        │
                              │     (Aggregate Root)     │
                              └────────────┬─────────────┘
                                           │
         ┌──────────────────┬──────────────┼──────────────┬──────────────────┐
         │                  │              │              │                  │
         ▼                  ▼              ▼              ▼                  ▼
┌──────────────────┐ ┌─────────────┐ ┌───────────┐ ┌─────────────┐ ┌──────────────────┐
│     BillItem     │ │ BillTaxLine │ │DiscountVal│ │ PaymentTxn  │ │BillingAuditEvent │
│  (Line Items)    │ │(CGST/SGST/  │ │(Item/Bill)│ │ (Split      │ │  (Append-Only    │
│                  │ │    VAT)     │ │           │ │ Tenders)    │ │   Audit Log)     │
└──────────────────┘ └─────────────┘ └───────────┘ └─────────────┘ └──────────────────┘
```

### 2.1 C# 13 Domain Entities & Value Objects

```csharp
namespace Yashdeep.Domain.Entities.Billing;

public enum BillStatus
{
    Draft = 1,       // Active KOTs placed, bill not yet printed
    Printed = 2,     // Pro-forma bill printed for guest, table locked
    Settled = 3,     // Fully paid and closed
    Cancelled = 4,   // Voided/Cancelled by authorized manager
    Refunded = 5     // Fully or partially refunded post-settlement
}

public enum PaymentMethod
{
    Cash = 1,
    UpiDynamicQr = 2,
    Card = 3,
    CustomerLedger = 4, // Credit account / Permit holder ledger
    RoomTransfer = 5
}

public enum SectionType
{
    Hall = 1,
    Restaurant = 2,
    Family = 3,
    Ac = 4,
    Vip = 5,
    Garden = 6,
    Parcel = 7
}

public record Money(decimal Amount, string Currency = "INR")
{
    public static Money Zero => new(0m);
    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);
    public static Money operator -(Money a, Money b) => new(a.Amount - b.Amount);
    public Money Round() => new(Math.Round(Amount, 2, MidpointRounding.AwayFromZero));
}

public class BillAggregate
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public string InvoiceNumber { get; private me; } = string.Empty; // Global Tax Invoice No
    public long DailySequenceNumber { get; private set; }            // Daily resetting counter
    public string TableNumber { get; private set; } = string.Empty;
    public SectionType Section { get; private set; }
    public string WaiterName { get; private set; } = string.Empty;
    public string CustomerName { get; private set; } = string.Empty;
    public string? CustomerPhone { get; private set; }
    public string? PermitLicenseNumber { get; private set; }         // Excise Permit Holder Ref

    public BillStatus Status { get; private me; } = BillStatus.Draft;

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PrintedAtUtc { get; private set; }
    public DateTime? SettledAtUtc { get; private set; }

    // Monetary Summaries
    public Money SubTotal { get; private set; } = Money.Zero;
    public Money FoodSubTotal { get; private set; } = Money.Zero;
    public Money LiquorSubTotal { get; private set; } = Money.Zero;
    public Money TotalDiscount { get; private set; } = Money.Zero;
    public Money TotalTax { get; private set; } = Money.Zero;
    public Money ServiceCharge { get; private set; } = Money.Zero;
    public Money NetPayable { get; private set; } = Money.Zero;
    public Money TotalPaid { get; private set; } = Money.Zero;
    public Money BalanceDue => new(Math.Max(0m, NetPayable.Amount - TotalPaid.Amount));

    // Navigation Collections
    private readonly List<BillItem> _items = new();
    public IReadOnlyCollection<BillItem> Items => _items.AsReadOnly();

    private readonly List<BillTaxLine> _taxLines = new();
    public IReadOnlyCollection<BillTaxLine> TaxLines => _taxLines.AsReadOnly();

    private readonly List<PaymentTransaction> _payments = new();
    public IReadOnlyCollection<PaymentTransaction> Payments => _payments.AsReadOnly();

    private readonly List<BillingAuditEvent> _auditEvents = new();
    public IReadOnlyCollection<BillingAuditEvent> AuditEvents => _auditEvents.AsReadOnly();

    // Domain Logic Methods
    public void RecalculateTotals(decimal discountPercent = 0m, decimal fixedDiscount = 0m, decimal serviceChargePercent = 0m)
    {
        decimal foodSum = _items.Where(i => i.Department == "Food").Sum(i => i.LineTotal.Amount);
        decimal liquorSum = _items.Where(i => i.Department == "Liquor").Sum(i => i.LineTotal.Amount);
        decimal totalSub = foodSum + liquorSum;

        FoodSubTotal = new Money(foodSum);
        LiquorSubTotal = new Money(liquorSum);
        SubTotal = new Money(totalSub);

        // Discount Calculation
        decimal computedDiscount = (totalSub * (discountPercent / 100m)) + fixedDiscount;
        TotalDiscount = new Money(Math.Min(totalSub, computedDiscount)).Round();

        decimal taxableFood = Math.Max(0m, foodSum - (foodSum / (totalSub == 0 ? 1 : totalSub) * TotalDiscount.Amount));

        // Tax Engine (GST 2.5% CGST + 2.5% SGST on Food)
        _taxLines.Clear();
        if (taxableFood > 0)
        {
            decimal cgst = Math.Round(taxableFood * 0.025m, 2, MidpointRounding.AwayFromZero);
            decimal sgst = Math.Round(taxableFood * 0.025m, 2, MidpointRounding.AwayFromZero);
            _taxLines.Add(new BillTaxLine("CGST", 2.5m, new Money(cgst)));
            _taxLines.Add(new BillTaxLine("SGST", 2.5m, new Money(sgst)));
        }

        // Service Charge Calculation
        decimal scAmt = Math.Round((totalSub - TotalDiscount.Amount) * (serviceChargePercent / 100m), 2);
        ServiceCharge = new Money(scAmt);

        TotalTax = new Money(_taxLines.Sum(t => t.TaxAmount.Amount));

        // Net Payable with Half-Even Rounding
        decimal rawNet = SubTotal.Amount - TotalDiscount.Amount + TotalTax.Amount + ServiceCharge.Amount;
        NetPayable = new Money(Math.Round(rawNet, 0, MidpointRounding.AwayFromZero));
    }
}
```

---

## 3. Differential Section Pricing Engine

Hotel Yashdeep features 6 seating areas (`Family`, `AC`, `Hall`, `Restaurant`, `Garden`, `Parcel`). The menu catalog (`item`) defines tier pricing columns.

### 3.1 Dynamic Price Resolution Matrix

```
       Table Selection (e.g. Table T-4 in AC Section)
                            │
                            ▼
           [ Fetch Table Section: SectionType.Ac ]
                            │
                            ▼
           [ Query Item Master for SKU Rate Tier ]
                            │
        ┌───────────────────┼───────────────────┐
        │                   │                   │
        ▼                   ▼                   ▼
If Section == Family   If Section == AC    If Section == Hall
 Rate = FAMILYRATE     Rate = ACRATE       Rate = salerate
```

| Section | Target Database Price Field | Description |
| :--- | :--- | :--- |
| **Hall / Restaurant** | `salerate` | Base standard dining menu rate. |
| **Family Room** | `FAMILYRATE` | Family hall rate (typically premium or packaged). |
| **AC Hall** | `ACRATE` | Air-conditioned section rate (reflects AC operational surcharge). |
| **VIP Lounge** | `VIPRATE` | Executive/VIP lounge pricing tier. |
| **Parcel Counter** | `WHOLESALE` | Takeaway / Delivery packaging rate. |

---

## 4. Taxes & Discounts Engine

### 4.1 Split Tax Architecture (Food GST vs Liquor Excise VAT)

Under Indian tax regulations and Maharashtra State Excise rules:
1. **Food & Non-Alcoholic Beverages**:
   - Subject to GST: **CGST @ 2.5%** + **SGST @ 2.5%** = **5.0% Total GST**.
   - Tax is calculated on `FoodSubTotal` after proportional discount deduction.
2. **Liquor (FL-III License)**:
   - Exempt from GST; governed by Maharashtra Value Added Tax (MVAT) or Excise-inclusive pricing.
   - Price on menu is tax-inclusive by default.

### 4.2 Discount Execution & Role Authority Limits

Discounts can be applied at the line item level or bill summary level. To prevent unauthorized revenue leakage, discounts are enforced via Role-Based Access Control (RBAC):

```
                        [ Discount Request ]
                                │
                                ▼
                   Is Discount > User Max Limit?
                                │
                 ┌──────────────┴──────────────┐
                 │ YES                         │ NO
                 ▼                             ▼
   [ Prompt Manager Override ]        [ Apply Discount ]
   ├── Master Key / Password          └── Write Audit Event
   └── Authenticate Supervisor
```

| User Role | Maximum Bill Discount (%) | Maximum Fixed Discount (₹) | Approval Requirement |
| :--- | :--- | :--- | :--- |
| **Cashier / Waiter** | 0% - 5% | ₹ 100 | Self-approval |
| **Senior Cashier** | 5.1% - 15% | ₹ 500 | Supervisor PIN |
| **Restaurant Manager**| 15.1% - 30% | ₹ 2,000 | Manager Auth |
| **Hotel Owner / Admin**| Up to 100% | Unlimited | Admin Master Key (`dateLckMaster`) |

---

## 5. Payments, Dynamic UPI & Split Tenders

### 5.1 Multi-Tender Split Payment Workflow
Guests frequently pay using combined methods (e.g., ₹500 Cash + ₹750 UPI). The modern engine processes multiple tender payments against a single `BillAggregate`.

```csharp
public class PaymentTransaction
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid BillId { get; init; }
    public PaymentMethod Method { get; init; }
    public Money Amount { get; init; }
    public string TransactionReference { get; init; } = string.Empty; // UPI RRN / Card Auth Code
    public DateTime ProcessedAtUtc { get; init; } = DateTime.UtcNow;
    public string CashierOperator { get; init; } = string.Empty;
}
```

### 5.2 Dynamic UPI QR URI Generation
In accordance with NPCI (National Payments Corporation of India) standards, dynamic UPI QR codes embed the exact net payable amount and bill reference directly into the ESC/POS print stream.

```
Dynamic UPI URI String:
upi://pay?pa={upi_id}&pn={HotelName}&tr={BillId}&am={NetPayable}&cu=INR&mc=5812
Example:
upi://pay?pa=dinu@upi&pn=HOTEL%20YASHDEEP&tr=INV-2025-08921&am=1250.00&cu=INR&mc=5812
```

---

## 6. Sequential Bill Numbering & Offline Pre-Allocation

To comply with GST regulations and prevent sequence collisions across offline POS terminals, the system uses a **Dual-Sequence Strategy**:

```
                       Dual-Sequence Bill Numbering
                                    │
         ┌──────────────────────────┴──────────────────────────┐
         ▼                                                     ▼
[ Global Tax Invoice Number ]                      [ Daily Counter Number ]
Format: `INV-{BranchCode}-{YYYYMM}-{Seq}`          Format: `{DailySeq}` (Resets to 1
Example: `INV-YASH-202503-00412`                    at Day End / Business Closing)
```

### 6.1 Collision-Free Offline Block Allocation

When POS terminals operate offline:
- Each POS counter terminal requests a block of 1,000 sequence numbers from the central server when online (e.g., POS-1 gets `10000-10999`, POS-2 gets `20000-20999`).
- Local transactions assign numbers sequentially within their allocated block.
- Upon reconnection, outbox sync merges sequence blocks seamlessly into PostgreSQL without duplicates or sequence gaps.

---

## 7. Cancellation, Voiding & Refund Architecture

### 7.1 Bill Cancellation vs KOT Cancellation

```
                  ┌─────────────────────────────────────┐
                  │      Cancellation Request          │
                  └──────────────────┬──────────────────┘
                                     │
                    Is Bill Settled / Closed?
                                     │
                   ┌─────────────────┴─────────────────┐
                   │ YES                               │ NO
                   ▼                                   ▼
      [ Post-Settlement Refund ]              [ Pre-Bill Cancellation ]
      ├── Manager Override Required           ├── Void KOT Lines
      ├── Reverses Ledger Vouchers            ├── Record in `CancelKot`
      └── Restores/Depletes Stock             └── Free Table Status
```

### 7.2 Reason Coding & Reversal Accounting
All cancellations require mandatory reason selection:
1. `Guest Non-Payment / Walkout`
2. `Wrong Table / Order Entry`
3. `Food Quality Complaint`
4. `Duplicate KOT Entry`
5. `System Operator Error`

When a bill is cancelled or refunded, the engine creates counter-reversal journal entries in `AccountHead` and logs a immutable audit event.

---

## 8. Immutable Audit Log & Day End Integration

### 8.1 Append-Only Audit Stream

```csharp
public class BillingAuditEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid BillId { get; init; }
    public string EventType { get; init; } = string.Empty; // Created, LineAdded, DiscountApplied, Printed, Cancelled
    public string OperatorName { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}
```

### 8.2 Modern Day End Settlement
Instead of legacy table truncation (`BILLFINAL` -> `BILLFINAL_Dayend`):
1. **Closing Validation**: Verifies all active tables are settled or explicitly closed.
2. **Business Date Advancement**: Advances `DAYEND.DATE` to the next operational date.
3. **Daily Counter Reset**: Resets `DailySequenceNumber` to 1 for the new business date.
4. **Automated Summary Dispatch**: Generates QuestPDF sales summary and dispatches email/SMS alerts to management (`Fahadsayyed92@gmail.com`).
