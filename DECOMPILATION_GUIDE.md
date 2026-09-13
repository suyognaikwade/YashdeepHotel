# Quick Reference: Decompilation & Database Extraction

## Decompile RSS.exe (Windows)

### Option 1: dnSpy (Recommended)
```powershell
# Download from: https://github.com/dnSpy/dnSpy/releases/latest
# Extract and run dnSpy.exe
# File → Open → RSS26\RSS.exe
# Right-click "RSS" assembly → "Export to Project"
# Choose VB.NET output, save to folder
```

### Option 2: ILSpy
```powershell
# Download from: https://github.com/icsharpcode/ILSpy/releases
# Open RSS.exe → Save as project (C# or VB)
```

### Option 3: dotPeek (JetBrains)
```powershell
# Download from: https://www.jetbrains.com/decompiler/
# Open RSS.exe → Export to project
```

---

## Key Types to Examine First

| Type | Why |
|------|-----|
| `RSS.ClassDB` | All database access logic |
| `RSS.RSS_MDI` | Main form, startup logic |
| `RSS.Forms.*` | 100+ forms - billing, KOT, inventory |
| `RSS.My.MyProject` | Application settings, connection strings |

---

## Search Terms in Decompiler

```text
# Database
"TABLE_NO"
"dinurss"
"dinu"
"GetDataTable"
"OleDbConnection"
"ConnectionString"

# Business Logic
"Bill"
"KOT"
"Stock"
"Item"
"Sale"
"Purchase"
"Waiter"
"Table"

# Reports
"CrystalDecisions"
"ReportDocument"
"ExportToDisk"
"PrintToPrinter"
```

---

## Database Schema Extraction (Requires 32-bit Access/ACE)

### Method 1: Microsoft Access (32-bit)
1. Install **32-bit Microsoft Access** or **32-bit Access Database Engine**
2. Open `dinurss.mdb` in Access
3. Enter password: `dinu` (from binary strings)
4. External Data → Export → Text File / XML / SQL Server

### Method 2: mdbtools (Linux/WSL2) - Recommended
```bash
# On Ubuntu/WSL2:
sudo apt-get install mdbtools

# List tables
mdb-tables -1 dinurss.mdb

# Export schema (with password)
mdb-schema dinurss.mdb postgres > schema.sql

# Export data
mdb-export -D '%Y-%m-%d' dinurss.mdb TABLE_NAME > table_name.csv

# Export all tables
for table in $(mdb-tables -1 dinurss.mdb); do
  mdb-export -D '%Y-%m-%d' dinurss.mdb "$table" > "${table}.csv"
done
```

### Method 3: PowerShell with 32-bit ACE (if installed)
```powershell
# Must run in 32-bit PowerShell: C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe
$connStr = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=C:\path\dinurss.mdb;Jet OLEDB:Database Password=dinu;"
$conn = New-Object System.Data.OleDb.OleDbConnection($connStr)
$conn.Open()
$schema = $conn.GetSchema("Tables")
$schema | Select TABLE_NAME | Export-Csv tables.csv
$conn.Close()
```

---

## Expected Database Tables (from error logs & form names)

| Table Pattern | Likely Purpose |
|---------------|----------------|
| `TABLE_*` | Restaurant tables |
| `ITEM*` / `MENU*` | Menu items |
| `BILL*` / `SALE*` | Billing/sales |
| `KOT*` | Kitchen orders |
| `STOCK*` / `INVENTORY*` | Inventory |
| `WAITER*` / `EMPLOYEE*` | Staff |
| `PURCHASE*` | Purchasing |
| `LEDGER*` / `ACCOUNT*` | Accounting |
| `COUNTER*` | Counter/terminal |
| `DEPT*` / `SECTION*` | Departments |

---

## Crystal Reports Migration Notes

### Reports to Migrate (from binary strings)
| Report File | Priority | Suggested Replacement |
|-------------|----------|----------------------|
| `BILL.rpt` | Critical | QuestPDF template |
| `KOT.rpt`, `KOT1.rpt` | Critical | QuestPDF (thermal printer) |
| `RPTITEMSALE*.rpt` | High | QuestPDF + Blazor report viewer |
| `RPTDEPTTOTALSALE*.rpt` | High | QuestPDF |
| `RPTCOUNTER*.rpt` | Medium | QuestPDF |
| `RPTGODOWNSTOCK*.rpt` | Medium | QuestPDF |
| `VoucharReport.rpt` | Medium | QuestPDF |
| `PaymentReport.rpt` | Medium | QuestPDF |
| `ReceiptReport.rpt` | Medium | QuestPDF |
| `nwitem5.rpt` | Low | QuestPDF (has VB wrapper) |

### QuestPDF Example (Bill Template)
```csharp
// QuestPDF bill generation
public class BillDocument : IDocument
{
    private readonly BillModel _bill;
    
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    
    public void Compose(IDocumentContainer container)
    {
        container.Page(page => {
            page.Margin(20);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Consolas"));
            
            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }
    
    void ComposeHeader(IContainer c) { /* Hotel name, address, GST */ }
    void ComposeContent(IContainer c) { /* Items table, totals */ }
    void ComposeFooter(IContainer c) { /* Terms, thank you, QR code */ }
}
```

---

## Sync Engine: Outbox Pattern (Recommended)

```csharp
// Local database (SQLite) - Outbox table
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string EntityType { get; set; }  // "TableOrder", "StockAdjustment"
    public string EntityId { get; set; }
    public string Operation { get; set; }   // "Create", "Update", "Delete"
    public string PayloadJson { get; set; } // Serialized entity
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
}

// Background sync service
public class SyncBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await ProcessOutboxAsync(ct);
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }
    
    async Task ProcessOutboxAsync(CancellationToken ct)
    {
        var messages = await _db.OutboxMessages
            .Where(m => m.SentAt == null && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(ct);
            
        foreach (var msg in messages)
        {
            try 
            {
                await _apiClient.SyncEntityAsync(msg.EntityType, msg.EntityId, 
                    msg.Operation, msg.PayloadJson, ct);
                msg.SentAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                msg.RetryCount++;
                msg.ErrorMessage = ex.Message;
            }
            await _db.SaveChangesAsync(ct);
        }
    }
}
```

---

## Project Structure for Rewrite

```
YashdeepHotelMS/
├── src/
│   ├── Yashdeep.Core/              # Domain models, interfaces
│   ├── Yashdeep.Client/            # MAUI/Blazor/WinForms client
│   │   ├── Yashdeep.Client.WinForms/  # Legacy-compatible
│   │   ├── Yashdeep.Client.MAUI/      # Cross-platform
│   │   └── Yashdeep.Client.Blazor/    # Web
│   ├── Yashdeep.LocalData/         # SQLite + EF Core (offline)
│   ├── Yashdeep.Sync/              # Outbox pattern, conflict resolution
│   ├── Yashdeep.Api/               # ASP.NET Core Web API
│   │   ├── Controllers/
│   │   ├── Services/
│   │   └── MultiTenancy/
│   ├── Yashdeep.Reports/           # QuestPDF templates
│   └── Yashdeep.Migration/         # Access → PostgreSQL tools
├── tests/
├── deploy/
│   ├── docker-compose.yml
│   ├── kubernetes/
│   └── scripts/
└── docs/
```

---

## Immediate Action Items

| Task | Command/Action | Owner |
|------|----------------|-------|
| 1. Decompile RSS.exe | Run dnSpy, export to `RSS_Decompiled/` | Dev |
| 2. Extract DB schema | Use WSL2 + mdbtools on `dinurss.mdb` | Dev/DBA |
| 3. Document all SQL queries | Search decompiled code for `SELECT|INSERT|UPDATE|DELETE` | Dev |
| 4. Map forms to features | List from resource names → business capability | BA/Dev |
| 5. Create PostgreSQL schema | From extracted Access schema | DBA |
| 6. Prototype QuestPDF bill | Replace `BILL.rpt` first | Dev |

---

## Useful Links

- [dnSpy Releases](https://github.com/dnSpy/dnSpy/releases)
- [mdbtools](https://github.com/mdbtools/mdbtools)
- [QuestPDF](https://www.questpdf.com/)
- [EF Core SQLite](https://learn.microsoft.com/ef/core/providers/sqlite/)
- [ASP.NET Core Multi-tenancy](https://learn.microsoft.com/aspnet/core/architecture/multi-tenancy)
- [Blazor Hybrid (MAUI)](https://learn.microsoft.com/aspnet/core/blazor/hybrid)