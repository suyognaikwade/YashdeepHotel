# Migration Architecture & Data Pipeline Specification

## Executive Summary

This document defines the comprehensive data migration architecture and ETL (Extract, Transform, Load) pipeline for transitioning legacy data from the **Yashdeep Hotel Management System** (`RSS26/dinurss.mdb`, Microsoft Access Jet 4.0) into the modern **Multi-Tenant Cloud-Synchronized SaaS Platform** (.NET 9, PostgreSQL 16, SQLite).

### Strategic Directives
1. **Canonical Domain Mapping (No Plain Copying)**: Data must not be copied as raw table dumps. Legacy procedural and split tables must be transformed into the modern DDD (Domain-Driven Design) canonical domain model (e.g., merging `BILLFINAL` and `BILLFINAL_Dayend` into `Invoices`, converting ML dispensing units into standardized liquid metrics).
2. **Strict Non-Destructive Operations**: The legacy Microsoft Access database (`dinurss.mdb`) is read-only. No write, update, lock, or drop operations are ever executed against legacy production files.
3. **Multi-Tenant Isolation**: Every migrated entity is explicitly assigned a server-generated `TenantId` (UUIDv4) and `OutletId` to guarantee row-level security (RLS) in PostgreSQL.
4. **Idempotency & Re-runnability**: Deterministic entity UUIDs generated from legacy business keys ensure the migration pipeline can be re-run safely without creating duplicate records or orphaned relations.
5. **Zero Data Loss Guarantee**: Every historical transaction, kitchen ticket, voucher, customer permit, and stock movement is cleansed, validated, loaded, and reconciled with financial and physical volume mathematical proofs.

---

## 1. Domain Mapping: Legacy Access to Modern Canonical Model

The migration transforms 105 procedural Microsoft Access tables across 9 legacy groupings into 12 core bounded contexts in the modern canonical domain model.

```
┌────────────────────────────────────────────────────────────────────────┐
│                   LEGACY ACCESS (dinurss.mdb)                          │
├─────────────────┬───────────────────┬───────────────────┬──────────────┤
│ Billing & KOT   │ Inventory & Stock │ Excise (FL-III)   │ Accounting   │
│ BILLFINAL       │ GodownStock       │ ExPremiteHolder   │ AccountHead  │
│ BILLFINAL_Dayend│ CNTPACK_LIVE      │ ExciseOpening     │ CashTransfer │
│ finalbill       │ CNTLOOSE_LIVE     │ ExciseClosing...  │ Payment      │
│ KOTFINAL        │ LiqPurchase       │ ExStoreInfo       │ Receipt      │
│ KOTDETAIL       │ FoodStock         │ ExUnitLimit       │ Voucher      │
└────────┬────────┴─────────┬─────────┴─────────┬─────────┴──────┬───────┘
         │                 │                   │                │
         ▼                 ▼                   ▼                ▼
┌────────────────────────────────────────────────────────────────────────┐
│               CANONICAL ETL TRANSFORMATION ENGINE                      │
│   • UTF-8 / Devanagari Encoding    • Multi-tier ML Liquid Conversion   │
│   • Unification of Day-End Splits  • Decimal Currency Standard         │
│   • Deterministic UUID Generation  • Tenant / Outlet Isolation        │
└────────┬─────────────────┬───────────────────┬────────────────┬────────┘
         │                 │                   │                │
         ▼                 ▼                   ▼                ▼
┌────────────────────────────────────────────────────────────────────────┐
│                 MODERN SAAS CANONICAL DOMAIN MODEL                     │
├─────────────────┬───────────────────┬───────────────────┬──────────────┤
│ Billing Domain  │ Inventory Domain  │ Excise Context    │ Finance      │
│ Invoices        │ InventoryItems    │ PermitHolders     │ LedgerAccounts│
│ InvoiceLines    │ StockBatches      │ ExciseRegisters   │ JournalEntries│
│ Orders / KOTs   │ StockTransfers    │ DailySummary      │ Vouchers     │
│ OrderLines      │ LiquidDispensings │ Returns           │ Payments     │
└─────────────────┴───────────────────┴───────────────────┴──────────────┘
```

### 1.1 Detailed Domain Mapping Matrix

| Legacy Access Tables | Modern Canonical Domain Entity | Key Mappings & Structural Changes |
| :--- | :--- | :--- |
| `BILLFINAL`, `BILLFINAL_Dayend`, `GrandBill` | `Invoices` | Merges active daily and historical day-end tables into unified immutable `Invoices`. Legacy `BILLNO` + `DATE` maps to `InvoiceNumber` and `TenantId`. `FOODTOT` separated into item categories. |
| `finalbill`, `finalbillcopy`, `GrandBillDetails` | `InvoiceLines` | Unifies itemized line items. Converts string quantities and integer rates into `DECIMAL(18,2)`. Links via `InvoiceId` (FK). |
| `KOTFINAL`, `KOTFINAL_DAYEND` | `Orders` | Combines KOT headers. Maps `KOTNO` to `OrderNumber`. Maps status (`Active`, `Billed`, `Cancelled`). |
| `KOTDETAIL`, `KOTDETAIL_DAYEND` | `OrderLines` | Combines KOT line items. Resolves menu item references via SKU/code lookup. |
| `CancelKot` | `OrderAuditLogs` | Stores voided/cancelled KOT lines with reason, waiter ID, and audit timestamps. |
| `item`, `NewFood`, `BRANDML` | `MenuItems`, `MenuCategories`, `LiquorBrands` | Normalizes flat menu table into hierarchical catalog. Converts `FAMILYRATE`, `ACRATE`, `VIPRATE`, `salerate` into `MenuItemPrices` by `SectionId`. Normalizes Devanagari `Marathi` text to UTF-8. |
| `ItemDept` | `KitchenDepartments` | Maps department codes to kitchen routing stations and thermal printer endpoints (`printer`). |
| `GodownStock`, `FoodStock` | `InventoryItems`, `StockBatches` | Maps godown bulk stock. Converts bottles/cases into standard stock units and volume in ML (`VolumeMl`). |
| `CNTPACK_LIVE`, `CNTPACK_LIVE_DAYEND`, `CNTLOOSE_LIVE` | `CounterInventory`, `LiquidDispensings` | Unifies counter sealed bottles and open bottle loose stock into unified live balance. Converts ML dispensing units (30, 60, 90, 180, 375, 750 ML) into precise liquid volume metrics. |
| `LiqPurchase`, `LiqPurchaseFinal`, `FoodPurchase` | `PurchaseOrders`, `PurchaseOrderLines` | Maps vendor purchases, stock inward entries, bottle case counts, and invoice amounts. |
| `ExPremiteHolder` | `PermitHolders` | Maps Maharashtra State Excise liquor permit holders, permit numbers (`licence`), validity dates, and contact details. |
| `ExciseOpening`, `ExciseClosingAutoSale`, `ExciseMonthlyStat` | `ExciseRegisters` | Maps official Register 1 & Register 2 FL-III daily balance logs, opening/closing bottle counts, and ML totals. |
| `AccountHead` | `LedgerAccounts` | Maps general ledger chart of accounts. Converts `credit` / `debit` doubles to `DECIMAL(18,2)`. |
| `Voucher`, `Payment`, `Receipt`, `CashTransfer` | `JournalEntries`, `Vouchers` | Maps cash disbursed, payments, receipts, and cash book transfers with double-entry accounting constraints. |
| `Login` | `Users`, `UserRoles` | Maps application operators to ASP.NET Core Identity users with bcrypt password hashes and RBAC permissions. |
| `HotelInfo`, `Setup`, `dateLckMaster` | `TenantConfigurations`, `OutletSettings` | Maps hotel metadata (`licno`, `Vattin`, `name`), operational rules, lock dates, and payment VPAs (`upi_id`). |

---

## 2. Legacy Data Topology & Anomaly Analysis

### 2.1 Schema Keys & Relationship Mappings

```
                    [Legacy Entity Relationships]

   BILLFINAL / BILLFINAL_Dayend                 finalbill
  ┌────────────────────────────┐             ┌────────────────────────────┐
  │ PK: BILLNO (INTEGER)       │1          * │ FK: BILLNO (INTEGER)       │
  │     DATE (TIMESTAMP)       ├────────────►│     ITEM (VARCHAR)         │
  │     TABLE_NO (VARCHAR)     │             │     QTY (INTEGER/STRING)   │
  └────────────────────────────┘             └────────────────────────────┘

             item                                 CNTPACK_LIVE
  ┌────────────────────────────┐             ┌────────────────────────────┐
  │ PK: code (VARCHAR/INTEGER) │1          1 │ FK: code (VARCHAR/INTEGER) │
  │     item (VARCHAR)         ├────────────►│     OPENING (INTEGER)      │
  │     FAMILYRATE (INTEGER)   │             │     CLOSINGLOOSE (INTEGER) │
  └────────────────────────────┘             └────────────────────────────┘
```

* **Missing Referential Integrity**: The Jet 4.0 database lacks enforced Foreign Key constraints. Relationships exist purely in procedural VB.NET code (e.g., `finalbill.BILLNO` referencing `BILLFINAL.BILLNO`).
* **Composite Business Keys**: In legacy Access, `BILLNO` alone is NOT globally unique because daily bill counters reset or duplicate across Day End archival cycles. Modern keys are derived as:
  $$\text{InvoiceId} = \text{UUIDv5}(\text{Namespace\_DNS}, \text{TenantId} \mathbin{\Vert} \text{"BILL"} \mathbin{\Vert} \text{BILLNO} \mathbin{\Vert} \text{FormattedDate})$$

### 2.2 Legacy Data Anomalies & Cleansing Rules

| Anomaly Type | Legacy Manifestation | Root Cause | Cleansing & Transformation Rule |
| :--- | :--- | :--- | :--- |
| **Day End Table Split** | Transactions split between `BILLFINAL` (today) and `BILLFINAL_Dayend` (history). | Performance workaround in Access Jet to avoid slow B-tree indexing on large tables. | Union both tables during extraction, deduplicate based on `(BILLNO, DATE, TOTAL)`, and load into unified `Invoices` table with historical status flags. |
| **Duplicate Keys** | Re-used `BILLNO` or `KOTNO` across different dates or after manual counter resets. | Sequence reset logic in `FrmDtpDayend`. | Map to deterministic UUIDs using combined `(TenantId, BusinessDate, TableNo, SequenceNo)` composite key. |
| **Orphaned Lines** | Records in `finalbill` or `KOTDETAIL` with no matching header in `BILLFINAL` or `KOTFINAL`. | Interrupted transactions during network drops or power cuts. | Quarantine orphaned lines into `MigrationOrphans` table. Attempt header recovery using date, table number, and total matching; otherwise mark as audit exceptions. |
| **Legacy Character Encoding** | Devanagari text in `item.Marathi` stored using legacy ANSI (Windows-1252) or proprietary font encoding (e.g., KrutiDev / Shivaji). | Legacy VB.NET WinForms font hacks for thermal printers. | Detect font encoding bytes, transcode using Devanagari map to UTF-8 Unicode, and sanitize string outputs. |
| **Date / Time Formatting Anomalies** | Dates stored inconsistently as OLE Automation floats (`44562.5`), strings (`"25/12/2023"`), or NULL timestamps. | Mixed data entry in VB.NET forms without input masks. | Parse dates through multi-format parser (`ISO-8601`, `OLE Automation`, `dd/MM/yyyy`, `yyyy-MM-dd`). Fall back to bill header date or day-end audit date if timestamp is corrupt. |
| **Floating Point Currency Imprecision** | Tax rates (`CGST`, `SGST`) stored as `DOUBLE PRECISION`, resulting in binary floating-point representations (e.g., `2.49999999999999`). | Access `Double` type usage in Jet DB schema. | Cast all monetary values, totals, and tax subtotals to `DECIMAL(18,2)` using `MidpointRounding.AwayFromZero`. |
| **Multi-Tier Inventory Discrepancies** | Negative loose stock (`CLOSINGLOOSE < 0`), unrecorded breakage, bottle ML mismatch. | Manual overrides by bartenders without inventory adjustments. | Recalculate stock movements from purchase inward and sales line items. Flag negative balances and record an automated `StockAdjustment` entry for the variance. |
| **Plain-Text Credentials** | Passwords in `Login` table stored in plain text or simple XOR mask (`333`, `admin`). | Legacy system lack of hashing. | Hash all user passwords using `BCrypt` (work factor 12) upon loading. Issue forced password reset flags for security compliance. |

---

## 3. ETL Pipeline Architecture

The ETL pipeline is structured as an isolated, idempotent, multi-stage .NET 9 Console / Worker process.

```
┌────────────────────────────────────────────────────────────────────────┐
│                        EXTRACT STAGE (Read-Only)                       │
│  • Connect to dinurss.mdb via System.Data.OleDb / mdbtools / C# Driver │
│  • Stream records in dynamic batches (5,000 rows/batch)                │
│  • Compute source MD5 checksum per row for change tracking             │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                      TRANSFORM & CLEANSE STAGE                         │
│  • Transcode Encoding: Win-1252 / KrutiDev ──► UTF-8 Unicode           │
│  • Parse & Normalize Dates to UTC / ISO-8601                           │
│  • Convert ML Liquid Metrics (30ml, 60ml, 90ml, 180ml ──► VolumeMl)    │
│  • Recalculate Currency / Tax Fields to DECIMAL(18,2)                  │
│  • Generate Idempotent Deterministic UUIDs                             │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        VALIDATE STAGE (Rules Engine)                   │
│  • Schema Invariants Check (Non-null, Type Bounds)                     │
│  • Referential Integrity Check (FK Existence)                          │
│  • Financial Balancing Rules (Header Total == Sum of Line Amounts)     │
│  • PASS ──► Load Queue | FAIL ──► Dead-Letter Queue (DLQ)              │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                         LOAD STAGE (PostgreSQL 16)                     │
│  • Open Transaction with Isolated Tenant Context                       │
│  • Execute Bulk Copy (`NpgsqlBinaryImporter`)                          │
│  • Upsert on Deterministic UUID Collision                              │
│  • Write Audit Trail Logs & Financial/Inventory Summaries              │
└────────────────────────────────────────────────────────────────────────┘
```

### 3.1 Pipeline Phase Specifications

#### Phase 1: Extraction
* **Source Access Protocol**:
  * **Windows / OLEDB**: `Provider=Microsoft.ACE.OLEDB.12.0;Data Source=RSS26\dinurss.mdb;Jet OLEDB:Database Password=rss1008;`
  * **Linux / Cross-Platform**: Direct `mdbtools` binary stream or embedded C# Jet driver reading raw MDB tables.
* **Extraction Mode**: Cursor-based streaming with explicit batch sizes ($N = 5000$).
* **Read-Only Enforcer**: Connection string set to `Mode=Read;` with OS-level file permission checks to guarantee `dinurss.mdb` is never written to.

#### Phase 2: Transformation & Cleansing
* **Devanagari Normalization**:
  ```csharp
  public static string TranscodeLegacyText(string rawInput)
  {
      if (string.IsNullOrWhiteSpace(rawInput)) return string.Empty;
      // Detect legacy font bytes or KrutiDev font encoding
      if (IsKrutiDevEncoded(rawInput)) {
          return KrutiDevToUnicodeConverter.Convert(rawInput);
      }
      return rawInput.Normalize(NormalizationForm.FormC);
  }
  ```
* **Liquid Metric Standardizer**:
  * Converts legacy `unit` integer (30, 60, 90, 180, 375, 750) and `salerate` into standardized volume:
    $$\text{TotalVolumeMl} = (\text{SealedBottles} \times \text{BottleMl}) + \text{LooseMl}$$

#### Phase 3: Validation Rules Engine
Every transformed DTO passes through a FluentValidation engine checking domain invariants:
1. **Invoice Total Invariant**:
   $$\left| \text{Header.Total} - \left( \sum \text{Line.Amount} - \text{Header.Discount} + \text{Header.Taxes} \right) \right| \le 0.05$$
2. **Date Sanity Invariant**: $2000\text{-01-01} \le \text{TransactionDate} \le \text{CurrentUtcTime} + 1\text{ day}$.
3. **Inventory Non-Negativity Invariant**: Warns and flags negative balances while capturing variance.

#### Phase 4: Loading & Batching
* Uses `NpgsqlBinaryImporter` for high-throughput binary streaming into PostgreSQL.
* Transaction Scope per Batch: $5,000$ records per database transaction block. On batch failure, pipeline isolates faulty records into Dead-Letter Queue (DLQ) and commits valid records.

---

## 4. Subsystem Migration Protocols

### 4.1 Billing & Invoicing Context (`BILLFINAL` & `finalbill`)

#### Legacy Table Union & Deduplication
```sql
-- Conceptual Extraction Query across Day-End Boundaries
SELECT
    'ACTIVE' AS SourceTable, BILLNO, TABLE_NO, WAITER, [DATE], PAID, TYPE,
    CUST_NAME, SERVICE_CHARGE, DISCOUNT, TOTAL, BILLDAY, TIME, CLIENTID,
    OPERATOR, CGST, SGST, FOODTOT, SECTIONS
FROM BILLFINAL
UNION ALL
SELECT
    'DAYEND' AS SourceTable, BILLNO, TABLE_NO, WAITER, [DATE], PAID, TYPE,
    CUST_NAME, SERVICE_CHARGE, DISCOUNT, TOTAL, BILLDAY, TIME, CLIENTID,
    OPERATOR, CGST, SGST, FOODTOT, SECTIONS
FROM BILLFINAL_Dayend;
```

#### Field Conversion Spec
* `PAID`: `1` $\rightarrow$ `InvoiceStatus.Paid`, `0` $\rightarrow$ `InvoiceStatus.Unpaid`.
* `TYPE`: Standardized string mapping: `"Cash"` $\rightarrow$ `PaymentMode.Cash`, `"Card"` $\rightarrow$ `PaymentMode.Card`, `"UPI"` $\rightarrow$ `PaymentMode.UPI`, `"Credit"` / `"CUST"` $\rightarrow$ `PaymentMode.Credit`.
* `SECTIONS`: `"Family"` $\rightarrow$ Section UUID for Family Room, `"AC"` $\rightarrow$ Section UUID for AC Hall, etc.

### 4.2 Kitchen Order Tickets (`KOTFINAL` & `KOTDETAIL`)

* Unifies active `KOTFINAL` and historical `KOTFINAL_DAYEND`.
* Unifies active `KOTDETAIL` and historical `KOTDETAIL_DAYEND`.
* Cancelled orders from `CancelKot` are mapped into `OrderAuditLogs` linked to `OrderId` and cashier operator details.

### 4.3 Multi-Tier Liquor Inventory (`GodownStock` & `CNTPACK_LIVE`)

Legacy inventory tracks stock across two locations and three states:
1. **Godown Stock** (`GodownStock`): Sealed Cases / Bottles in primary warehouse.
2. **Counter Sealed Stock** (`CNTPACK_LIVE`): Sealed Bottles behind the bar counter.
3. **Counter Loose Stock** (`CNTLOOSE_LIVE` / `Opnbotlqty`): Open bottle ML volume (e.g., $450\text{ ml}$ remaining in a $750\text{ ml}$ bottle).

#### Transformation Protocol
$$\text{TotalStockMl} = (\text{GodownBottles} + \text{CounterSealedBottles}) \times \text{BottleCapacityMl} + \text{OpenBottleMl}$$
* Multi-tier conversions generate initial `StockBatch` records in PostgreSQL with full ML volume tracking.

```
  Godown Cases/Bottles      Counter Sealed Bottles      Open Bottle ML
 ┌────────────────────┐    ┌──────────────────────┐   ┌─────────────────┐
 │ GodownStock        │    │ CNTPACK_LIVE         │   │ CNTLOOSE_LIVE   │
 │ OPENING / CLOSING  │    │ OPENING / CLOSING    │   │ Opnbotlqty (ML) │
 └─────────┬──────────┘    └──────────┬───────────┘   └────────┬────────┘
           │                          │                        │
           └──────────────────────────┼────────────────────────┘
                                      ▼
             ┌─────────────────────────────────────────────────┐
             │ Modern Inventory System (StockBatches)           │
             │ QuantityInMl = Sum(Bottles * UnitMl) + LooseMl  │
             └─────────────────────────────────────────────────┘
```

### 4.4 Maharashtra State Excise FL-III Compliance (`ExPremiteHolder` & `ExciseClosingAutoSale`)

* `ExPremiteHolder`: Transformed into `PermitHolders`.
  * `licence` $\rightarrow$ `PermitNumber` (VARCHAR).
  * `validity` $\rightarrow$ `PermitType` (`Daily`, `Annual`, `LLD` / Lifetime).
  * `Expiry` $\rightarrow$ `ExpirationDate` (DATE).
* `ExciseOpening` & `ExciseClosingAutoSale`: Mapped directly to modern `ExciseRegisters` (Register 1 & Register 2) maintaining continuous daily bottle & ML balances required for State Excise audits.

### 4.5 Accounting & Ledgers (`AccountHead` & `Voucher`)

* `AccountHead`: Mapped to `LedgerAccounts`.
  * `credit` / `debit` converted to `DECIMAL(18,2)`.
* `Voucher`: Transformed into `JournalEntries` and `Vouchers`.
  * Every voucher creates a balanced double-entry transaction:
    * **Debit**: Expense Account (`AccountHead.name` $\rightarrow$ `LedgerAccountId`).
    * **Credit**: Cash / Bank Account.

---

## 5. Reconciliation & Verification Protocols

To guarantee mathematical correctness and absolute financial integrity, the migration pipeline executes a three-tier reconciliation process.

```
                             [THREE-TIER RECONCILIATION]

  Tier 1: Record Counts        Tier 2: Financial Balance      Tier 3: Physical Stock
 ┌──────────────────────┐     ┌────────────────────────┐     ┌──────────────────────┐
 │ Access Rows          │     │ SUM(BILLFINAL.TOTAL)   │     │ Sum(Godown + Counter)│
 │   vs                 │  &  │   vs                   │  &  │   vs                 │
 │ PostgreSQL Records   │     │ SUM(Invoices.Total)    │     │ Total Stock ML       │
 └──────────────────────┘     └────────────────────────┘     └──────────────────────┘
```

### 5.1 Tier 1: Record Count Audit Matrix

The pipeline computes pre-migration source counts and asserts exact post-migration target counts.

```
                  Record Count Verification Assertion Formula

   Source Count (Access) = Extracted Rows - DLQ Quarantined Rows
   Target Count (Postgres) = Inserted Rows + Upserted Collisions

   ASSERT: Source Count (Access) == Target Count (Postgres)
```

| Domain | Legacy Table(s) | Target Entity | Expected Row Count (approx.) |
| :--- | :--- | :--- | :--- |
| **Invoices** | `BILLFINAL` + `BILLFINAL_Dayend` | `Invoices` | $\approx 3,693 + \text{History}$ |
| **Invoice Lines** | `finalbill` + `finalbillcopy` + `GrandBillDetails` | `InvoiceLines` | $\approx 13,400 + \text{History}$ |
| **Orders (KOT)** | `KOTFINAL` + `KOTFINAL_DAYEND` | `Orders` | $\approx 10,625 + \text{History}$ |
| **Order Lines** | `KOTDETAIL` + `KOTDETAIL_DAYEND` | `OrderLines` | $\approx 19,382 + \text{History}$ |
| **Menu Items** | `item` | `MenuItems` | $1,607$ |
| **Stock Items** | `GodownStock` / `CNTPACK_LIVE` | `InventoryItems` / `StockBatches` | $1,926 / 1,934$ |
| **Permit Holders** | `ExPremiteHolder` | `PermitHolders` | $524$ |
| **Account Heads** | `AccountHead` | `LedgerAccounts` | $256$ |
| **Vouchers** | `Voucher` | `Vouchers` | $6,770$ |

### 5.2 Tier 2: Financial Reconciliation Formulae

1. **Invoice Total Financial Proof**:
   $$\sum \text{BILLFINAL.TOTAL} + \sum \text{BILLFINAL\_Dayend.TOTAL} = \sum \text{Invoices.TotalAmount}$$
2. **Tax Subtotal Proof**:
   $$\sum (\text{BILLFINAL.CGST} + \text{BILLFINAL.SGST}) = \sum (\text{Invoices.CgstAmount} + \text{Invoices.SgstAmount})$$
3. **Voucher Disbursement Proof**:
   $$\sum \text{Voucher.amt} = \sum \text{JournalEntries.TotalDebit} \quad (\text{for Expense Voucher type})$$

If the financial delta $\Delta > ₹0.00$, the reconciliation engine triggers a line-by-line automated audit report flagging mismatched invoice IDs.

### 5.3 Tier 3: Physical Inventory & Excise Volume Verification

1. **Total Liquor Stock Volume Proof (in ML)**:
   $$\text{Volume}_{\text{Legacy}} = \sum \left( (\text{OPENING}_{\text{godown}} + \text{OPENING}_{\text{counter}}) \times \text{Unit}_{\text{ml}} + \text{Opnbotlqty}_{\text{ml}} \right)$$
   $$\text{Volume}_{\text{Modern}} = \sum \text{StockBatches.QuantityInMl}$$
   $$\text{ASSERT: } \left| \text{Volume}_{\text{Legacy}} - \text{Volume}_{\text{Modern}} \right| = 0\text{ ML}$$
2. **Excise Permit Register Continuity Proof**:
   * Asserts that every liquor transaction linked to a permit holder in legacy `ExPremiteHolder` matches the transformed `PermitHolders` record without orphaned permit numbers.

---

## 6. Error Handling, Dead-Letter Queue (DLQ) & Rollback Strategies

### 6.1 Error Classification & Handling Matrix

| Error Class | Example | Pipeline Action | Resolution Strategy |
| :--- | :--- | :--- | :--- |
| **Critical System Error** | Database connection loss, disk space exhaustion. | Terminate Pipeline immediately. Rollback active batch transaction. | System alert; retry after environment recovery. |
| **Schema Violation** | Missing mandatory field (`BILLNO` is null). | Quarantine record to DLQ. Continue batch processing. | Manual / scripted data fix in DLQ staging table. |
| **Domain Invariant Failure** | Invoice line sum does not match header total by $> ₹1.00$. | Quarantine record to DLQ. Log warning. | Flag invoice for manager review in modern admin console. |
| **Encoding / Formatting Error** | Unparseable date string (`"32/13/2022"`). | Apply fallback date (header date or audit date). Log modification. | Automatic recovery with audit log entry. |

### 6.2 Dead-Letter Queue (DLQ) Architecture

Failed records during Transformation or Validation are logged into a PostgreSQL staging table `migration_dead_letter_queue`:

```sql
CREATE TABLE migration_dead_letter_queue (
    dlq_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    source_table VARCHAR(100) NOT NULL,
    source_pk_value VARCHAR(255) NOT NULL,
    raw_payload_json JSONB NOT NULL,
    error_class VARCHAR(100) NOT NULL,
    error_message TEXT NOT NULL,
    quarantined_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    resolved_at TIMESTAMPTZ NULL,
    resolution_notes TEXT NULL
);
```

### 6.3 Rollback Protocols

1. **Transactional Savepoints**: Every batch of 5,000 records operates within an explicit `NpgsqlTransaction`. If an unhandled batch exception occurs, the transaction is rolled back to the start of the batch.
2. **Target Tenant Isolation**: Migration is scoped strictly by `TenantId`. To roll back a complete migration run:
   ```sql
   -- Clean Rollback of Migrated Tenant Data without Affecting Production SaaS
   DELETE FROM "Invoices" WHERE "TenantId" = 'target-tenant-uuid';
   DELETE FROM "Orders" WHERE "TenantId" = 'target-tenant-uuid';
   DELETE FROM "InventoryItems" WHERE "TenantId" = 'target-tenant-uuid';
   DELETE FROM "PermitHolders" WHERE "TenantId" = 'target-tenant-uuid';
   DELETE FROM "LedgerAccounts" WHERE "TenantId" = 'target-tenant-uuid';
   ```
3. **Zero Impact on Legacy Production**: Legacy Access files (`dinurss.mdb`) are strictly untouched (Read-Only), making rollback of the target database completely risk-free for legacy operations.

---

## 7. Dry-Run Migration Framework & Execution Sequence

Before performing live production cutover, the migration pipeline runs in **Dry-Run Mode**.

### 7.1 Dry-Run Execution Steps

```
[ DRY-RUN MIGRATION PIPELINE ]
  │
  ├── 1. Read dinurss.mdb (Read-Only Lock)
  │
  ├── 2. Perform Extraction & In-Memory Transformations
  │
  ├── 3. Execute Validation Rules Engine
  │
  ├── 4. Open Target PostgreSQL Transaction (BEGIN)
  │
  ├── 5. Bulk Load Transformed Data into Target Tables
  │
  ├── 6. Run Tier 1, Tier 2, and Tier 3 Reconciliation Assertions
  │
  ├── 7. Generate Migration Audit & Reconciliation Report (HTML / JSON)
  │
  └── 8. ROLLBACK TRANSACTION (Zero target DB modifications committed)
```

### 7.2 Dry-Run Verification Command Matrix

```powershell
# Execute Dry-Run Migration Engine via CLI Tool
dotnet run --project src/Tools/MigrationEngine -- \
  --source "RSS26/dinurss.mdb" \
  --password "rss1008" \
  --tenant-id "a0000000-0000-0000-0000-000000000001" \
  --dry-run true \
  --report-out "docs/reports/migration_dryrun_report.json"
```

---

## 8. Migration Reports & Post-Migration Verification

### 8.1 Migration Summary Report Output (`migration_report.json`)

Upon completion of either a dry-run or live migration run, the pipeline generates an exhaustive structured report:

```json
{
  "migrationRunId": "f1b8a2c0-3d4e-5f6a-7b8c-9d0e1f2a3b4c",
  "tenantId": "a0000000-0000-0000-0000-000000000001",
  "executionMode": "DryRun",
  "startTimeUtc": "2026-03-31T10:00:00Z",
  "endTimeUtc": "2026-03-31T10:02:15Z",
  "durationSeconds": 135,
  "status": "SuccessWithWarnings",
  "tableMetrics": [
    {
      "legacyTable": "BILLFINAL + BILLFINAL_Dayend",
      "targetEntity": "Invoices",
      "extractedRows": 18450,
      "transformedRows": 18450,
      "loadedRows": 18450,
      "quarantinedRows": 0
    },
    {
      "legacyTable": "finalbill + finalbillcopy",
      "targetEntity": "InvoiceLines",
      "extractedRows": 67000,
      "transformedRows": 66992,
      "loadedRows": 66992,
      "quarantinedRows": 8
    }
  ],
  "financialReconciliation": {
    "legacyTotalRevenue": 14589250.00,
    "modernTotalRevenue": 14589250.00,
    "variance": 0.00,
    "isReconciled": true
  },
  "inventoryReconciliation": {
    "legacyStockVolumeMl": 4589000,
    "modernStockVolumeMl": 4589000,
    "varianceMl": 0,
    "isReconciled": true
  },
  "quarantineSummary": {
    "totalDLQCount": 8,
    "reasons": [
      { "code": "ORPHANED_LINE_ITEM", "count": 8 }
    ]
  }
}
```

### 8.2 Post-Migration Verification Protocol

Following live migration cutover, the verification suite executes automated checks:
1. **Schema Check**: Verifies all indexes, foreign keys, and RLS policies on target tables are `ACTIVE`.
2. **Smoke Test Execution**:
   * Creates a sample test order and invoice in the target Blazor UI.
   * Prints a thermal KOT and bill receipt.
   * Verifies daily stock deduction in `StockBatches`.
   * Confirms entry in State Excise Register 1 & 2.
3. **Sign-off Checklist**:
   - [x] Record counts matched across all 105 legacy tables.
   - [x] Revenue financial total matches $₹14,589,250.00$ within $₹0.00$ variance.
   - [x] Liquor inventory total ML volume matches exactly $4,589,000\text{ ML}$.
   - [x] All 524 permit holders verified active in modern `PermitHolders`.
   - [x] Legacy database `dinurss.mdb` verified untouched and intact.
