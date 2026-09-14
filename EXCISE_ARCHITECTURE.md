# Maharashtra State Excise (FL-III) Compliance Architecture

## 1. Executive Summary & Legal Framework

This document details the architectural specifications for the **Maharashtra State Excise Compliance Subsystem** of the Yashdeep Hotel Management System (RSS Modernization).

Venues serving alcoholic beverages in Maharashtra operate under strict statutory regulations mandated by the **Maharashtra State Excise Department** and governed by the **Bombay Prohibition Act, 1949**. Hotel Yashdeep holds an **FL-III License** (`FL III-2151444022D8ADF7`), authorizing the sale and consumption of Foreign Liquor (IMFL), Country Liquor, Beer, and Wine on hotel premises to licensed permit holders.

Under state law, statutory registers and daily statements must maintain 100% mathematical accuracy against physical stock and sales. Any discrepancy between physical stock and register balances constitutes a punishable regulatory offense under Section 65 & 108 of the Bombay Prohibition Act.

---

## 2. FL-III License Setup & Store Configuration

### 2.1 License Master Configuration (`HotelInfo` & `ExStoreInfo`)
The venue's excise credentials and legal parameters are configured at the tenant level.

| Field Name | Legacy Source | Modernization Purpose | Example Value |
| :--- | :--- | :--- | :--- |
| `licno` | `HotelInfo.licno` | Primary State Excise License Number | `FL III-2151444022D8ADF7` |
| `RuleUsed` | `HotelInfo.RuleUsed` | Legal Register Heading | `Register of sales of foreign liquor in units...` |
| `RuleHeading` | `HotelInfo.RuleHeading` | Statutory Form Title | `FORM F.L.R. 1 / REGISTER 1` |
| `PerHldId` | `ExStoreInfo.PerHldId` | Licensee Entity ID | `FL3-YASHDEEP-001` |
| `MlLimit` | `ExStoreInfo.MlLimit` | Maximum Individual Dispense (ML) | `1800 ML` |
| `QtyLimit` | `ExStoreInfo.QtyLimit` | Maximum Individual Possession (Units) | `2 Quarts / 4 Nips` |
| `exyear` | `ExStoreInfo.exyear` | Active Excise Financial Year | `2024-2025` |
| `Vattin` | `HotelInfo.Vattin` | Commercial Tax VAT / GST TIN | `27900111779v` (State 27 = Maharashtra) |

---

## 3. Customer Liquor Permit Management (`ExPremiteHolder`)

Under Maharashtra State Excise regulations, liquor cannot be served to a patron without verifying their valid liquor permit.

### 3.1 Permit Categories
1. **Daily Permit (Pass)**: Single-day temporary permit purchased by guest at the hotel.
2. **Annual / LLD Permit**: State-issued long-term liquor permit issued to individuals.

### 3.2 Permit Holder Master (`ExPremiteHolder`)

| Column Name | Type | Description |
| :--- | :--- | :--- |
| `code` | INTEGER | Permit holder internal reference ID |
| `name` | TEXT | Guest full legal name as per ID proof |
| `licence` | INTEGER / TEXT | Official Liquor Permit Number |
| `validity` | TEXT | Permit validity type (`Daily`, `Annual`, `LLD`, `Lifetime`) |
| `phone` | TEXT | Contact mobile number |
| `Address` | TEXT | Residential address |
| `Issued` | TIMESTAMP | Permit date of issue |
| `Expiry` | TIMESTAMP | Permit expiration date |

### 3.3 POS Order Verification Rule
When a liquor item (BOT) is added to a table in `FRMENTRY`:
1. The POS checks if an active permit is attached to the table/order.
2. If no permit exists, the cashier can issue an instant **Daily Permit Pass** recorded in `ExPremiteHolder`.
3. The permit reference is linked to `BILLFINAL.CUST_NAME` and `ExFinalBill`.

---

## 4. Daily Bulk Litre Statement (`FrmDailyBulkLitre`)

State Excise Inspectors audit venue sales based on **Bulk Litres (BL)** and **Proof Litres (PL)** dispensed per liquor category during every operational day.

### 4.1 Volume Unit Conversion Rules

$$\text{Bulk Litres (BL)} = \frac{\text{Quantity Sold} \times \text{Bottle Unit (ML)}}{1000}$$

#### Category Classification & Proof Factors

| Excise Liquor Category | Example SKUs | Standard Strength (% ABV) | Conversion ML $\rightarrow$ BL |
| :--- | :--- | :---: | :---: |
| **IMFL Spirit** | Whisky, Rum, Vodka, Gin, Brandy | 42.8% v/v (75° Proof) | $\text{Bottles} \times \text{Unit} / 1000$ |
| **Country Liquor (CL)**| Plain / Flavored Spirits | 35.0% v/v | $\text{Bottles} \times \text{Unit} / 1000$ |
| **Strong Beer** | Haywards 5000, Kingfisher Strong | 6.0% - 8.0% v/v | $\text{Bottles} \times 650 / 1000$ |
| **Mild Beer** | Kingfisher Premium, Tuborg Mild | < 5.0% v/v | $\text{Bottles} \times 650 / 1000$ |
| **Wine / Cider** | Port Wine, Sula Sauvignon | 10.0% - 14.0% v/v | $\text{Bottles} \times 750 / 1000$ |

### 4.2 Daily Bulk Litre Aggregation Formula
At the end of each operational day (`FrmDailyBulkLitre`), the system aggregates volume dispensed:

$$\text{Total IMFL BL} = \sum_{\text{IMFL}} \left( (\text{Sealed Sold} \times \text{Unit}) + \text{Loose ML Sold} \right) \div 1000$$
$$\text{Total Beer BL} = \sum_{\text{Beer}} \left( \text{Bottles Sold} \times \text{Bottle Unit ML} \right) \div 1000$$
$$\text{Total Wine BL} = \sum_{\text{Wine}} \left( \text{Bottles Sold} \times \text{Bottle Unit ML} \right) \div 1000$$

---

## 5. Transport Permits (TP) & Inward Bottle Movement Tracking

All liquor brought onto hotel premises must be covered by a valid **Transport Permit (TP)** issued by the district Excise Superintendent.

```
┌────────────────────────────────────────────────────────────────────────┐
│                   DISTRICT EXCISE SUPERINTENDENT                       │
│             Issues Transport Permit (TP No, e.g. "TP-2024-8841")        │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ Inward Shipment Arrives
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                      INWARD ENTRY (`LiqPurchase`)                      │
│  - Verifies TP Number & TP Date (`tpno`, `tpdate`, `tpDate2`)           │
│  - Records Batch Numbers (`batchno`) & Manufacturing Date              │
│  - Maps SKU, Bottle Volume, and Cases Received                         │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ Automated Statutory Post
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│              EXCISE REGISTER 1 (`ExciseMonthlyStat`)                   │
│  - Increments `RECEIVEDMONTH` and `RECEIVED1APR`                       │
│  - Links batch stock to licensed distributor vendor (`vndnm`)          │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 6. Prescribed Excise Registers & Monthly Statements

### 6.1 Register 1 / Sales Register (`ExciseMonthlyStat`)
Register 1 (Form F.L.R. 1) is the mandatory statutory register of foreign liquor sales.

| Column Name | Mathematical Definition |
| :--- | :--- |
| `OPENING` | Start-of-day opening balance in whole bottles |
| `Purchase` | Inward bottles received under Transport Permits today |
| `SALE` | Total bottles sold today (Sealed + Loose equivalent) |
| `BREAKAGE` | Bottles written off due to breakage / spillage |
| `CLOSING` | Net end-of-day balance: $\text{OPENING} + \text{Purchase} - \text{SALE} - \text{BREAKAGE}$ |
| `OPENINGMONTH` | Opening stock balance on the 1st day of the active month |
| `RECEIVEDMONTH` | Cumulative Transport Permit receipts during current month |
| `RECEIVED1APR` | Cumulative receipts since the start of the financial year (1st April) |
| `SELLMONTH` | Cumulative sales during current month |
| `SELL1APR` | Cumulative sales since 1st April |
| `CLOSINGMONTH` | Closing stock balance reported at month end |

### 6.2 Financial Year Baseline (1st April Rule)
State excise reporting tracks cumulative throughput starting **April 1st** (the Indian financial year start).
- On April 1st 00:00, `RECEIVED1APR` and `SELL1APR` reset to 0.
- Daily transactions accumulate into these counters throughout the 365-day cycle to provide the Excise Inspector with instant year-to-date compliance figures.

### 6.3 Automated Closing Snapshot (`ExciseClosingAutoSale`)
To prevent manual tampering, the system executes an automated snapshot job during Day End:
- Copies live counter sales and computes `ClosingCurrent`.
- Compares computed closing against physical stock to highlight unauthorized stock variances.

---

## 7. Excise Pass / Permit Billing (`ExFinalBill` & `ExFinalBillDetails`)

When required by excise inspectors, customer invoices for liquor must be exported into a dedicated **Excise Pass Register** separate from general restaurant food bills.

### 7.1 Excise Bill Header (`ExFinalBill`)
- `srno`, `invoice`: Sequential excise invoice serial number
- `date`: Invoice date and time
- `name`: Permit holder guest name
- `license`: Guest permit number
- `validity`: Permit validity type
- `amt`: Total excise liquor billing amount

### 7.2 Excise Line Breakdown (`ExFinalBillDetails`)
- `item`, `qty`, `rate`, `amt`
- `type`: IMFL / Beer / Wine
- `unit`: Bottle size in ML (750, 375, 180, 650)
- `packing`, `brand`: Brand details

---

## 8. Dry Days Enforcement & Legal Unit Limits

### 8.1 Dry Days Prohibition Engine (`DryDates`)
The `DryDates` table stores dates on which alcohol sale is legally prohibited (e.g., Gandhi Jayanti, Republic Day, Election Days).

```
[ POS Order Entry (`FRMENTRY`) ]
               │
               ▼
   [ Check Today's Date against `DryDates` ]
               │
               ├── IF Date IS IN `DryDates`:
               │      Block all Bar KOT/BOT creation
               │      Display Error: "LEGAL DRY DAY: Alcohol sales prohibited under State Excise rules."
               │
               └── IF Date IS NOT IN `DryDates`:
                      Allow normal order processing
```

### 8.2 Individual Legal Possession Limits (`ExUnitLimit`)
`ExUnitLimit` defines the legal maximum quantity a single permit holder may purchase or transport at one time under Maharashtra Excise rules:

| Unit Size (ML) | Maximum Allowed Limit per Permit |
| :--- | :--- |
| **750 ML (Quart)** | 2 Bottles |
| **375 ML (Pint)** | 4 Bottles |
| **180 ML (Nip)** | 8 Bottles |
| **650 ML (Beer)** | 6 Bottles |

If an order exceeds these thresholds for a single customer bill, POS triggers a **Split Permit Prompt**, requiring an additional valid permit number for the excess volume.

---

## 9. Day End Integration & Excise Settlement

### 9.1 Day End Excise Settlement Flow
During the mandatory Day End procedure (`FrmDtpDayend` / `frmExDayend`):

1. **Reconcile Loose Dispensing to Bottles**:
   - Total loose ML sold is converted to equivalent whole bottles ($\text{Loose ML} / \text{Unit}$).
2. **Post to `ExciseMonthlyStat`**:
   - Increments `SELLMONTH` and `SELL1APR`.
3. **Lock Historical Records (`dateLckMaster` / `datelock`)**:
   - Locks past date records. Editing a past excise entry requires entering the dynamic **Master Date Lock Password** (`333` / master key).

---

## 10. QuestPDF Compliance Reports & Statutory Formats

The modernized system uses **QuestPDF** to render pixel-perfect statutory reports matching prescribed Excise Department layouts:

1. **Daily Bulk Litre Statement (Form FLR-3)**: Itemized daily volume breakdown in Bulk Litres per category.
2. **Register 1 / Sales Register (Form FLR-1)**: Landscape A4 statutory monthly log showing Opening, TP Receipts, Daily Sales, Breakage, and Closing balance per brand.
3. **Permit Holder Register**: Summary of all liquor permits issued and recorded during the month.

---

## 11. Offline Compliance Integrity & Synchronization Conflict Resolution

### 11.1 Offline Edge Compliance Engine
POS terminals operating offline in local SQLite retain full compliance rules:
- **Offline Permit Capture**: Local creation and validation of permit passes.
- **Offline Bulk Litre Accumulation**: Local calculation of BL volume in SQLite.

### 11.2 Conflict Resolution & Non-Repudiation
1. **Immutable Historical Sequence**: Excise returns are immutable once Day End is finalized.
2. **Server-Side Validation**: When offline outbox batches sync to PostgreSQL, the server validates TP numbers and permit serials against existing records to prevent duplicate permit numbers or overlapping TP sequences.

---

## 12. Regulatory Audit Trail & Anti-Tampering Security

To satisfy Excise Department audits:
- **HMAC-SHA256 Hash Chaining**: Each entry in `ExciseMonthlyStat` generates a cryptographic hash based on the previous record's hash + current line data.
- **Anti-Tamper Warning**: If an unauthorized SQL edit alters a historical record in PostgreSQL, the hash chain breaks, flagging the database during audit inspection.

---

## 13. Modernized C# .NET 9 Clean Architecture Domain Entities

```csharp
namespace YashdeepHotelMS.Domain.Entities.Excise;

public class ExciseLicenseConfig : BaseAuditableEntity
{
    public Guid TenantId { get; set; }
    public string LicenseNumber { get; set; } = string.Empty; // FL III-2151444022D8ADF7
    public string TradeName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string VatTin { get; set; } = string.Empty;
    public string ActiveExciseYear { get; set; } = "2024-2025";
}

public class CustomerPermitHolder : BaseAuditableEntity
{
    public Guid TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PermitNumber { get; set; } = string.Empty;
    public string PermitType { get; set; } = "Daily"; // Daily, Annual, LLD
    public string Mobile { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public DateTime IssuedDate { get; set; }
    public DateTime ExpiryDate { get; set; }
}

public class ExciseRegister1Record : BaseAuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid InventoryItemId { get; set; }
    public DateTime RecordDate { get; set; }

    public int OpeningBottles { get; set; }
    public int ReceivedTransportPermitBottles { get; set; }
    public int SoldBottles { get; set; }
    public int BreakageBottles { get; set; }
    public int ClosingBottles => OpeningBottles + ReceivedTransportPermitBottles - SoldBottles - BreakageBottles;

    // Monthly & Financial Year Accumulators
    public int OpeningMonthBottles { get; set; }
    public int ReceivedMonthBottles { get; set; }
    public int Received1stAprilBottles { get; set; }
    public int SoldMonthBottles { get; set; }
    public int Sold1stAprilBottles { get; set; }

    public string HashSignature { get; set; } = string.Empty; // HMAC-SHA256 chain
}

public class ExciseDailyBulkLitreSummary : BaseAuditableEntity
{
    public Guid TenantId { get; set; }
    public DateTime SummaryDate { get; set; }

    public decimal ImflBulkLitres { get; set; }
    public decimal CountryLiquorBulkLitres { get; set; }
    public decimal BeerBulkLitres { get; set; }
    public decimal WineBulkLitres { get; set; }

    public decimal TotalBulkLitres => ImflBulkLitres + CountryLiquorBulkLitres + BeerBulkLitres + WineBulkLitres;
}
```
