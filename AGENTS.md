# Yashdeep Hotel MS - AI Agent Guidelines & Context

Welcome, AI Agent! This document is your rapid onboarding guide to the **Yashdeep Hotel Management System (RSS / Real Soft)** codebase and modernization project.

---

## 1. Project Context in 60 Seconds

- **What is this project?**
  A production Hotel, Restaurant & Bar Management System used by **Hotel Yashdeep** (FL-III Liquor License holder in Maharashtra, India).
- **Legacy System Name**: `RSS` (Restaurant Sales System) / `Real Soft` (रिअल सॉफ्ट) / `Vision Soft`.
- **Legacy Stack**: VB.NET (.NET Framework 4.0 WinForms), Microsoft Access Jet 4.0 Database (`.mdb`), Crystal Reports 13, iTextSharp, QRCoder.
- **Repository Current State**: The repository originally contained compiled binaries (`RSS26/RSS.exe`, `RSS_LONGLIFE.exe`, `dinurss.mdb`), 44 monthly error logs (2022–2026), Crystal Report templates, and helper scripts. Full reflection, decompilation metadata, and database schema extraction have been performed and documented.
- **Modernization Mission**: Migrate the legacy standalone desktop app into an **Offline-First, Cloud-Synchronized, Multi-Tenant SaaS Platform** built with **.NET 9, Blazor Hybrid / MAUI, SQLite (local), PostgreSQL (cloud), EF Core, and QuestPDF**.

---

## 2. Critical Credentials & System Constants

| Asset / Parameter | Value | Details |
| :--- | :--- | :--- |
| **Primary Database File** | `RSS26/dinurss.mdb` | 22.5 MB production database (105 user tables) |
| **`dinurss.mdb` Password** | `rss1008` | Recovered from Jet 4.0 header XOR mask |
| **Backup Databases** | `dinurss - Copy.mdb`, `OLD.mdb` | Password: `dinu` |
| **Default App Logins** | `Admin` / `333`<br>`ADMIN` / `admin` | Found in `Login` table |
| **Master Date Lock PW** | `333` / dynamic master | Found in `dateLckMaster` |
| **Hotel Name** | `HOTEL YASHDEEP` | Location: Bhenda / Nanded, Maharashtra |
| **Excise License** | `FL III-2151444022D8ADF7` | Maharashtra State Excise FL-III Hotel/Club License |
| **VAT TIN** | `27900111779v` | State Code 27 (Maharashtra) |
| **Admin Mobile / Contact** | `7741870808` / `Fahadsayyed92@gmail.com` | Configured in `HotelInfo` |

---

## 3. Key Directory Structure

```
YashdeepHotelMS/
├── AGENTS.md                          # Quick agent onboarding (this file)
├── README.md                          # Master project documentation
├── DECOMPILATION_GUIDE.md             # Reverse-engineering manual & tools guide
├── PROJECT_ANALYSIS.md                # Initial static binary analysis
├── extract_schema.ps1                 # PowerShell extraction script
├── extract_schema.sh                  # Linux/WSL mdbtools extraction script
├── .agent/
│   └── rules/
│       └── hotel_ms_rules.md          # Antigravity agent operational rules
├── docs/
│   ├── ARCHITECTURE.md                # Legacy architecture & modernization roadmap
│   ├── DATABASE_SCHEMA.md             # Comprehensive schema reference (105 tables)
│   ├── BUSINESS_LOGIC.md              # Domain workflows: Table, KOT, Bill, Stock, Excise, DayEnd
│   └── AGENTS_AND_RULES.md            # In-depth agent roles, tasks, commands, guidelines
├── schema_extracted/
│   ├── DATABASE_SCHEMA.md             # Extracted tables & columns summary
│   ├── postgres_schema.sql            # Full PostgreSQL DDL for all 105 tables
│   └── tables_inventory.csv           # Table inventory with row & column counts
└── RSS26/                             # Legacy production deployment
    ├── RSS.exe                        # Primary WinForms executable (.NET 4.0)
    ├── RSS_LONGLIFE.exe               # Variant executable for long-life billing
    ├── RSSUTILITYNEW.exe              # Utility tool
    ├── dinurss.mdb                    # Production database (Password: rss1008)
    ├── dinurss - Copy.mdb             # Backup database (Password: dinu)
    ├── OLD.mdb                        # Historical database (Password: dinu)
    ├── nwitem5.rpt / nwitem5.vb       # Item sales Crystal Report & VB.NET wrapper
    ├── Log/                           # 44 monthly error logs from 2022 to 2026
    └── PDF/                           # Sample generated PDF reports
```

---

## 4. Agent Rules & Operational Constraints

1. **Protect Legacy Files**: Never modify or overwrite original legacy binary files (`RSS26/*.exe`, `RSS26/*.mdb`, `RSS26/*.rpt`) unless explicitly instructed.
2. **Preserve Indian Restaurant & Bar Terminology**: Keep domain concepts intact in all models and documentation:
   - **KOT**: Kitchen Order Ticket
   - **BOT**: Bar Order Ticket (DeptKot = Bar)
   - **FL-III**: Maharashtra Foreign Liquor Hotel & Club License
   - **Peg / Unit**: Liquor dispensing units (30ml, 60ml, 90ml, 180ml / Nip, 375ml / Pint, 750ml / Quart)
   - **Godown**: Central bulk warehouse / storage room
   - **Counter**: Bar / Dispensing counter
   - **Day End**: Daily closing audit where daily tables are archived to `*_Dayend` and counters reset
3. **Database Access**:
   - Always use password `rss1008` when accessing `RSS26/dinurss.mdb`.
   - Use `Microsoft.ACE.OLEDB.12.0` on 64-bit Windows PowerShell, or `Microsoft.Jet.OLEDB.4.0` on 32-bit (`SysWOW64`).
4. **Target Modern Stack**:
   - Backend / API: .NET 9 Web API + EF Core + PostgreSQL
   - Local / Offline: SQLite + EF Core + Outbox Pattern Sync Engine
   - Client: Blazor Hybrid (MAUI) for Desktop & Tablets, responsive web for Admin
   - Reporting: QuestPDF (code-first, thermal printer ESC/POS friendly)
5. **Quality Standards**:
   - Ensure all database IDs, UUIDs, currency values, and timestamps are timezone-aware (IST / UTC).
   - Never introduce hardcoded plain-text passwords in new source code; use ASP.NET Core Identity with bcrypt / PBKDF2.

---

## 5. Quick Commands Reference

```powershell
# Test database connection with recovered password
powershell -Command "$c = New-Object System.Data.OleDb.OleDbConnection('Provider=Microsoft.ACE.OLEDB.12.0;Data Source=RSS26\dinurss.mdb;Jet OLEDB:Database Password=rss1008;'); $c.Open(); Write-Host 'Connected!'; $c.Close()"

# Extract tables inventory & schema to postgres_schema.sql
python scratch\run_extract_schema.py

# Inspect specific table data (e.g. HotelInfo, Setup)
python scratch\dump_hotel_configs.py

# Search decompiled queries
# Queries are indexed in: scratch\extracted_sql_queries.txt
# Table lists are in: schema_extracted\tables_inventory.csv
```
