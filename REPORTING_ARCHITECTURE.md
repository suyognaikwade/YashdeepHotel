# Yashdeep Hotel MS - Reporting Architecture Specification

> **Module**: Legacy Crystal Reports Replacement, QuestPDF Code-First Engine, Operational Reports, Management Analytics, Maharashtra State Excise (FL-III) Statutory Registers, Historical Archival & Multi-Format Exports
> **Status**: Approved Architectural Specification
> **Target Platform**: .NET 9 Web API, QuestPDF, ClosedXML / EPPlus, EF Core, SQLite, PostgreSQL 16

---

## 1. Executive Summary & Legacy Crystal Reports Strategy

### 1.1 Legacy Crystal Reports Architecture Analysis
In the legacy VB.NET application (`RSS26`), reporting was driven by **Crystal Reports 13** (`CrystalDecisions.CrystalReports.Engine`) using 34 `.rpt` report templates (such as `nwitem5.rpt`). Data was pushed via dynamic ADO.NET DataTables populated by inline SQL queries in `ClassDB.vb`.

```
Legacy Reporting Pipeline (Deprecated)
┌──────────────────────┐     ADO.NET OleDb      ┌─────────────────────────┐     Windows COM Spool    ┌──────────────────────┐
│  Access MDB Tables   ├───────────────────────►│ Crystal Reports Engine  ├─────────────────────────►│ Crystal Viewer / PDF │
│   (dinurss.mdb)      │   (Inline SQL Query)   │  (nwitem5.rpt / .NET 4) │   (Windows Only COM)    │   (Desktop Form)     │
└──────────────────────┘                        └─────────────────────────┘                          └──────────────────────┘
```

### 1.2 Flaws & Reasons for Replacing Crystal Reports

1. **Platform Lock-In**: Requires Windows OS COM runtime (`CRyReport.msi`); cannot run natively on Linux cloud servers, Docker containers, or mobile Android/iOS POS tablets.
2. **Brittle Data Bindings**: Report templates were bound to rigid 32-bit Access ODBC/OleDb schema definitions, causing runtime crashes (`0x80040E10`) whenever database columns were altered.
3. **High Resource Overhead**: Crystal Reports engine loaded up to 150 MB of memory per report instance, severely slowing down daily closing report compilation.
4. **Poor Code Maintainability**: Business logic was split between VB.NET code-behind files and formula fields inside GUI `.rpt` files.

### 1.3 Target Modern Strategy: QuestPDF + ClosedXML Engine

```
Target Modern Reporting Architecture
┌──────────────────────┐     EF Core / LINQ     ┌─────────────────────────┐     QuestPDF DSL Stream  ┌──────────────────────┐
│ PostgreSQL / SQLite  ├───────────────────────►│  Reporting Core Service ├─────────────────────────►│ Vector PDF Document  │
│  (Temporal Tables)   │   (Strongly Typed)     │  (QuestPDF / C# Code)   │   (Cross-Platform)     │ (A4 / 80mm / Thermal)│
└──────────────────────┘                        └────────────┬────────────┘                          └──────────────────────┘
                                                             │                  ClosedXML Stream     ┌──────────────────────┐
                                                             └──────────────────────────────────────►│ Excel (.xlsx) / CSV  │
                                                                                                     └──────────────────────┘
```

---

## 2. Operational Reports Architecture

Operational reports assist shift supervisors and cashiers in managing real-time restaurant flow, seating, and daily cash balancing.

### 2.1 Operational Reports Inventory

| Report Name | Legacy Form / Source | Target C# Service / Component | Purpose & Description | Output Formats |
| :--- | :--- | :--- | :--- | :--- |
| **Live Table & KOT Status** | `FRM_CALLTBL`, `KOTDETAIL` | `TableStatusReport` | Real-time map of vacant, occupied, and billed tables with unbilled KOT totals. | Web UI / 80mm Slip |
| **Daily Shift Cash Balancing** | `CounterCashDisplay`, `CashBookOpen` | `CashierShiftBalanceReport` | Opening cash, total cash sales, card/UPI totals, voucher expenses, net counter drawer balance. | 80mm Thermal / PDF |
| **Item-Wise Sales Summary** | `nwitem5.rpt`, `nwitem5.vb` | `ItemSalesSummaryReport` | Quantity and revenue breakdown per menu SKU across Food and Bar departments. | A4 PDF / Excel |
| **Waiter Sales Performance** | `Waiter`, `BILLFINAL` | `WaiterPerformanceReport` | Order counts, average ticket size, and total sales generated per waitstaff member. | A4 PDF / Excel |
| **Cancelled KOT / Void Audit** | `CancelKot` | `VoidAuditReport` | Detailed audit of cancelled order lines, waiter IDs, timestamp, and manager approval codes. | A4 PDF / Excel |

### 2.2 QuestPDF Item Sales Code Example

```csharp
namespace Yashdeep.Infrastructure.Reporting.Templates;

public class ItemSalesSummaryDocument : IDocument
{
    private readonly ItemSalesSummaryModel _model;

    public ItemSalesSummaryDocument(ItemSalesSummaryModel model) => _model = model;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(20);
            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeTable);
            page.Footer().AlignCenter().Text(x => x.CurrentPageNumber());
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(_model.HotelName).FontSize(18).Bold();
                col.Item().Text($"Item Sales Report: {_model.StartDate:dd/MM/yyyy} to {_model.EndDate:dd/MM/yyyy}");
            });
        });
    }

    private void ComposeTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(40);
                cols.RelativeColumn(3);
                cols.RelativeColumn(1);
                cols.RelativeColumn(1);
            });

            table.Header(header =>
            {
                header.Cell().Text("#").Bold();
                header.Cell().Text("Item Name").Bold();
                header.Cell().Text("Qty").Bold();
                header.Cell().Text("Amount (₹)").Bold();
            });

            foreach (var item in _model.Items)
            {
                table.Cell().Text(item.SrNo.ToString());
                table.Cell().Text(item.ItemName);
                table.Cell().Text(item.Quantity.ToString());
                table.Cell().Text($"{item.TotalAmount:F2}");
            }
        });
    }
}
```

---

## 3. Management Analytics Suite

Management reports provide executive visibility into profit margins, section performance, and tax liabilities.

### 3.1 Management Analytics Inventory

| Report Name | Legacy Form / Source | Target C# Service / Component | Key Metrics & Dimensions |
| :--- | :--- | :--- | :--- |
| **Section Revenue Analysis** | `sectionwise_temp`, `TABLE_NO_GROP` | `SectionRevenueReport` | Revenue split across Family, AC, Hall, Garden, and Parcel sections. |
| **Department Margin Split** | `finalbill`, `ItemDept` | `DepartmentContributionReport` | Food vs. Liquor gross revenue, cost ratio, and margin contribution. |
| **Peak Dining Velocity** | `BILLFINAL.INTIME`, `TIME` | `PeakHourAnalyticsReport` | Hourly order density, table turnaround time, and seating occupancy rate. |
| **Discount & Promotion Audit** | `BILLFINAL.DISCOUNT` | `DiscountAuditReport` | Total discounts granted per cashier/manager with reason code distribution. |
| **GST Tax Liability Report** | `BILLFINAL.CGST`, `SGST` | `GstTaxLiabilityReport` | Taxable turnover, CGST 2.5%, SGST 2.5%, and net GST payable output statement. |

---

## 4. Maharashtra State Excise (FL-III Bar) Regulatory Reports

Under the Maharashtra Prohibition Act and FL-III License requirements (`FL III-2151444022D8ADF7`), statutory registers must be generated for State Excise inspection.

```
                           [ Maharashtra State Excise Reports ]
                                            │
         ┌──────────────────────────────────┼──────────────────────────────────┐
         ▼                                  ▼                                  ▼
┌─────────────────────────┐        ┌─────────────────────────┐        ┌─────────────────────────┐
│ Permit Holder Register  │        │ Daily Bulk Litre Stmt   │        │ Monthly Register 1 Return│
│ (`ExPremiteHolder`)     │        │ (`FrmDailyBulkLitre`)   │        │ (`ExciseMonthlyStat`)   │
│ - Guest Permit License  │        │ - Volume in Bulk Litres │        │ - Opening Stock (1st)   │
│ - Liquor Dispensed      │        │ - IMFL / Beer / Wine    │        │ - TP Receipts & Sales   │
│ - Date & Invoice Ref    │        │ - Daily Reconciliation  │        │ - Closing Audit Balance │
└─────────────────────────┘        └─────────────────────────┘        └─────────────────────────┘
```

### 4.1 Statutory Excise Registers Specification

1. **Permit Holder Register (`ExPremiteHolder`)**:
   - Records every liquor transaction against the customer's State Liquor Permit Number.
   - Mandated columns: Date, Invoice No, Guest Name, Permit No, Expiry Date, Volume Dispensed (ML), Amount.
2. **Daily Bulk Litre Statement (`FrmDailyBulkLitre`)**:
   - Converts bottle and peg sales into absolute Bulk Litres (BL) for IMFL (Spirits), Beer, Country Liquor, and Wine.
   - Calculation: $\text{Total BL} = \sum (\text{Qty Sold} \times \text{Bottle Size ML}) / 1000$.
3. **Monthly Statement & Register 1 (`ExciseMonthlyStat`)**:
   - Official monthly inspection return submitted to the Excise Inspector on the 1st of every month.
   - Reconciles Opening Balance + Inward Transport Permits (TP) - Sales - Breakage = Closing Balance per Brand SKU.

---

## 5. Historical Archival & Unified Temporal Querying

Legacy system moved active rows to `*_Dayend` tables at closing, breaking historical queries. The modern target architecture unifies active and archived data using **PostgreSQL Temporal Partitioning & Unified Views**.

```sql
-- Unified SQL View bridging active and historical transactions seamlessly
CREATE OR REPLACE VIEW view_unified_bills AS
SELECT
    id, tenant_id, invoice_number, table_number, section,
    sub_total, total_discount, total_tax, net_payable,
    settled_at_utc, 'ACTIVE' AS storage_tier
FROM bills
WHERE is_archived = FALSE

UNION ALL

SELECT
    id, tenant_id, invoice_number, table_number, section,
    sub_total, total_discount, total_tax, net_payable,
    settled_at_utc, 'HISTORICAL' AS storage_tier
FROM bills_history;
```

---

## 6. Offline Report Generation & Multi-Format Exports

### 6.1 Offline Edge POS Rendering
- **QuestPDF** runs entirely in-process on the local POS terminal without needing internet connectivity or external cloud services.
- Generated report PDFs are stored locally in SQLite blob storage (`ReportCache`) for immediate viewing or thermal re-printing.

### 6.2 ClosedXML Multi-Format Export Engine
In addition to PDF, reports can be exported to Excel (`.xlsx`), CSV, and JSON using ClosedXML for accounting integrations (such as Tally / Zoho Books).

```csharp
public async Task<byte[]> ExportItemSalesToExcelAsync(ItemSalesSummaryModel data)
{
    using var workbook = new XLWorkbook();
    var worksheet = workbook.Worksheets.Add("Item Sales");

    worksheet.Cell(1, 1).Value = "Item Name";
    worksheet.Cell(1, 2).Value = "Quantity";
    worksheet.Cell(1, 3).Value = "Total Revenue (INR)";

    int row = 2;
    foreach (var item in data.Items)
    {
        worksheet.Cell(row, 1).Value = item.ItemName;
        worksheet.Cell(row, 2).Value = item.Quantity;
        worksheet.Cell(row, 3).Value = item.TotalAmount;
        row++;
    }

    using var stream = new MemoryStream();
    workbook.SaveAs(stream);
    return stream.ToArray();
}
```
