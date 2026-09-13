# Legacy-to-SaaS Migration Architecture Specification

**Document Version:** 1.0.0
**Author:** Agent 13 — Migration Architect
**Target System:** Cloud-Synchronized Multi-Tenant SaaS Platform (.NET 9 + PostgreSQL 16 + SQLite POS)
**Source System:** Yashdeep Hotel MS (`RSS26/dinurss.mdb` Jet 4.0 Access Database, Password: `rss1008`)

---

## 1. Executive Summary & Canonical Domain Mapping Blueprint

### 1.1 Scope & Migration Strategy
This document specifies the end-to-end migration architecture for transitioning from the legacy single-tenant desktop Access database (`dinurss.mdb`, 22.5 MB, 105 user tables) used by **Hotel Yashdeep** to the target multi-tenant cloud-synchronized SaaS platform.

The core guiding rule of this migration is: **Do NOT simply copy tables.**

The legacy database represents a procedural, desktop-centric schema designed for Microsoft Access Jet 4.0. It contains flat structures, duplicated tables for daily rollover (`*_Dayend`), floating-point monetary values, missing foreign keys, ASCII/Devanagari font hacks, and unnormalized inventory states.

The target system is an offline-first, domain-driven SaaS platform built on .NET 9, PostgreSQL 16 (cloud), SQLite (edge POS), and EF Core. Migration requires extracting procedural legacy artifacts, cleansing invalid data, resolving legacy encoding, standardizing timestamps and currency, and mapping concepts cleanly into modern Canonical Domain Bounded Contexts.

### 1.2 Data Preservation Guarantee
- **Strict Read-Only Source Access**: The production Access database (`RSS26/dinurss.mdb`) and backup files (`RSS26/dinurss - Copy.mdb`, `RSS26/OLD.mdb`) are strictly protected assets. All extraction routines operate in read-only mode (`ReadOnly=True`). No write, modify, drop, or truncate operations are ever executed against legacy production files.
- **Non-Destructive Staging**: Extraction streams legacy data into an isolated PostgreSQL staging schema (`migration_staging`). All transformations, cleansing, and key mapping occur within staging tables before final bulk loading into target production schema tables.

### 1.3 Canonical Domain Model Blueprint
The 105 legacy tables are mapped into 8 Domain Bounded Contexts within the SaaS canonical model:

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│                             CANONICAL DOMAIN MODEL                               │
├──────────────────────────┬───────────────────────────┬───────────────────────────┤
│ 1. Tenant Context        │ 2. Catalog & Menu Context │ 3. Inventory Context      │
│  - Tenants               │  - CatalogItems           │  - Warehouses             │
│  - TenantSettings        │  - Categories             │  - WarehouseStock         │
│  - FloorSections         │  - Departments            │  - BarCounterStock        │
│  - StaffMembers          │  - PricingTiers           │  - LooseDispensaryStock   │
├──────────────────────────┼───────────────────────────┼───────────────────────────┤
│ 4. Sales & Billing       │ 5. Kitchen Orders (KOT)   │ 6. Excise FL-III Context  │
│  - SalesInvoices         │  - KotHeaders             │  - ExcisePermittees       │
│  - SalesInvoiceItems     │  - KotItems               │  - ExciseLicenseConfigs   │
│  - TaxLedgers            │  - VoidItemLogs           │  - ExciseStockRegisters   │
│  - PaymentTransactions   │  - KitchenPrinterRoutes   │  - ExciseMonthlyReturns   │
├──────────────────────────┴───────────────────────────┴───────────────────────────┤
│ 7. General Ledger Accounting                         8. Security & Identity      │
│  - ChartOfAccounts                                   - ApplicationUsers          │
│  - PettyCashVouchers                                 - UserRoles & Claims        │
│  - CustomerLedgers / VendorLedgers                   - SecurityAuditPolicies     │
└──────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Legacy System Artifact Identification

### 2.1 Legacy Tables Inventory & Domain Grouping
The 105 legacy tables in `dinurss.mdb` are categorized into 9 functional domains, identifying active operational tables, historical rollover tables (`*_Dayend`), temporary staging tables (`*_temp`, `Paste Errors`), and redundant copies:

| Domain | Table Count | Active Operational Tables | Historical / Rollover Tables | Temporary / Staging / Copy Tables |
| :--- | :---: | :--- | :--- | :--- |
| **1. Sales & Billing** | 16 | `BILLFINAL` (3,693), `finalbill` (13,400) | `BILLFINAL_Dayend` (0), `finalbill_Dayend` (0) | `GrandBill` (3,693), `GrandBillDetails` (13,400), `finalbillcopy` (13,209), `GrandBillDetailscopy` (13,209), `BILLKOT` (0), `BILLKOT_TBLSHOW` (0), `BILLREPORT` (0), `dk` (0), `sectionwiseBillfinal_Temp` (133), `sectionwise_temp` (481), `finalbill_Excise` (0), `finalbill_Excise1` (0), `finalbill_LoosAmt` (0) |
| **2. Kitchen Orders (KOT)** | 10 | `KOTFINAL` (10,625), `KOTDETAIL` (19,382), `CancelKot` (1,389) | `KOTFINAL_DAYEND` (2,393), `KOTDETAIL_DAYEND` (3,031) | `KOT` (0), `KOTTEMP` (0), `KOTTOTAL` (1), `CancelKot_Dayend` (0) |
| **3. Menu & Categories** | 6 | `item` (1,607), `ItemDept` (39), `BRANDML` (741), `BtPerCase` (12), `TABLE_NO_GROP` (6) | None | `NewFood` (0) |
| **4. Inventory & Stock** | 17 | `GodownStock` (1,926), `CNTPACK_LIVE` (1,934), `CNTLOOSE_LIVE` (757), `ADJUSTMENT` (11) | `CNTPACK_LIVE_DAYEND` (8,256), `CounterSockDayend` (0) | `FoodStock` (0), `CNTPACK` (0), `CNTLOOSE` (0), `CNTPACK1` (0), `CNTPACK_LIVE_222` (0), `CNTLOOSE_LIVE_111` (0), `CntRecieved` (0), `CntRecieved_N` (0), `STK_CNTR_KEYPRESS` (2,923), `stockcheck` (0) |
| **5. Purchases** | 6 | `OtherPurchase` (16), `OtherPurchaseFinal` (10) | None | `LiqPurchase` (0), `LiqPurchaseFinal` (0), `FoodPurchase` (0), `FoodPurchaseFinal` (0) |
| **6. Excise Compliance** | 15 | `ExPremiteHolder` (524), `ExStoreInfo` (1), `HotelInfo` (1), `ExUnitLimit` (11), `ExciseOpening` (1,873), `ExciseClosingAutoSale` (1,873), `ExciseMonthlyStat` (1,873) | None | `cBilNumber` (1), `DryDates` (0), `ExciseBilNumber` (0), `ExciseInfo` (0), `ExFinalBill` (0), `ExFinalBillDetails` (0), `ExFinalBillDetailsTemp` (0), `ExFinalBillTemp` (0), `ExciseOpening_B` (0), `ExciseOpeningTemp` (0) |
| **7. Accounting & Ledgers** | 11 | `AccountHead` (256), `NewCustomer` (31), `NewVendor` (10), `Voucher` (6,770), `Receipt` (95), `CashTransfer` (1), `CashBookOpen` (11) | None | `NewBank` (0), `FoodIssue` (0), `temp` (1) |
| **8. Staff & Customers** | 4 | `Waiter` (4) | None | `Customer` (0), `ContactList` (0), `ContactList_Temp` (0) |
| **9. System Configuration** | 20 | `HotelInfo` (1), `Setup` (1), `Login` (2), `dateLckMaster` (1), `datelock` (1), `DAYEND` (1), `PRINTERSETUP` (3), `SoftwareName` (2), `product_key` (1), `softkey` (1), `BackupDrive` (1), `ExcisePath` (1), `Message` (1), `Deal_Name` (1), `PrintBillCount` (1) | None | `k` (0), `Paste Errors` (0), `rptdept_type` (0) |

---

### 2.2 Legacy Columns Analysis
Legacy column names reflect VB.NET non-standard naming, typos, and improper data types. The migration pipeline maps these into clean, PascalCase domain properties:

```
Legacy Table: item
  code (VARCHAR)            ──► CatalogItem.ItemCode (String)
  item (VARCHAR)            ──► CatalogItem.Name (String)
  dept (VARCHAR)            ──► KitchenDepartment.Name (String)
  salerate (INTEGER)        ──► CatalogItem.BasePrice (Decimal)
  FAMILYRATE (INTEGER)      ──► CatalogItem.FamilySectionPrice (Decimal)
  VIPRATE (INTEGER)         ──► CatalogItem.VipSectionPrice (Decimal)
  ACRATE (INTEGER)          ──► CatalogItem.AcSectionPrice (Decimal)
  WHOLESALE (INTEGER)       ──► CatalogItem.WholesalePrice (Decimal)
  exciserate (INTEGER)      ──► CatalogItem.ExciseRate (Decimal)
  purchaserate (INTEGER)    ──► CatalogItem.CostPrice (Decimal)
  bottel (INTEGER)          ──► CatalogItem.BottlesPerCase (Int32)
  unit (INTEGER)            ──► CatalogItem.VolumeMl (Int32)
  Marathi (VARCHAR)         ──► CatalogItem.MarathiName (String, UTF-8 Devanagari)

Legacy Table: CNTPACK_LIVE
  OPENING (INTEGER)         ──► BarCounterStock.OpeningBottles (Int32)
  OOPEN (INTEGER)           ──► BarCounterStock.InitialOpenBottles (Int32)
  CLOSING (INTEGER)         ──► BarCounterStock.ClosingBottles (Int32)
  CLOSINGLOOSE (INTEGER)    ──► BarCounterStock.ClosingOpenBottles (Int32)
  Opnbotlqty (INTEGER)      ──► BarCounterStock.OpenBottleRemainingMl (Int32)
  AddAdjml (INTEGER)        ──► BarCounterStock.SpillageAdditionMl (Int32)
  MinusAdjml (INTEGER)      ──► BarCounterStock.SpillageDeductionMl (Int32)

Legacy Table: Login
  passward (VARCHAR)        ──► ApplicationUser.PasswordHash (String, bcrypt hashed)
```

---

### 2.3 Legacy Keys Analysis
- **Missing Explicit Integrity Constraints**: Access Jet 4.0 database engine does not enforce Foreign Key constraints. Relationships are purely implicit.
- **Implicit Composite Primary Keys**:
  - `finalbill`: Relies on (`BILLNO`, `SRNO`) composite sequence.
  - `KOTDETAIL`: Relies on (`KOTNO`, `SRNO`) composite sequence.
  - `CNTPACK_LIVE` & `GodownStock`: Keyed by text `code` (e.g., `"001"`, `"102"`).
- **Sequence Resets**: In legacy operations, `BILLDAY` and `DAYKOT` were reset daily upon Day End rollover. However, `BILLNO` in `BILLFINAL` incremented globally up to 3,693.
- **Surrogate UUID Architecture**: Target PostgreSQL tables use UUID v7 (sequential UUIDs) as primary keys (`"Id"`). Migration creates a surrogate mapping table `migration_staging.map_keys` to map composite legacy keys to target UUIDs.

---

### 2.4 Legacy Relationships Mapping
The implicit legacy relationships are formally declared as PostgreSQL Foreign Key Constraints in the target schema:

```
[BILLFINAL] (BILLNO) ─────────< (BILLNO) [finalbill]
   │                                        │
   │ (WAITER)                               │ (ITEM)
   ▼                                        ▼
[Waiter] (name)                      [item] (item / code)
   ▲                                        ▲
   │ (WAITER)                               │ (ITEM)
[KOTFINAL] (KOTNO) ──────────< (KOTNO) [KOTDETAIL]

[GodownStock] (code) ───────► [item] (code) ◄─────── [CNTPACK_LIVE] (code)
```

1. **Invoice Line Items**: `finalbill.BILLNO` $\rightarrow$ `SalesInvoices.Id` via `BILLFINAL.BILLNO`.
2. **KOT Line Items**: `KOTDETAIL.KOTNO` $\rightarrow$ `KotHeaders.Id` via `KOTFINAL.KOTNO`.
3. **Void Logs**: `CancelKot.BILLNO` $\rightarrow$ `SalesInvoices.Id` via `BILLFINAL.BILLNO`.
4. **Stock Items**: `CNTPACK_LIVE.code` and `GodownStock.code` $\rightarrow$ `CatalogItems.Id` via `item.code`.
5. **Waitstaff**: `BILLFINAL.WAITER` and `KOTFINAL.WAITER` $\rightarrow$ `StaffMembers.Id` via `Waiter.name`.
6. **Petty Cash**: `Voucher.name` $\rightarrow$ `GeneralLedgerAccounts.Id` via `AccountHead.name`.

---

### 2.5 Duplicate Records Strategy
The legacy database contains significant data redundancy produced by VB.NET shadow writing and historical snapshot logging:

1. **Shadow Billing Headers (`BILLFINAL` vs `GrandBill`)**:
   - `BILLFINAL` (3,693 rows) and `GrandBill` (3,693 rows) contain identical transaction headers. `GrandBill` was maintained as a secondary shadow table.
   - **Resolution**: Extract `BILLFINAL` as authoritative header source. Perform checksum verification against `GrandBill` and drop `GrandBill`.
2. **Shadow Invoice Line Items (`finalbill` vs `GrandBillDetails` vs `finalbillcopy`)**:
   - `finalbill` (13,400 rows) and `GrandBillDetails` (13,400 rows) are 100% identical. `finalbillcopy` (13,209 rows) is an un-updated snapshot missing 191 post-edit line items.
   - **Resolution**: Extract `finalbill` as authoritative line items. Verify checksums against `GrandBillDetails` and discard copy tables.
3. **Daily Stock Snapshots (`CNTPACK_LIVE_DAYEND`)**:
   - `CNTPACK_LIVE_DAYEND` contains 8,256 snapshot rows logged across daily closing routines.
   - **Resolution**: Do not populate into live stock balances. Migrate into temporal audit log table `InventoryStockSnapshots`.

---

### 2.6 Invalid Data & Anomalies
Data cleansing rules during transformation quarantine non-conforming records:

| Anomaly Description | Affected Tables | Legacy Example | Cleansing / Transformation Action |
| :--- | :--- | :--- | :--- |
| **Orphan Line Items** | `finalbill` | 14 rows where `BILLNO` does not exist in `BILLFINAL` | Move to `quarantine_records` with error code `ERR_ORPHAN_BILLNO`. |
| **Non-Numeric Totals** | `GrandBill` | `TOTAL = " "` or `"N/A"` | Standardize using line item recalculation: `SUM(QTY * RATE)`. |
| **Negative Stock Count**| `CNTPACK_LIVE` | `CLOSING = -5` due to manual unlinked sales | Set `BarCounterStock.ClosingBottles = 0` and flag warning `WARN_NEGATIVE_STOCK`. |
| **Missing Item Name** | `finalbill` | `ITEM = NULL` or `""` (12 rows) | Recalculate or flag row into `quarantine_records` (`ERR_NULL_ITEM`). |
| **Null Date Field** | `Voucher` | `date = NULL` | Assign business date from corresponding `DAYEND.DATE` snapshot. |

---

### 2.7 Legacy Encoding & Character Sets
- **Mixed ASCII & Devanagari Script**: Menu items store English names (`Chicken Tikka`) in `item.item`, while Devanagari script (`चिकन टिक्का`) is stored in `item.Marathi`.
- **Legacy Font Encoding Anomalies**: Devanagari text in Jet 4.0 was saved using ANSI / Windows-1252 byte streams.
- **Cleansing Action**: The transformation pipeline parses raw byte streams, re-encodes string values into standard **UTF-8 Unicode**, and trims surrounding whitespace (`TRIM(REGEXP_REPLACE(str, '\s+', ' '))`).

---

### 2.8 Dates & Business Shifts
- **Timestamp Formats**:
  - `BILLFINAL.DATE`: Access `TIMESTAMP` (`YYYY-MM-DD HH:MM:SS`).
  - `BILLREPORT.DATE`: String formatted (`"12/05/2024"`).
  - `STK_CNTR_KEYPRESS`: ISO String (`"2024-05-12T14:32:00"`).
- **Business Day vs Calendar Date**:
  - In Indian bar operations, a business day running from 10:00 AM to 02:00 AM spans calendar midnight.
  - Transactions at 01:30 AM on May 13 belong to Business Date May 12 (`DAYEND.DATE`).
- **Coercion Rule**: Convert all legacy dates to UTC `TIMESTAMPTZ` assuming India Standard Time (IST, UTC+05:30) source context, while preserving explicit `BusinessDate` (`DATE`) on operational invoices.

---

### 2.9 Amounts & Monetary Fields
- **Floating-Point Precision Loss**: Legacy table `BILLFINAL` used `DOUBLE PRECISION` for `CGST` and `SGST`. Floating point sums lead to imprecise figures (e.g. `12.500000000000002`).
- **Missing Paisa / Cents Normalization**: `BILLFINAL.TOTAL` was stored as `INTEGER`, truncating decimal values.
- **Coercion Rule**: All monetary attributes are coerced to PostgreSQL `DECIMAL(18,2)` / `numeric(18,2)`:
  $$\text{NetPayable} = \text{ROUND}(\text{Subtotal} - \text{Discount} + \text{ServiceCharge} + \text{CGST} + \text{SGST}, 2)$$

---

### 2.10 Historical Transactions Rollover
- Active transaction tables (`BILLFINAL`, `finalbill`, `KOTFINAL`, `KOTDETAIL`) and rollover archive tables (`KOTFINAL_DAYEND`, `KOTDETAIL_DAYEND`, `CNTPACK_LIVE_DAYEND`) are unified during migration:
  - Union active and archived records.
  - Assign temporal attributes `IsArchived = True` and link to historical `BusinessShiftId`.

---

### 2.11 Multi-Tier Inventory Model Mapping
The legacy inventory tables map to the SaaS multi-tier liquor inventory model:

```
Legacy Multi-Tier Inventory              Modern Canonical Inventory Entities
┌───────────────────────────────┐        ┌───────────────────────────────────┐
│ GodownStock (1,926 rows)      │ ─────► │ WarehouseStock (Godown Location) │
└───────────────────────────────┘        └───────────────────────────────────┘
┌───────────────────────────────┐        ┌───────────────────────────────────┐
│ CNTPACK_LIVE (1,934 rows)     │ ─────► │ BarCounterStock (Sealed Bottles)  │
└───────────────────────────────┘        └───────────────────────────────────┘
┌───────────────────────────────┐        ┌───────────────────────────────────┐
│ CNTLOOSE_LIVE (757 rows)      │ ─────► │ LooseDispensaryStock (Open ML)    │
└───────────────────────────────┘        └───────────────────────────────────┘
┌───────────────────────────────┐        ┌───────────────────────────────────┐
│ BRANDML (741) + BtPerCase(12) │ ─────► │ PackagingUnit & UnitConversion    │
└───────────────────────────────┘        └───────────────────────────────────┘
```

- **Peg Conversion Logic**:
  - 1 Case = 12 Bottles (or `BtPerCase.CaseBott`)
  - 1 Sealed Bottle = 750 ml (Quart) / 375 ml (Pint) / 180 ml (Nip)
  - 1 Large Peg = 60 ml; 1 Small Peg = 30 ml
  - Opening a bottle decrements `BarCounterStock.ClosingBottles` by 1 and adds 750 ml to `LooseDispensaryStock.RemainingVolumeMl`.

---

### 2.12 Sales & Bills Mapping
- `BILLFINAL` + `finalbill` $\rightarrow$ `SalesInvoice` + `SalesInvoiceItem`:
  - Header attributes: `InvoiceNumber`, `BusinessDate`, `TableIdentifier`, `WaiterName`, `Subtotal`, `DiscountAmount`, `ServiceChargeAmount`, `CGSTAmount`, `SGSTAmount`, `TotalAmount`, `PaymentMode`.
  - Line attributes: `CatalogItemId`, `ItemName`, `DepartmentName`, `SectionName`, `Quantity`, `UnitPrice`, `LineGrossAmount`, `DispensingUnitMl`.

---

### 2.13 Users & Security Mapping
- `Login` (2 rows) $\rightarrow$ `ApplicationUser`:
  - `Login.Uname` (`"Admin"`, `"ADMIN"`) $\rightarrow$ `ApplicationUser.UserName`.
  - `Login.passward` (`"333"`, `"admin"`) $\rightarrow$ `ApplicationUser.PasswordHash` re-hashed via **bcrypt** (work factor 12).
- `Waiter` (4 rows) $\rightarrow$ `StaffMember`:
  - `Waiter.name` (`"WAITER 1"`, `"WAITER 2"`) $\rightarrow$ `StaffMember.FullName`.

---

### 2.14 Configuration Mapping
- `HotelInfo` (1 row) $\rightarrow$ `TenantInfo` & `TenantExciseProfile`:
  - Name: `"HOTEL YASHDEEP"`, License: `"FL III-2151444022D8ADF7"`, VAT TIN: `"27900111779v"`, UPI: `"dinu"`.
- `Setup` (1 row) $\rightarrow$ `TenantSettings`:
  - Flags for KOT setup, thermal printing, bill lock, GST enablement, dynamic QR printing.
- `TABLE_NO_GROP` (6 rows) $\rightarrow$ `FloorSection`:
  - Family, AC, Hall, Restaurant, Garden, Parcel.
- `ItemDept` (39 rows) $\rightarrow$ `KitchenDepartment`:
  - Tandoor, Bar, Chinese, Kitchen, with printer routing endpoints.

---

### 2.15 Reports Mapping
- Legacy Crystal Reports templates (`nwitem5.rpt`, `BILLREPORT`, `rptdept_type`) are superseded by QuestPDF code-first report components.
- SQL queries underlying legacy reports are converted into PostgreSQL Materialized Views:
  - `vw_item_sales_summary`
  - `vw_section_revenue_analysis`
  - `vw_excise_daily_register`

---

### 2.16 Maharashtra FL-III Excise Data Mapping
- `ExPremiteHolder` (524 rows) $\rightarrow$ `ExcisePermittee`:
  - Name, Permit Number, Issue Date, Expiry Date, Address, Contact.
- `ExciseOpening` (1,873), `ExciseClosingAutoSale` (1,873), `ExciseMonthlyStat` (1,873) $\rightarrow$ `ExciseStockRegister`:
  - FL-III Register 1 balance, Bulk Litre IMFL/Beer/Wine receipts, TP (Transport Permit) logs, Daily sales volume, Closing balance reported to Excise Inspector.

---

## 3. ETL Pipeline Architecture Specification

### 3.1 Extraction Pipeline
- **Engine**: .NET 9 Migration Console Worker / Python `pyodbc` + `mdbtools` read-only extractor.
- **Connection Mode**: Read-only connection string (`Mode=Share Deny None;ReadOnly=True;`).
- **Staging Target**: Schema `migration_staging` in PostgreSQL.

```sql
CREATE SCHEMA IF NOT EXISTS migration_staging;

CREATE TABLE migration_staging.map_keys (
    legacy_table VARCHAR(100) NOT NULL,
    legacy_key VARCHAR(255) NOT NULL,
    target_uuid UUID NOT NULL PRIMARY KEY,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);
```

---

### 3.2 Transformation Specifications

```
  Legacy Raw Record                Staging Transformation Logic               Target Domain Object
┌──────────────────┐           ┌───────────────────────────────────┐         ┌────────────────────┐
│ BILLFINAL        │           │ 1. Cast TOTAL to DECIMAL(18,2)    │         │ SalesInvoice       │
│  BILLNO = 1001   │           │ 2. Generate UUID v7 Primary Key   │         │  Id = UUIDv7       │
│  TOTAL = 450     │ ────────► │ 3. Parse DATE to TIMESTAMPTZ UTC  │ ──────► │  LegacyBillNo=1001 │
│  CGST = 11.25    │           │ 4. Inject TenantId Discriminator │         │  TotalAmount=450.00│
│  SGST = 11.25    │           │ 5. Map WAITER string to Staff UUID│         │  TenantId = UUID   │
└──────────────────┘           └───────────────────────────────────┘         └────────────────────┘
```

Transformation rules applied in .NET pipeline:
1. **Surrogate Key Generation**: Generate `Guid.CreateVersion7()` for every canonical entity. Store key mapping in `migration_staging.map_keys`.
2. **Text Normalization**: Sanitize strings, strip control characters, convert Devanagari text to UTF-8.
3. **Monetary Standardization**: Coerce all numbers to `decimal` rounded to 2 decimal places.
4. **Timestamp Conversion**: Map local IST time to UTC `DateTimeOffset`.
5. **Password Re-Hashing**: Re-hash legacy plain text passwords using `BCrypt.Net.BCrypt.HashPassword(pass, 12)`.

---

### 3.3 Validation Engine Rules
Every record must satisfy mandatory validation constraints before loading:

$$\text{Validation Matrix}$$
$$\begin{array}{|l|l|l|}
\hline
\textbf{Rule ID} & \textbf{Validation Logic} & \textbf{Failure Action} \\ \hline
\text{V01} & |\text{SalesInvoice.Subtotal} - \sum \text{Item.LineGross}| < 0.01 & \text{Recompute Subtotal} \\ \hline
\text{V02} & |\text{SalesInvoice.CGSTAmount} - (\text{FoodSubtotal} \times 0.025)| < 0.05 & \text{Quarantine if } > 1.00 \\ \hline
\text{V03} & \text{Foreign Key exists in parent header} & \text{Move to Quarantine Table} \\ \hline
\text{V04} & \text{CatalogItem.BasePrice} \ge 0 & \text{Reject Record} \\ \hline
\text{V05} & \text{ExcisePermittee.LicenseNumber is non-null} & \text{Flag Warning} \\ \hline
\end{array}$$

---

### 3.4 Loading Strategy
- **High-Performance Loading**: Utilizes Npgsql Binary COPY protocol (`COPY target_table FROM STDIN (FORMAT BINARY)`).
- **Dependency Loading Order**:
  1. `Tenants` & `TenantExciseProfiles`
  2. `ApplicationUsers` & `StaffMembers`
  3. `KitchenDepartments` & `FloorSections`
  4. `CatalogItems`, `LiquorBrands`, `PackagingUnits`
  5. `Warehouses`, `WarehouseStock`, `BarCounterStock`
  6. `ExcisePermittees`
  7. `SalesInvoices` & `SalesInvoiceItems`
  8. `KotHeaders` & `KotItems`
  9. `GeneralLedgerAccounts` & `PettyCashVouchers`
  10. `ExciseStockRegisters`

---

### 3.5 Reconciliation Engine
The reconciliation engine executes automated comparative queries between source Access staging and target PostgreSQL tables:

$$\Delta_{\text{Revenue}} = \sum \text{BILLFINAL.TOTAL} - \sum \text{SalesInvoice.TotalAmount}$$
$$\Delta_{\text{StockBottles}} = \sum \text{CNTPACK\_LIVE.CLOSING} - \sum \text{BarCounterStock.ClosingBottles}$$
$$\Delta_{\text{LooseMl}} = \sum \text{CNTLOOSE\_LIVE.TOTALML} - \sum \text{LooseDispensaryStock.RemainingVolumeMl}$$

If any $\Delta \neq 0$, the pipeline flags an error and outputs a line-by-line mismatch audit log.

---

### 3.6 Error Handling & Quarantine Pattern
Unresolvable rows are diverted into quarantine without halting the migration process:

```sql
CREATE TABLE migration_staging.quarantine_records (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    legacy_table VARCHAR(100) NOT NULL,
    legacy_pk VARCHAR(255) NOT NULL,
    error_code VARCHAR(50) NOT NULL,
    error_message TEXT NOT NULL,
    raw_payload_json JSONB NOT NULL,
    quarantined_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);
```

---

### 3.7 Rollback Mechanics
- **Tenant-Isolated Savepoints**: Loading operations for a tenant run inside an explicit PostgreSQL transaction block.
- **Rollback Trigger**: If validation error rate exceeds 1% or financial mismatch $\Delta > 0$, the pipeline executes:
  `ROLLBACK TO SAVEPOINT tenant_migration_start;`
- Restores target database state instantly without affecting other active tenants.

---

### 3.8 Dry-Run Migration Execution (`--dry-run`)
Dry-run mode allows executing the complete ETL pipeline in simulated mode:
```bash
dotnet run --project MigrationTool -- --dry-run --tenant "HOTEL YASHDEEP" --source "RSS26/dinurss.mdb"
```
1. Extracts all records from `dinurss.mdb`.
2. Transforms and validates every entity.
3. Computes financial and inventory reconciliation metrics.
4. Generates migration report artifact.
5. Issues an automatic `ROLLBACK` on PostgreSQL, leaving production tables untouched.

---

### 3.9 Migration Reports Strategy
Upon pipeline execution, two audit artifacts are generated:
1. `MIGRATION_EXECUTION_SUMMARY.md`: Executive summary report.
2. `MIGRATION_DETAILS.json`: Machine-readable audit file containing row counts, checksums, quarantine items, and timing metrics.

---

## 4. Reconciliation Metrics & Verification Protocols

### 4.1 Record Counts Comparison Matrix

| Legacy Domain / Table | Access Row Count | Target PostgreSQL Entity | Expected Target Rows | Quarantine Threshold |
| :--- | :---: | :--- | :---: | :---: |
| `BILLFINAL` | 3,693 | `SalesInvoice` | 3,693 | 0 |
| `finalbill` | 13,400 | `SalesInvoiceItem` | 13,386 | $\le 14$ |
| `KOTFINAL` + `DAYEND` | 13,018 | `KotHeader` | 13,018 | 0 |
| `KOTDETAIL` + `DAYEND` | 22,413 | `KotItem` | 22,413 | 0 |
| `CancelKot` | 1,389 | `VoidItemLog` | 1,389 | 0 |
| `item` | 1,607 | `CatalogItem` | 1,607 | 0 |
| `GodownStock` | 1,926 | `WarehouseStock` | 1,926 | 0 |
| `CNTPACK_LIVE` | 1,934 | `BarCounterStock` | 1,934 | 0 |
| `CNTLOOSE_LIVE` | 757 | `LooseDispensaryStock` | 757 | 0 |
| `ExPremiteHolder` | 524 | `ExcisePermittee` | 524 | 0 |
| `Voucher` | 6,770 | `PettyCashVoucher` | 6,770 | 0 |
| `AccountHead` | 256 | `GeneralLedgerAccount` | 256 | 0 |
| `Login` | 2 | `ApplicationUser` | 2 | 0 |
| `Waiter` | 4 | `StaffMember` | 4 | 0 |

---

### 4.2 Financial Reconciliation Verification

| Financial Metric | Legacy Access Query (`dinurss.mdb`) | Target PostgreSQL Query | Permissible Variance |
| :--- | :--- | :--- | :---: |
| **Gross Revenue** | `SELECT SUM(TOTAL) FROM BILLFINAL` | `SELECT SUM("TotalAmount") FROM "SalesInvoices"` | ₹ 0.00 |
| **Food Revenue** | `SELECT SUM(FOODTOT) FROM BILLFINAL` | `SELECT SUM("FoodSubtotal") FROM "SalesInvoices"` | ₹ 0.00 |
| **CGST Collected** | `SELECT SUM(CGST) FROM BILLFINAL` | `SELECT SUM("CGSTAmount") FROM "SalesInvoices"` | $\le$ ₹ 0.50 (Rounding) |
| **SGST Collected** | `SELECT SUM(SGST) FROM BILLFINAL` | `SELECT SUM("SGSTAmount") FROM "SalesInvoices"` | $\le$ ₹ 0.50 (Rounding) |
| **Total Discounts** | `SELECT SUM(DISCOUNT) FROM BILLFINAL` | `SELECT SUM("DiscountAmount") FROM "SalesInvoices"` | ₹ 0.00 |
| **Service Charge** | `SELECT SUM(SERVICE_CHARGE) FROM BILLFINAL`| `SELECT SUM("ServiceChargeAmount") FROM "SalesInvoices"`| ₹ 0.00 |
| **Petty Cash Disbursed**| `SELECT SUM(amt) FROM Voucher` | `SELECT SUM("Amount") FROM "PettyCashVouchers"` | ₹ 0.00 |

---

### 4.3 Inventory Reconciliation Verification

| Stock Metric | Legacy Access Query (`dinurss.mdb`) | Target PostgreSQL Query | Permissible Variance |
| :--- | :--- | :--- | :---: |
| **Godown Total Units** | `SELECT SUM(CLOSING) FROM GodownStock` | `SELECT SUM("ClosingQuantity") FROM "WarehouseStocks"` | 0 Units |
| **Counter Sealed Bottles** | `SELECT SUM(CLOSING) FROM CNTPACK_LIVE` | `SELECT SUM("ClosingBottles") FROM "BarCounterStocks"` | 0 Bottles |
| **Loose ML Volume** | `SELECT SUM(TOTALML) FROM CNTLOOSE_LIVE` | `SELECT SUM("RemainingVolumeMl") FROM "LooseDispensaryStocks"` | 0 ML |
| **Permittee Count** | `SELECT COUNT(*) FROM ExPremiteHolder` | `SELECT COUNT(*) FROM "ExcisePermittees"` | 0 Records |

---

### 4.4 Post-Migration Verification Protocols
1. **Automated Documentation & Schema Verification**: Run `python3 /home/jules/self_created_tools/doc_audit.py MIGRATION_ARCHITECTURE.md schema_extracted/tables_inventory.csv` to confirm doc links and table references.
2. **Referential Integrity Audit**: Execute SQL queries ensuring zero orphaned line items exist in target tables.
3. **Multi-Tenant Isolation Verification**: Verify 100% of inserted PostgreSQL rows contain a valid `TenantId`.
4. **User Authentication Check**: Authenticate with converted user credentials and verify bcrypt password hash validation.
5. **QuestPDF Thermal Invoice Verification**: Render migrated historical invoice IDs to PDF/ESC/POS byte streams and verify matching totals with original receipts.
6. **Source Asset Protection Audit**: Verify file hash checksums of `RSS26/dinurss.mdb` pre- and post-migration to guarantee zero modification of legacy production assets.
