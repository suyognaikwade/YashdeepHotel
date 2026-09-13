# Yashdeep Hotel MS - AI Agent Guidelines, Workflows & Operational Context

Welcome, AI Agent! This document is your primary onboarding guide to the **Yashdeep Hotel Management System (RSS / Real Soft)** codebase and modernization project.

---

## 1. AI Agent Quick Start

Before beginning any task in this repository, you **must** read and understand the following documents:

1. [**`README.md`**](README.md): Master project overview, architecture, business domain, and quick commands.
2. [**`docs/ARCHITECTURE.md`**](docs/ARCHITECTURE.md): Technical breakdown of the legacy monolith (`RSS.exe` / Jet 4.0) vs. target modern SaaS architecture (.NET 9 + Blazor + SQLite + PostgreSQL + Outbox Sync).
3. [**`docs/BUSINESS_LOGIC.md`**](docs/BUSINESS_LOGIC.md): Indian hotel & Maharashtra State Excise FL-III bar domain workflows (KOT/BOT, differential section pricing, multi-tier stock, Day End).
4. [**`docs/DATABASE_SCHEMA.md`**](docs/DATABASE_SCHEMA.md): Complete schema reference for all 105 user tables.
5. [**`docs/DEVELOPMENT_AND_WORKFLOWS.md`**](docs/DEVELOPMENT_AND_WORKFLOWS.md): Setup, actual terminal commands, testing, coding standards, and troubleshooting.
6. [**`docs/CONFIGURATION_AND_ENV.md`**](docs/CONFIGURATION_AND_ENV.md): Environment variables, configuration management, security, and hardware protocols.

---

## 2. Critical Credentials & System Constants

| Asset / Parameter | Value / Placeholder | Details |
| :--- | :--- | :--- |
| **Primary Database File** | `RSS26/dinurss.mdb` | 22.5 MB production database (105 user tables) |
| **`dinurss.mdb` Password** | `rss1008` | Recovered from Jet 4.0 header XOR mask |
| **Backup Databases** | `dinurss - Copy.mdb`, `OLD.mdb` | Password: `dinu` |
| **Default App Logins** | `Admin` / `333`<br>`ADMIN` / `admin` | Found in `Login` table |
| **Master Date Lock PW** | `333` / dynamic master | Found in `dateLckMaster` |
| **Hotel Trade Name** | `HOTEL YASHDEEP` | Location: Bhenda / Nanded, Maharashtra |
| **State Excise License** | `FL III-2151444022D8ADF7` | Maharashtra State Excise FL-III Hotel/Club License |
| **VAT TIN** | `27900111779v` | State Code 27 (Maharashtra) |
| **Admin Contact** | `7741870808` / `Fahadsayyed92@gmail.com` | Configured in `HotelInfo` |

*Note: Never introduce hardcoded plain-text passwords in new source code. Use ASP.NET Core Identity with bcrypt / PBKDF2 or secure environment secrets.*

---

## 3. Directory Map & Component Responsibilities

```
YashdeepHotelMS/
├── AGENTS.md                          # Quick AI agent onboarding & rules (this file)
├── README.md                          # Master project documentation & system overview
├── DECOMPILATION_GUIDE.md             # Reverse-engineering manual & decompilation tool guide
├── PROJECT_ANALYSIS.md                # Initial binary analysis & stack findings
├── extract_schema.ps1                 # Windows PowerShell 32-bit ACE schema extraction script
├── extract_schema.sh                  # Linux / WSL2 mdbtools schema extraction script
├── .agent/
│   └── rules/
│       └── hotel_ms_rules.md          # Workspace rules for AI assistants
├── docs/
│   ├── ARCHITECTURE.md                # Legacy WinForms architecture & target modernization
│   ├── DATABASE_SCHEMA.md             # Comprehensive schema reference (105 tables)
│   ├── BUSINESS_LOGIC.md              # Domain workflows: Table, KOT, Bill, Stock, Excise, DayEnd
│   ├── AGENTS_AND_RULES.md            # Comprehensive AI agent workflows, rules & safety
│   ├── DEVELOPMENT_AND_WORKFLOWS.md   # Setup, commands, testing, conventions & troubleshooting
│   └── CONFIGURATION_AND_ENV.md       # Config, env vars, hardware printing & error handling
├── schema_extracted/
│   ├── DATABASE_SCHEMA.md             # Extracted tables & columns summary
│   ├── postgres_schema.sql            # Full PostgreSQL DDL for all 105 tables
│   └── tables_inventory.csv           # Table inventory with row & column counts
└── RSS26/                             # Legacy production deployment (PROTECTED DIRECTORY)
    ├── RSS.exe                        # Primary WinForms executable (.NET 4.0)
    ├── RSS_LONGLIFE.exe               # Variant executable for long-life billing
    ├── RSSUTILITYNEW.exe              # Utility executable tool
    ├── dinurss.mdb                    # Production database (Password: rss1008)
    ├── dinurss - Copy.mdb             # Backup database (Password: dinu)
    ├── OLD.mdb                        # Historical database (Password: dinu)
    ├── nwitem5.rpt / nwitem5.vb       # Item sales Crystal Report & VB.NET wrapper
    ├── Log/                           # 44 monthly error logs from 2022 to 2026
    └── PDF/                           # Sample generated PDF reports
```

---

## 4. Protected Assets & Safety Directives

### 4.1 Files and Directories That MUST NOT Be Modified
- `RSS26/RSS.exe`, `RSS26/RSS_LONGLIFE.exe`, `RSS26/RSSUTILITYNEW.exe`: Legacy executables.
- `RSS26/dinurss.mdb`, `RSS26/dinurss - Copy.mdb`, `RSS26/OLD.mdb`: Legacy production Access databases.
- `RSS26/*.rpt`: Legacy Crystal Report templates.
- `RSS26/Log/*`: Historical production error logs.

### 4.2 Dangerous Operations That MUST NOT Be Executed Casually
- `git reset --hard` without verifying uncommitted work.
- Overwriting or truncating existing schema SQL (`schema_extracted/postgres_schema.sql`).
- Directly editing auto-generated artifacts without modifying the underlying source or generator script.

---

## 5. Repository AI Agent Rules

1. **Protect Legacy Assets**: Never modify or overwrite original legacy binary files (`RSS26/*.exe`, `RSS26/*.mdb`, `RSS26/*.rpt`).
2. **Preserve Indian Restaurant & Bar Terminology**: Keep domain concepts intact in all models and documentation:
   - **KOT**: Kitchen Order Ticket
   - **BOT**: Bar Order Ticket (Bar KOT)
   - **FL-III**: Maharashtra Foreign Liquor Hotel & Club License
   - **Peg / Unit**: Liquor dispensing units (30ml, 60ml, 90ml, 180ml / Nip, 375ml / Pint, 750ml / Quart)
   - **Godown**: Central bulk warehouse / storage room
   - **Counter**: Bar / Dispensing counter
   - **Day End**: Daily closing audit where active tables move to `*_Dayend` and counters reset
3. **Database Access**:
   - Always use password `rss1008` when accessing `RSS26/dinurss.mdb`.
   - Modern target stack must use **PostgreSQL** for cloud and **SQLite** for edge POS terminals.
4. **Target Modern Stack**:
   - Backend / API: .NET 9 Web API + EF Core + PostgreSQL
   - Local / Offline: SQLite + EF Core + Outbox Pattern Sync Engine
   - Client: Blazor Hybrid (MAUI) for Desktop & Tablets, responsive web for Admin
   - Reporting: QuestPDF (code-first, thermal printer ESC/POS friendly)

---

## 6. AI Agent Task Workflows

### 6.1 General Task Workflow
1. **Understand Scope**: Read relevant domain documents (`BUSINESS_LOGIC.md`, `ARCHITECTURE.md`, `DATABASE_SCHEMA.md`).
2. **Inspect Implementation**: Inspect existing code or scripts before proposing changes.
3. **Trace Dependencies**: Identify affected entities, queries, or UI handlers.
4. **Implement Smallest Correct Change**: Keep changes modular, well-typed, and aligned with Clean Architecture.
5. **Run Validation**: Execute verification commands or test scripts.
6. **Update Documentation**: Update documentation if architectural behavior or entity schemas change.

### 6.2 Specific AI Agent Task Workflows

#### Workflow: Investigating and Implementing a Task
- Trace the requirement to legacy database tables or decompiled forms (`RSS.exe` / `dinurss.mdb`).
- Verify domain rules in `docs/BUSINESS_LOGIC.md`.
- Implement clean, strongly-typed .NET 9 / C# 13 code in `src/`.

#### Workflow: Debugging a Production Failure Mode
- Search historical error logs (`RSS26/Log/`).
- Diagnose the root cause (e.g., Access Jet lock `0x80004005`, missing parameters `0x80040E10`).
- Ensure modern solution eliminates the flaw (e.g. replacing Access Jet with PostgreSQL / SQLite + WAL).

#### Workflow: Adding a Feature
- Define Domain entities and EF Core DbContext mappings.
- Implement CQRS handlers (MediatR) and FluentValidation.
- Build Blazor Hybrid UI components and QuestPDF report components where applicable.
- Add unit and integration tests under `tests/`.

#### Workflow: Database & Migration Changes
- Update PostgreSQL DDL (`schema_extracted/postgres_schema.sql`) or EF Core migration files.
- Ensure all tables include `TenantId` for multi-tenancy and timestamp columns for temporal tracking.
- Test data transformation pipelines.

---

## 7. Useful Agent Commands Matrix

```powershell
# 1. Test Access MDB database connection with recovered password
powershell -Command "$c = New-Object System.Data.OleDb.OleDbConnection('Provider=Microsoft.ACE.OLEDB.12.0;Data Source=RSS26\dinurss.mdb;Jet OLEDB:Database Password=rss1008;'); $c.Open(); Write-Host 'Connected Successfully!'; $c.Close()"

# 2. Extract database schema and tables inventory (Linux / WSL)
bash extract_schema.sh

# 3. Extract database schema using PowerShell (Windows)
powershell -ExecutionPolicy Bypass -File extract_schema.ps1

# 4. Check git branch and status
git status
git log -n 5 --oneline
```
