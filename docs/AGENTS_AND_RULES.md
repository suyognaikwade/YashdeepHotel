# Comprehensive AI Agent Operations, Workflows, Commands & Safety Rules

This document is the master operational specification for AI coding agents working on the **Yashdeep Hotel Management System (YashdeepHotelMS)** codebase.

---

## 1. AI Agent Quick Start

When joining this repository as an AI agent, perform the following initialization steps before proposing or implementing changes:

1. **Read Core Specifications**:
   - [`README.md`](../README.md): Primary project entrypoint and high-level architecture.
   - [`docs/ARCHITECTURE.md`](ARCHITECTURE.md): System architecture (Legacy VB.NET/Access Jet 4.0 vs. Target .NET 9 Blazor Hybrid + PostgreSQL/SQLite SaaS).
   - [`docs/BUSINESS_LOGIC.md`](BUSINESS_LOGIC.md): Domain workflows (Dining sections, KOT routing, split taxes, dynamic UPI, multi-tier stock, FL-III Excise compliance, Day End).
   - [`docs/DATABASE_SCHEMA.md`](DATABASE_SCHEMA.md): Complete schema reference for all 105 user tables.
   - [`docs/DEVELOPMENT_AND_WORKFLOWS.md`](DEVELOPMENT_AND_WORKFLOWS.md): Setup, commands, testing, conventions, and troubleshooting.
   - [`docs/CONFIGURATION_AND_ENV.md`](CONFIGURATION_AND_ENV.md): Configuration rules, environment variables, security, and hardware printing protocols.

2. **Verify Credentials & Database Parameters**:
   - Production Database: `RSS26/dinurss.mdb` (Password: `<PRODUCTION_MDB_PASSWORD>`)
   - Backup Databases: `dinurss - Copy.mdb`, `OLD.mdb` (Password: `<BACKUP_MDB_PASSWORD>`)
   - Default Application Logins: `Admin` / `<DEFAULT_ADMIN_PASSWORD>` or `ADMIN` / `<DEFAULT_ADMIN_PASSWORD>`

3. **Verify Environment Setup**:
   - Confirm file integrity of `RSS26/` legacy binaries.
   - Confirm presence of `schema_extracted/postgres_schema.sql` (105 tables).

---

## 2. Core Agent Rules & Safety Constraints

### Rule 1: Protection of Legacy Production Assets
- **`RSS26/` Directory**: Contains compiled binaries (`RSS.exe`, `RSS_LONGLIFE.exe`, `RSSUTILITYNEW.exe`), Access Jet 4.0 databases (`dinurss.mdb`), Crystal Reports (`.rpt`), and historical error logs (`Log/`).
- **Constraint**: NEVER overwrite, edit, or delete files in `RSS26/`. All modernization code must be authored in `src/` or `tests/`.

### Rule 2: Domain Concept & Terminology Preservation
- Maintain authentic Indian hospitality & liquor domain terminology across code, database models, and documentation:
  - **KOT**: Kitchen Order Ticket
  - **BOT**: Bar Order Ticket (Bar KOT)
  - **Godown**: Central bulk warehouse
  - **Counter Stock**: Sealed bottle inventory behind the bar
  - **Loose Dispensary**: Open bottle volume dispensed in pegs (30ml, 60ml, 90ml, 180ml / Nip, 375ml / Pint, 750ml / Quart)
  - **FL-III**: Maharashtra State Excise Foreign Liquor License
  - **Permit Holder**: Customer licensed under State Excise rules
  - **Day End**: End-of-day audit where active daily tables move to `*_Dayend` and counters reset

### Rule 3: Database Credentials & Secrets Handling
- **Legacy MDB Access**: Always supply password `<PRODUCTION_MDB_PASSWORD>` for `RSS26/dinurss.mdb`, and `dinu` for backup databases.
- **Modern Code**: Never commit hardcoded passwords or API keys to git. Use environment variables or `appsettings.json` placeholders.

### Rule 4: Multilingual Devanagari Script Support
- Ensure item models, KOT printing components, and UI views preserve UTF-8 Devanagari script strings (`item.Marathi`, e.g., `चिकन टिक्का`).

---

## 3. AI Agent Task Workflows

### 3.1 Workflow 1: Investigating and Implementing a Task
```
  1. Inspect Scope & Requirements
     └── Read relevant sections in README.md, BUSINESS_LOGIC.md, DATABASE_SCHEMA.md.
  2. Trace Existing Implementation
     └── Inspect legacy decompiled code or schema SQL (postgres_schema.sql).
  3. Formulate Implementation Plan
     └── Break work into small, atomic, testable steps.
  4. Implement Modern Code
     └── Build cleanly structured C# / .NET 9 components in src/.
  5. Validate Work
     └── Execute automated tests and build checks.
  6. Update Documentation
     └── Reflect architectural or entity changes in docs/.
```

### 3.2 Workflow 2: Debugging Production Failure Modes
```
  1. Review Error Logs
     └── Inspect log traces in RSS26/Log/ or modern application logs.
  2. Identify Root Cause
     └── E.g., Jet B-Tree index corruption (0x80004005) or SQL string concatenation (0x80040E10).
  3. Develop Fix in Modern Architecture
     └── Ensure modern solution uses ACID storage (PostgreSQL/SQLite WAL) and parameterized queries.
  4. Verify Fix
     └── Write characterization / unit tests replicating the edge case.
```

### 3.3 Workflow 3: Adding a New Feature
```
  1. Domain Model Definition
     └── Define entity classes in Yashdeep.Domain with strongly-typed properties and TenantId.
  2. Application Layer (CQRS)
     └── Define MediatR Commands/Queries and FluentValidation rules in Yashdeep.Application.
  3. Infrastructure Mappings
     └── Add EF Core DbSet and Entity Framework configurations in Yashdeep.Infrastructure.
  4. UI Component & Reporting
     └── Build Blazor Hybrid touch components and QuestPDF document layouts.
  5. Testing & Verification
     └── Add unit tests in tests/ and verify local build.
```

### 3.4 Workflow 4: Database & Migration Changes
```
  1. Schema Analysis
     └── Review schema_extracted/postgres_schema.sql and DATABASE_SCHEMA.md.
  2. EF Core Migration Creation
     └── Generate EF Core migration scripts or update PostgreSQL DDL scripts.
  3. Multi-Tenancy & Index Audit
     └── Verify TenantId filter is applied and indices are created on high-query columns.
  4. Data Pipeline Verification
     └── Verify data ingestion scripts map Access Jet types to PostgreSQL/SQLite types accurately.
```

### 3.5 Workflow 5: API & Integration Changes
```
  1. OpenAPI / DTO Contract Definition
     └── Define request/response DTOs and API endpoints in Yashdeep.Api.
  2. Authentication & Authorization Check
     └── Ensure JWT Bearer auth and role-based policies (RBAC) are applied.
  3. Idempotency & Outbox Alignment
     └── Ensure POST/PUT endpoints support idempotency keys for edge offline sync.
```

### 3.6 Workflow 6: Frontend & UI Changes
```
  1. Component Design
     └── Build responsive Blazor Hybrid / MAUI components.
  2. State Management & Offline Cache
     └── Ensure UI reads from local SQLite cache when offline.
  3. Touch POS Optimization
     └── Ensure UI controls are touch-friendly (large target areas for cashiers).
```

### 3.7 Workflow 7: Deployment & CI/CD Changes
```
  1. Containerization
     └── Inspect Dockerfile / docker-compose.yml configurations.
  2. Environment Parameterization
     └── Ensure database connection strings, JWT secrets, and port bindings use environment variables.
```

---

## 4. Protected Files, Generated Files & Dangerous Commands

### 4.1 Files and Directories That Should Not Be Modified
- `RSS26/`: Protected legacy deployment directory.
- `schema_extracted/`: Source-of-truth extracted metadata from legacy MDB.

### 4.2 Generated Files
- `schema_extracted/postgres_schema.sql`: Generated by extraction tools (`scratch/run_extract_schema.py`). Do not edit manually without updating the generator script.
- `schema_extracted/tables_inventory.csv`: System inventory manifest.

### 4.3 Dangerous Commands & Operations
- `git reset --hard`: May discard local workspace changes.
- `rm -rf RSS26/`: Would destroy original legacy binary and database assets.
- Hardcoded credential commits: Absolutely forbidden.

---

## 5. Required Validation After Making Changes

After modifying code or documentation:
1. **Source Code Verification**: Use `read_file` or `git status` to verify modified files.
2. **Build Verification**: Run `dotnet build` (or relevant compiler commands).
3. **Test Execution**: Run `dotnet test` to ensure zero regressions.
4. **Documentation Audit**: Update links, table references, and workflow guides if functionality changed.
