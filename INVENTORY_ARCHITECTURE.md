# Inventory Architecture & Stock Management Specifications

## 1. Executive Summary & Domain Overview

This document provides the definitive architectural blueprint for the **Inventory and Stock Management Subsystem** of the Yashdeep Hotel Management System (RSS Modernization). In hospitality operations—specifically hotel-restaurant-bar venues operating under Maharashtra State Excise FL-III rules—liquor and beverage inventory cannot be treated as a simple, generic warehouse stock item.

Liquor stock operates across a **three-tier physical hierarchy** (Godown Bulk Warehouse $\rightarrow$ Counter Sealed Bottle Stock $\rightarrow$ Loose Dispensary Volume in ML), subjected to strict physical state transformations (Case $\rightarrow$ Bottle $\rightarrow$ Peg), precise unit conversions (30ml, 60ml, 90ml, 180ml Nip, 375ml Pint, 750ml Quart, 1000ml), automated bottle-opening triggers, spillage/breakage tracking, offline local execution, outbox synchronization, and strict regulatory compliance.

---

## 2. Multi-Tier Physical Inventory Topology

```
┌────────────────────────────────────────────────────────────────────────┐
│                   TIER 1: GODOWN (Central Warehouse)                  │
│                   Tracked in `GodownStock` / `GodownItem`               │
├────────────────────────────────────────────────────────────────────────┤
│  - Receives bulk shipments (Cases & Sealed Bottles) from Vendors.       │
│  - Recorded via Transport Permit (TP) Numbers & Invoices.              │
│  - Managed in Cases and Whole Bottles (`OPENING`, `Purchase`, `CLOSING`).│
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ Stock Issue / Transfer
                                    │ (`CntRecieved_N` / `StockTransfer`)
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│             TIER 2: COUNTER STOCK (Bar Dispensary - Sealed)            │
│              Tracked in `CNTPACK_LIVE` / `CounterStock`                │
├────────────────────────────────────────────────────────────────────────┤
│  - Sealed bottles stored behind active bar counter for fast serving.   │
│  - Incremented by Godown Receipts (`RECIEVED`).                        │
│  - Decremented by whole bottle direct sales (`SALE`) or by opening     │
│    bottles for loose peg dispensing (`SALELOOSE`).                     │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ Bottle Opened for Peg Service
                                    │ (Triggered automatically or manually)
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│              TIER 3: LOOSE DISPENSARY (Open Bottle Volume)             │
│              Tracked in `CNTLOOSE_LIVE` / `LooseDispensary`            │
├────────────────────────────────────────────────────────────────────────┤
│  - Tracked strictly in Millilitres (ML) (`TOTALML`).                    │
│  - Serves individual pegs (30ml, 60ml, 90ml, 180ml).                    │
│  - Auto-replenished from Tier 2 when active open bottle volume          │
│    (`Opnbotlqty`) is depleted to 0 ML.                                 │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Godown Subsystem (Central Warehouse)

### 3.1 Responsibilities & Operations
The **Godown** acts as the central bulk storage facility. All commercial inward purchases from licensed liquor distributors are initially received and stored in the Godown.

- **Primary Units**: Cases, Boxes, and Sealed Bottles.
- **Legacy Mappings**: `GodownStock` table.
- **Key Fields**: `OPENING`, `OpenTemp`, `Purchase`, `Recieved` (Transfers Issued out to Counter), `CLOSING`, `TOTALPACK`, `salerate`.

### 3.2 Opening, Inward & Closing Balance Calculations

$$\text{Godown.CLOSING} = \text{Godown.OPENING} + \text{Godown.Purchase} - \text{Godown.Recieved} \pm \text{Adjustments}$$

- **`OPENING`**: Carried forward from previous day's `CLOSING` during Day End.
- **`Purchase`**: Cumulative inward stock received from vendor bills during the operational day.
- **`Recieved`**: Cumulative sealed bottles issued/transferred from Godown to active Counter(s).
- **`CLOSING`**: Net available stock balance in whole bottles.

---

## 4. Procurement, Purchase & Inward Receipt Workflow

### 4.1 Purchase Structure (`LiqPurchase` & `LiqPurchaseFinal`)
Procurement of foreign liquor, beer, and wine requires recording both itemized SKU lines (`LiqPurchase`) and vendor header details with tax breakdowns (`LiqPurchaseFinal`).

#### Inward Purchase Header (`LiqPurchaseFinal` / `PurchaseInvoice`)
- **Vendor & License Details**: Vendor name (`name`), Address, License No.
- **Transport Permit (`tpno`, `tpdate`, `tpDate2`)**: Official state excise permit authorizing transport.
- **Invoice & Financials**: Bill No (`billno`), Bill Date (`billdate`), Tax Amount (`taxamt`), Invoice Amount (`invamt`), Pay Type (`paytype` - Cash/Credit).
- **Duties & Levies Overhead Breakdown**:
  - `stampduty`: State stamp duty fee
  - `cd`: Custom / Countervailing duty
  - `scheme`: Promotional scheme deductions
  - `stocktran`: Stock transfer levies
  - `carting`: Transportation / freight charges
  - `saleta`: Sales tax / VAT / GST component
  - `cs` / `sc`: Cess / Surcharge
  - `edu`: Education cess

#### Itemized Line Items (`LiqPurchase`)
- SKU Details: Item name (`item`), Brand (`brand`), Type (`type` - IMFL/Beer/Wine/CL), Packing (`packing`), Bottle Unit Volume (`unit` in ML), Batch Number (`batchno`).
- Quantities & Rates: Cases purchased (`qty`), Total Bottles (`bottle`), Unit Purchase Rate (`rate`), Line Total Amount (`amt`).

### 4.2 Inward Purchase Logic & Stock Update Rule

When a purchase invoice is saved and verified:
1. **Case-to-Bottle Conversion**:
   $$\text{Total Bottles Added} = (\text{Cases} \times \text{BtPerCase.CaseBott}) + \text{Loose Bottles Purchased}$$
2. **Godown Increment**:
   $$\text{GodownStock.Purchase} \leftarrow \text{GodownStock.Purchase} + \text{Total Bottles Added}$$
   $$\text{GodownStock.CLOSING} \leftarrow \text{GodownStock.CLOSING} + \text{Total Bottles Added}$$
3. **Excise Entry**: Automatically queues records to `ExciseOpening` / `ExciseMonthlyStat` under the corresponding Transport Permit (`tpno`).

---

## 5. Stock Transfer Mechanics (Godown $\rightarrow$ Counter)

### 5.1 Transfer Protocol & Transaction Boundaries
Transferring stock from Godown to Counter moves sealed bottles from Tier 1 bulk storage to Tier 2 active dispensing stock.

```
[ Transfer Request Initiated at POS/Admin ]
              │
              ├── 1. Validate: Transfer Qty <= GodownStock.CLOSING
              │
              ├── 2. Atomically Decrement Godown:
              │      GodownStock.Recieved = GodownStock.Recieved + Qty
              │      GodownStock.CLOSING  = GodownStock.CLOSING - Qty
              │
              ├── 3. Atomically Increment Counter:
              │      CNTPACK_LIVE.RECIEVED = CNTPACK_LIVE.RECIEVED + Qty
              │      CNTPACK_LIVE.CLOSING  = CNTPACK_LIVE.CLOSING + Qty
              │
              └── 4. Audit Log Entry:
                     Record in `CntRecieved_N` (Code, Item, Received, Date, ClientId, RefNo)
```

### 5.2 Transfer Verification (`CntRecieved_N`)
The legacy system logs transfers in `CntRecieved_N`:
- `code`, `item`, `unit`, `packing`, `salerate`
- `Received`: Quantity of sealed bottles transferred
- `DATE`, `CLIENTID`: Terminal ID where transfer was executed
- `RefNo`: Sequential transfer reference voucher number

---

## 6. Counter Stock & Sealed Bottle Management

### 6.1 Counter Live Balance Model (`CNTPACK_LIVE`)
`CNTPACK_LIVE` tracks sealed bottle balances behind the active bar counter.

| Legacy Field | Description | Formula / Rule |
| :--- | :--- | :--- |
| `OPENING` | Opening sealed bottles at start of day | Rolled over from previous day `CLOSING` |
| `RECIEVED` | Stock received from Godown today | Sum of transfers from Godown |
| `SALE` | Whole sealed bottles sold directly | Incremented on full bottle bill lines |
| `SALELOOSE` | Whole bottles opened for loose dispensing | Incremented when bottle is opened for pegs |
| `CLOSING` | Remaining sealed bottles balance | $\text{OPENING} + \text{RECIEVED} - \text{SALE} - \text{SALELOOSE}$ |
| `Opnbotlqty` | Remaining volume (ML) in currently active open bottle | Range: $0 \le \text{Opnbotlqty} \le \text{Unit}$ |
| `AddAdjml` | Positive adjustment in ML (surplus/correction) | Increments loose volume |
| `MinusAdjml` | Negative adjustment in ML (spillage/breakage) | Decrements loose volume |

---

## 7. Bottle Stock, Packaging Units & Conversion Matrix

Liquor is packaged in standardized bottle sizes. The system maintains strict conversion matrices (`BtPerCase` & `BRANDML`).

### 7.1 Standard Bottle Sizes & Case Factors (`BtPerCase`)

| Bottle Unit (ML) | Standard Packaging Name | Bottles per Case (`CaseBott`) | Total Case Volume (L) |
| :--- | :--- | :---: | :---: |
| **1000 ML** | 1 Litre Bottle | 12 | 12.0 L |
| **750 ML** | Quart (Full Bottle) | 12 | 9.0 L |
| **375 ML** | Pint (Half Bottle) | 24 | 9.0 L |
| **180 ML** | Nip (Quarter Bottle) | 48 | 8.64 L |
| **90 ML** | Sample / Mini | 96 | 8.64 L |
| **650 ML** | Standard Beer Bottle | 12 | 7.8 L |
| **500 ML** | Can / Pint Beer | 24 | 12.0 L |
| **330 ML** | Small Beer Bottle / Can | 24 | 7.92 L |

---

## 8. Loose Stock & Peg Dispensing Engine

### 8.1 Loose Dispensary Model (`CNTLOOSE_LIVE`)
When a bottle is opened for peg service, its liquid volume is tracked in `CNTLOOSE_LIVE`:
- `code`, `item`, `brand`, `unit`, `packing`
- `TOTALML`: Total accumulated volume in millilitres available across all open bottles of this SKU.

### 8.2 Standard Dispensing Peg Units (`ADJUSTMENT`)
The `ADJUSTMENT` lookup table maps order unit selections to exact ML subtractions:

| Unit Code | Dispensing Name | Liquid Volume (ML) |
| :--- | :--- | :---: |
| **30** | Small Peg | 30 ML |
| **60** | Large Peg | 60 ML |
| **90** | Patiala / Extra Large Peg | 90 ML |
| **180** | Nip / Quarter Dispense | 180 ML |
| **375** | Pint Dispense | 375 ML |
| **750** | Quart Dispense | 750 ML |

### 8.3 Dispensing Order Execution Logic

```
[ Waiter places BOT/Order for 1x 60ml Peg of Whisky ]
                           │
                           ▼
             [ Check Active Counter Stock ]
                           │
  ├── 1. Check Opnbotlqty in CNTPACK_LIVE for SKU
  │
  ├── 2. IF Opnbotlqty >= Requested ML (e.g., 60 ML):
  │        Deduct directly: Opnbotlqty = Opnbotlqty - 60
  │        Deduct CNTLOOSE_LIVE.TOTALML = CNTLOOSE_LIVE.TOTALML - 60
  │
  └── 3. IF Opnbotlqty < Requested ML (e.g., 20 ML remaining, 60 ML needed):
           a. Trigger Bottle Opening Protocol (See Section 9)
           b. Open 1 Sealed Bottle from CNTPACK_LIVE.CLOSING
           c. Opnbotlqty = Opnbotlqty + Bottle.Unit (e.g., 20 + 750 = 770 ML)
           d. Deduct Requested ML: Opnbotlqty = 770 - 60 = 710 ML
           e. Update CNTLOOSE_LIVE.TOTALML
```

---

## 9. Bottle Opening Protocol & Auto-Opening Mechanism

### 9.1 Rules of Bottle Opening
Under Maharashtra Excise guidelines, a sealed bottle moves from "Sealed Pack" to "Loose Dispensary" the moment its seal is broken.

1. **Sealed Stock Decrement**:
   $$\text{CNTPACK\_LIVE.SALELOOSE} \leftarrow \text{CNTPACK\_LIVE.SALELOOSE} + 1$$
   $$\text{CNTPACK\_LIVE.CLOSING} \leftarrow \text{CNTPACK\_LIVE.CLOSING} - 1$$
2. **Open Bottle Volume Increment**:
   $$\text{CNTPACK\_LIVE.Opnbotlqty} \leftarrow \text{CNTPACK\_LIVE.Opnbotlqty} + \text{Bottle.Unit}$$
   $$\text{CNTLOOSE\_LIVE.TOTALML} \leftarrow \text{CNTLOOSE\_LIVE.TOTALML} + \text{Bottle.Unit}$$
3. **Validation Constraint**: A bottle cannot be opened if $\text{CNTPACK\_LIVE.CLOSING} \le 0$. If attempted, POS raises a `StockDepletedException` ("Insufficient sealed counter stock to open bottle").

---

## 10. Consumption, Wastage, Spillage & Stock Adjustments

### 10.1 Spillage & Breakage Management
In high-volume bar operations, liquid loss occurs due to bottle breakage, over-pouring, spillage, or pipe flushing in draught systems.

- **`MinusAdjml`**: Records liquid volume lost in ML (e.g., 750ml broken bottle or 30ml spillage).
- **`AddAdjml`**: Records positive liquid adjustments in ML (e.g., physical audit surplus correction).

### 10.2 Net Available Stock Formula with Adjustments

$$\text{Net Loose ML} = \text{CNTLOOSE\_LIVE.TOTALML} + \text{CNTPACK\_LIVE.AddAdjml} - \text{CNTPACK\_LIVE.MinusAdjml}$$

### 10.3 Wastage Accounting
Every wastage/breakage record must capture:
1. `SKUId`, `Brand`, `VolumeML`
2. `ReasonCode` (Breakage, Spillage, Expiry, Inspection Sample)
3. `AuthorizedBy` (Manager User ID)
4. `Timestamp` & `ShiftId`

---

## 11. Stock Reconciliation & Physical Inventory Audit

### 11.1 Shift & Daily Reconciliation Workflow
Physical stock auditing is conducted by counting whole sealed bottles and measuring remaining liquid levels in open bottles.

```
                  [ Audit Triggered ]
                           │
    ┌──────────────────────┴──────────────────────┐
    ▼                                             ▼
[ Physical Count Sealed Bottles ]     [ Dipstick / Scale Open Bottles ]
 (Count whole bottles on shelf)        (Measure remaining ML in open bottles)
    │                                             │
    └──────────────────────┬──────────────────────┘
                           ▼
              [ Calculate System Variance ]
  ├── Variance Bottles = Physical Sealed - System CNTPACK_LIVE.CLOSING
  └── Variance ML      = Physical Loose ML - System Opnbotlqty
                           │
                           ▼
         [ Manager Review & Adjustment Approval ]
  ├── If Variance < Threshold: Auto-apply AddAdjml / MinusAdjml
  └── If Variance >= Threshold: Require Mandatory Manager PIN & Reason
```

---

## 12. Stock Valuation Methodology

Inventory valuation in the modernized system supports dual valuation models:

### 12.1 Purchase Cost Valuation (FIFO / Weighted Average)
Used for financial balance sheets and tax accounting.

$$\text{SKU Weighted Unit Cost} = \frac{\sum (\text{Batch Qty} \times \text{Batch Purchase Rate})}{\sum \text{Batch Qty}}$$
$$\text{Godown Stock Value} = \text{GodownStock.CLOSING} \times \text{Weighted Unit Cost}$$
$$\text{Counter Stock Value} = \text{CNTPACK\_LIVE.CLOSING} \times \text{Weighted Unit Cost} + \left( \frac{\text{Opnbotlqty}}{\text{Unit}} \times \text{Weighted Unit Cost} \right)$$

### 12.2 Realizable Sales Value
Used for sales forecasting and loss calculation.

$$\text{Sales Valuation} = (\text{Whole Bottles} \times \text{Salerate}) + \left( \frac{\text{Loose ML}}{30} \times \text{Peg 30ml Rate} \right)$$

---

## 13. Day End Interaction & Stock Rollover

### 13.1 Day End Stock Rollover (`FrmDtpDayend`)
During the Day End procedure, live operational stock balances are frozen, archived to history, and reset for the next business date.

```
[ Day End Triggered ]
          │
          ├── 1. Freeze Live Stock: Prevent new KOTs or Stock Transfers
          │
          ├── 2. Historical Snapshot Archival:
          │      INSERT INTO CNTPACK_LIVE_DAYEND SELECT *, CurrentDate FROM CNTPACK_LIVE
          │      INSERT INTO CounterSockDayend SELECT *, CurrentDate FROM CNTPACK_LIVE
          │
          ├── 3. Stock Rollover Execution:
          │      Today's CLOSING becomes Tomorrow's OPENING:
          │      UPDATE CNTPACK_LIVE SET
          │          OPENING = CLOSING,
          │          RECIEVED = 0,
          │          SALE = 0,
          │          SALELOOSE = 0,
          │          AddAdjml = 0,
          │          MinusAdjml = 0,
          │          DATE1 = NextBusinessDate
          │
          └── 4. Reset Godown Counter Balances:
                 GodownStock.OPENING = GodownStock.CLOSING
                 GodownStock.Purchase = 0
                 GodownStock.Recieved = 0
```

---

## 14. Offline Inventory Operations & Synchronization Conflicts

### 14.1 Offline Local First Architecture (SQLite POS Terminals)
Each POS terminal runs an embedded SQLite database containing full local stock tables (`CNTPACK_LIVE`, `CNTLOOSE_LIVE`).

1. **Local Deductions**: Orders and stock deductions execute locally in SQLite within milliseconds without waiting for server response.
2. **Outbox Queueing**: Every stock mutation generates an Outbox event payload (`InventoryDeductedEvent`, `StockTransferredEvent`).

### 14.2 Sync Conflict Resolution Strategy

When multiple offline POS terminals operate simultaneously during network disconnects:

#### Conflict Scenario: Dual Bottle Opening
Terminals POS-1 and POS-2 both have 1 sealed bottle left in system stock offline. Both open a bottle.

#### Resolution Protocol: Additive Delta Merging
Rather than overwriting absolute stock numbers (`CLOSING = X`), the Cloud API processes stock updates as **relative deltas**:

$$\Delta \text{SALE} = \text{Client.SALE} - \text{Client.LastSyncedSALE}$$
$$\Delta \text{SALELOOSE} = \text{Client.SALELOOSE} - \text{Client.LastSyncedSALELOOSE}$$
$$\text{Server.CLOSING} \leftarrow \text{Server.CLOSING} - \Delta \text{SALE} - \Delta \text{SALELOOSE}$$

If Server `CLOSING` drops below 0 due to concurrent offline sales, the server logs a **Negative Stock Audit Warning** (`MINUSSTOCK` flag in `Setup`), allowing operations to continue while alerting management during reconciliation.

---

## 15. Inventory Audit, Traceability & Security

### 15.1 Keypress & Action Tracking (`STK_CNTR_KEYPRESS`)
The legacy system captures user interaction timestamps during counter stock entries in `STK_CNTR_KEYPRESS` (`DATE_TIME_NOW`, `DATE_TIME_ENTER`).

The modernized system expands this into an **Immutable Audit Log Table** (`InventoryAuditLogs`):

| Column | Type | Purpose |
| :--- | :--- | :--- |
| `AuditId` | Guid | Primary Key |
| `TenantId` | Guid | Multi-tenant isolation |
| `Timestamp` | DateTimeOffset | Exact UTC time |
| `UserId` | String | Operator / Manager who performed action |
| `TerminalId` | String | Physical POS terminal name |
| `SKUId` | String | Target inventory item |
| `ActionType` | Enum | Purchase, Transfer, Sale, BottleOpen, Spillage, Adjustment |
| `DeltaQty` | Decimal | Quantity / Volume change (+ / -) |
| `PreviousBalance`| Decimal | Balance before change |
| `NewBalance` | Decimal | Balance after change |
| `HashSignature` | String | HMAC-SHA256 signature for tamper detection |

---

## 16. Modernized C# .NET 9 Clean Architecture Domain Entities

```csharp
namespace YashdeepHotelMS.Domain.Entities.Inventory;

public class InventoryItem : BaseAuditableEntity
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty; // Fast POS shortcode
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty; // Kitchen, Bar
    public string ItemType { get; set; } = string.Empty; // IMFL, Beer, Wine, Food
    public int UnitMl { get; set; } // 750, 375, 180, 650
    public string Packing { get; set; } = string.Empty;

    public decimal SaleRate { get; set; }
    public decimal PurchaseRate { get; set; }
    public decimal ExciseRate { get; set; }

    // Differential Section Pricing
    public decimal FamilyRate { get; set; }
    public decimal VipRate { get; set; }
    public decimal AcRate { get; set; }
    public decimal WholesaleRate { get; set; }

    public string DevanagariName { get; set; } = string.Empty; // Marathi translation
}

public class GodownStock : BaseAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;

    public int OpeningBottles { get; set; }
    public int PurchasedBottles { get; set; }
    public int TransferredOutBottles { get; set; }
    public int ClosingBottles => OpeningBottles + PurchasedBottles - TransferredOutBottles;

    public DateTime SnapshotDate { get; set; }
}

public class CounterStockLive : BaseAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;

    public int OpeningSealedBottles { get; set; }
    public int ReceivedFromGodown { get; set; }
    public int SoldSealedBottles { get; set; }
    public int OpenedForLooseDispensary { get; set; }

    public int ClosingSealedBottles => OpeningSealedBottles + ReceivedFromGodown - SoldSealedBottles - OpenedForLooseDispensary;

    public int ActiveOpenBottleVolumeMl { get; set; } // Remaining ML in current open bottle
    public int AddedAdjustmentMl { get; set; }
    public int SubtractedAdjustmentMl { get; set; }
}

public class PurchaseInvoice : BaseAuditableEntity
{
    public Guid TenantId { get; set; }
    public string TransportPermitNo { get; set; } = string.Empty;
    public DateTime TransportPermitDate { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public Guid VendorId { get; set; }

    // Financial Overheads
    public decimal StampDuty { get; set; }
    public decimal CustomDuty { get; set; }
    public decimal FreightCarting { get; set; }
    public decimal SalesTaxVat { get; set; }
    public decimal TotalAmount { get; set; }

    public List<PurchaseInvoiceItem> Lines { get; set; } = new();
}
```
