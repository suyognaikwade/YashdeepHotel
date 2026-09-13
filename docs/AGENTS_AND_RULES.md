# AI Agent Operations, Tasks, Commands & Rules

This document is the definitive guide for AI agents operating in the **YashdeepHotelMS** codebase. It defines agent personas, available automation tools, common maintenance tasks, CLI commands, and operational boundaries.

---

## 1. Agent Personas & Domain Roles

When acting in this repository, agents should adopt the appropriate persona based on the task:

1. **Domain Analyst / Reverse Engineer**:
   - Understands legacy VB.NET patterns, Jet 4.0 Access internals, Crystal Reports structure, and Indian hotel/bar operational practices (KOT, FL-III Excise, Day End).
2. **Database Migration Engineer**:
   - Translates Access Jet 4.0 tables, indices, and datatypes into robust PostgreSQL 16+ schemas and SQLite offline schemas with EF Core migrations.
3. **Full-Stack .NET Systems Architect**:
   - Modernizes legacy desktop logic into .NET 9 Clean Architecture, ASP.NET Core Web API, Blazor Hybrid POS terminals, and QuestPDF document generators.
4. **Offline Sync Specialist**:
   - Implements resilient edge-to-cloud synchronization using the Outbox Pattern, conflict resolution strategies, and idempotent endpoints.

---

## 2. Core Agent Rules (Non-Negotiable)

1. **Rule of Legacy Preservation**:
   - Never overwrite or modify original production assets under `RSS26/` (`RSS.exe`, `dinurss.mdb`, `OLD.mdb`, `nwitem5.rpt`). All work must produce new code or extract artifacts in designated directories.
2. **Rule of Database Credentials**:
   - When communicating with `RSS26/dinurss.mdb`, ALWAYS supply password `rss1008`.
   - When communicating with `RSS26/dinurss - Copy.mdb` or `OLD.mdb`, supply password `dinu`.
3. **Rule of Domain Terminology**:
   - Retain authentic hospitality domain terminology in code and documentation: `KOT`, `BOT`, `Godown`, `Counter Stock`, `Loose Stock`, `Peg`, `Day End`, `FL-III`, `Permit Holder`.
4. **Rule of Dual Language Support**:
   - Retain support for Marathi (Devanagari script UTF-8) for item names, kitchen slips, and bill headers.

---

## 3. Standard Agent Tasks & Workflows

### Task 1: Inspecting Compiled Legacy Code
To decompile, reflect, or inspect methods and types from `RSS.exe`:
- Run the PowerShell reflection script or inspection scripts in `scratch/inspect_core.py` and `scratch/inspect_forms.py`.
- For interactive decompilation, open `RSS26/RSS.exe` in **dnSpy** or **ILSpy**.

### Task 2: Querying or Extracting the Database
To extract schema, table lists, or row counts:
```powershell
# 1. Run full schema extraction to schema_extracted/
python scratch\run_extract_schema.py

# 2. Query any table directly using PowerShell ACE OLEDB
powershell -Command "
  $conn = New-Object System.Data.OleDb.OleDbConnection('Provider=Microsoft.ACE.OLEDB.12.0;Data Source=RSS26\dinurss.mdb;Jet OLEDB:Database Password=rss1008;');
  $conn.Open();
  $cmd = $conn.CreateCommand();
  $cmd.CommandText = 'SELECT TOP 10 * FROM [BILLFINAL]';
  $da = New-Object System.Data.OleDb.OleDbDataAdapter($cmd);
  $dt = New-Object System.Data.DataTable;
  $da.Fill($dt);
  $dt | Format-Table;
  $conn.Close();
"
```

### Task 3: Building PostgreSQL Migrations
The PostgreSQL DDL is available at [`schema_extracted/postgres_schema.sql`](file:///c:/xampp/htdocs/AntigravityProjects/YashdeepHotelMS/schema_extracted/postgres_schema.sql). When creating modern EF Core DbContext:
1. Map Access types to PostgreSQL (`TIMESTAMP` for Date/Time, `NUMERIC(18,2)` for Currency, `INTEGER` for Long, `VARCHAR`/`TEXT` for strings).
2. Add `TenantId` to all entities for SaaS multi-tenancy.
3. Replace the legacy `*_Dayend` table duplication with indexed temporal partition columns (`BusinessDate DATE`).

### Task 4: Modernizing Crystal Reports to QuestPDF
Crystal Reports (`.rpt`) should be migrated into C# QuestPDF components:
- `BILL.rpt` -> `BillDocument.cs` (80mm & 58mm thermal rolls)
- `KOT.rpt` / `KOT1.rpt` -> `KotDocument.cs` (Kitchen slip with Devanagari font)
- `RPTITEMSALE.rpt` -> `ItemSaleReportDocument.cs` (A4 management report)
- `VoucharReport.rpt` -> `VoucherReportDocument.cs`

---

## 4. Automation Scripts & Tools Registry

| Script / Tool | Location | Purpose |
| :--- | :--- | :--- |
| `run_extract_schema.py` | `scratch/` | Extracts all 105 tables and column metadata into PostgreSQL DDL |
| `dump_hotel_configs.py` | `scratch/` | Dumps `HotelInfo`, `Setup`, and `SoftwareName` configuration |
| `extract_sql.py` | `scratch/` | Extracts all 863 inline SQL queries and 98 tables from `RSS.exe` |
| `inspect_core.py` | `scratch/` | Inspects fields, properties, and methods of core classes |
| `inspect_forms.py` | `scratch/` | Inspects UI controls, handlers, and logic in key forms |
| `verify_pw.py` | `scratch/` | Verifies and decodes Jet 4.0 password hash algorithms |
| `extract_schema.ps1` | Root | PowerShell script for 32-bit ACE database dumping |
| `extract_schema.sh` | Root | Linux / WSL2 bash script for `mdbtools` |

---

## 5. Modern Solution Directory Convention

When generating new modern source code, agents should follow this standard structure:

```
YashdeepHotelMS/
├── src/
│   ├── Yashdeep.Domain/            # Entities, Enums, Value Objects, Domain Events
│   ├── Yashdeep.Application/       # CQRS Commands, Queries, DTOs, Validators, Interfaces
│   ├── Yashdeep.Infrastructure/    # PostgreSQL EF Core DbContext, Cloud Repositories
│   ├── Yashdeep.LocalData/         # SQLite EF Core DbContext, Local Cache
│   ├── Yashdeep.SyncEngine/        # Outbox Processor, Conflict Resolver, Background Worker
│   ├── Yashdeep.Reports/           # QuestPDF Document Templates (Bills, KOTs, Statements)
│   ├── Yashdeep.Hardware/          # ESC/POS Thermal Printing, USB/Network/Serial Drivers
│   ├── Yashdeep.Api/               # ASP.NET Core 9 Web API, Controllers, SignalR Hubs
│   ├── Yashdeep.Client.Blazor/     # Blazor WebAssembly / Hybrid POS UI Components
│   └── Yashdeep.Migration/         # Access MDB to PostgreSQL Data Ingestion Utilities
├── tests/
│   ├── Yashdeep.Domain.Tests/
│   ├── Yashdeep.Application.Tests/
│   └── Yashdeep.SyncEngine.Tests/
└── docs/                           # Architecture, Schemas, Domain Logic, Agent Rules
```
