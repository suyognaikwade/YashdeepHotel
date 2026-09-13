# Development Environment, Commands, Standards & Operational Workflows

This document specifies the required local setup, development commands, build instructions, coding standards, git workflows, and troubleshooting procedures for the **Yashdeep Hotel Management System**.

---

## 1. Required Local Development Environment

To work on both legacy inspection and the modern .NET 9 cloud-synchronized platform, developers and AI agents require:

### 1.1 Core Development Tools
- **.NET 9 SDK**: Version `9.0.100` or higher (for modern C# 13, EF Core, and Blazor Hybrid / MAUI).
- **Python 3.10+**: Required for schema extraction tools and diagnostic scripts (`scratch/run_extract_schema.py`).
- **PowerShell 7+** (or 32-bit Windows PowerShell `SysWOW64` for Microsoft ACE OLEDB driver connection testing).
- **Git 2.40+**: Source control management.

### 1.2 Target Database Engines
- **PostgreSQL 16+**: Cloud / central multi-tenant database server.
- **SQLite 3.40+**: Local edge POS terminal database engine (embedded via EF Core).

### 1.3 Legacy Inspection Tools (Optional / Recommended)
- **dnSpy** / **ILSpy** (v7.0+): For interactive .NET 4.0 decompilation of `RSS26/RSS.exe`.
- **`mdbtools`** (Linux / WSL2): For native Linux extraction of Microsoft Access `.mdb` schemas.

---

## 2. Actual System Commands Matrix

The following table documents actual, tested commands used in this repository.

### 2.1 Database Extraction & Inspection Commands

```powershell
# 1. Test Access MDB connectivity using recovered password '<PRODUCTION_MDB_PASSWORD>'
powershell -Command "$c = New-Object System.Data.OleDb.OleDbConnection('Provider=Microsoft.ACE.OLEDB.12.0;Data Source=RSS26\dinurss.mdb;Jet OLEDB:Database Password=<PRODUCTION_MDB_PASSWORD>;'); $c.Open(); Write-Host 'SUCCESS!'; $c.Close()"

# 2. Re-run complete schema extraction to schema_extracted/
python scratch\run_extract_schema.py

# 3. Dump configuration tables (HotelInfo, Setup, Login)
python scratch\dump_hotel_configs.py

# 4. Extract legacy SQL queries from decompiled binary
python scratch\extract_sql.py

# 5. Linux / WSL2 schema export using mdbtools
bash extract_schema.sh

# 6. Windows PowerShell 32-bit schema export script
powershell -ExecutionPolicy Bypass -File extract_schema.ps1
```

### 2.2 Modern .NET 9 Development & Build Commands

```bash
# Restore NuGet package dependencies
dotnet restore

# Build modern solution in Debug mode
dotnet build

# Build modern solution in Release mode
dotnet build -c Release

# Run modern Web API project
dotnet run --project src/Yashdeep.Api/Yashdeep.Api.csproj

# Run Blazor Hybrid POS Client
dotnet run --project src/Yashdeep.Client.Blazor/Yashdeep.Client.Blazor.csproj

# Run Unit & Integration Tests
dotnet test

# Execute tests with code coverage report
dotnet test --collect:"XPlat Code Coverage"

# Format C# code according to .editorconfig rules
dotnet format

# Run EF Core Migration creation (PostgreSQL)
dotnet ef migrations add InitialCreate --project src/Yashdeep.Infrastructure --startup-project src/Yashdeep.Api

# Apply EF Core Migrations to PostgreSQL Cloud DB
dotnet ef database update --project src/Yashdeep.Infrastructure --startup-project src/Yashdeep.Api
```

---

## 3. Coding, Naming & Architecture Conventions

### 3.1 C# & .NET Standards
- Target **.NET 9** with C# 13 features enabled (`<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`).
- Use **Clean Architecture** layering: `Domain` -> `Application` -> `Infrastructure` / `LocalData` -> `Api` / `Client`.
- Use PascalCase for Class names, Method names, and Properties (`BillFinal`, `CalculateTaxAmount()`).
- Use camelCase for local variables and parameters (`tableNo`, `netAmount`).
- Use `_camelCase` for private readonly fields (`_dbContext`, `_logger`).

### 3.2 Domain Terminology Rules
Always use official Indian hospitality & excise domain terms in code:
- `Kot` / `KotDetail` (Kitchen Order Ticket)
- `Bot` / `BotDetail` (Bar Order Ticket)
- `Godown` / `GodownStock` (Warehouse storage)
- `CounterStock` (Sealed bottles behind bar)
- `LooseDispensary` / `Peg` (Open bottles dispensed in ml)
- `ExciseRegister` / `PermitHolder` (State Excise compliance)
- `DayEnd` / `BusinessDate` (Daily financial & stock closing)

### 3.3 Git & Branching Conventions
- Feature Branches: `feature/short-description` (e.g., `feature/blazor-touch-pos`).
- Bugfix Branches: `bugfix/short-description` (e.g., `bugfix/outbox-sync-retry`).
- Commit Messages: Capitalized summary line (< 50 chars), blank line, detailed body explaining *why*.

---

## 4. Definition of Done (DoD)

A feature or pull request is considered **Done** when:
1. Code compiles without errors or warnings.
2. Unit tests covering new domain rules and handlers pass cleanly (`dotnet test`).
3. Offline sync / Outbox queue behavior has been verified for edge scenarios.
4. Multilingual Devanagari script strings (e.g., `item.Marathi`) render properly in UI and QuestPDF bills.
5. Relevant documentation under `docs/` or `README.md` is updated.
6. Pre-commit validation steps have been executed.

---

## 5. Troubleshooting & Production Failure Diagnostics

### 5.1 Legacy Failure Modes (From 44 Historical Error Logs)

| Error Code / Symptom | Root Cause | Solution in Modern Stack |
| :--- | :--- | :--- |
| `0x80004005: Jet database engine stopped process` | SMB file sharing lock contention across multiple POS PCs accessing `dinurss.mdb`. | SQLite WAL mode per terminal + Outbox background HTTP sync to cloud PostgreSQL. |
| `0x80004005: Search key not found in any record` | Corrupted B-Tree indices in Access `.mdb` due to power cuts during write operations. | ACID compliant transactional engines with Write-Ahead Logging. |
| `0x80040E10: No value given for required parameters` | Empty UI inputs concatenated directly into dynamic SQL queries (`WHERE TABLE_NO=''`). | Strongly-typed EF Core parameterized queries with FluentValidation rules. |
| `0x80040E4D: Not a valid password` | Wrong database password passed during OleDb connection initialization. | Always supply recovered password `<PRODUCTION_MDB_PASSWORD>` when opening `RSS26/dinurss.mdb`. |

---

## 6. Dangerous Commands & Protected Operations

- Never execute `git reset --hard` without verifying uncommitted work.
- Never delete or modify files inside `RSS26/`.
- Never execute destructive `DROP TABLE` commands against production PostgreSQL databases.
