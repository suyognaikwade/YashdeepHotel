# Yashdeep Hotel Management System (RSS / Real Soft)

[![Platform](https://img.shields.io/badge/Legacy_Platform-VB.NET_4.0_WinForms-blue.svg)](#)
[![Database](https://img.shields.io/badge/Legacy_Database-Access_Jet_4.0_(105_Tables)-orange.svg)](#)
[![Target](https://img.shields.io/badge/Target_Platform-.NET_9_Blazor_Hybrid-purple.svg)](#)
[![Cloud DB](https://img.shields.io/badge/Target_Cloud-PostgreSQL_16-blue.svg)](#)
[![Offline](https://img.shields.io/badge/Offline_Sync-SQLite_+_Outbox_Pattern-green.svg)](#)

> **Enterprise Hotel, Multi-Section Restaurant & Maharashtra State Excise FL-III Bar Management System.**  
> Originally developed as **RSS (Restaurant Sales System / Real Soft)** in VB.NET and Microsoft Access; now fully reverse-engineered, documented, and architected for migration to an **Offline-First, Cloud-Synchronized, Multi-Tenant SaaS Platform**.

---

## 🤖 AI Agent Quick Start

If you are an AI coding agent assigned to this repository, read and follow these essential documents before making changes:

1. [**`AGENTS.md`**](AGENTS.md): Core agent rules, system constants, protected directory map, and task workflows.
2. [**`docs/AGENTS_AND_RULES.md`**](docs/AGENTS_AND_RULES.md): Detailed AI agent operational specifications, safety rules, dangerous operations, and post-change validation.
3. [**`docs/ARCHITECTURE.md`**](docs/ARCHITECTURE.md): System architecture (Legacy VB.NET/Access Jet 4.0 vs. Target .NET 9 Blazor Hybrid + PostgreSQL/SQLite SaaS).
4. [**`docs/SAAS_ARCHITECTURE.md`**](docs/SAAS_ARCHITECTURE.md): Multi-Tenant SaaS Architecture (Tenants, lifecycle, isolation, PostgreSQL RLS, subscriptions & multi-location behavior).
5. [**`docs/BUSINESS_LOGIC.md`**](docs/BUSINESS_LOGIC.md): Domain workflows (Dining sections, KOT routing, split taxes, dynamic UPI, multi-tier stock, FL-III Excise compliance, Day End).
5. [**`docs/DATABASE_SCHEMA.md`**](docs/DATABASE_SCHEMA.md): Complete schema reference for all 105 user tables.
6. [**`docs/DEVELOPMENT_AND_WORKFLOWS.md`**](docs/DEVELOPMENT_AND_WORKFLOWS.md): Setup, actual commands, testing expectations, C# conventions, and troubleshooting.
7. [**`docs/CONFIGURATION_AND_ENV.md`**](docs/CONFIGURATION_AND_ENV.md): Environment variables, configuration, hardware thermal printing, and security policies.

---

## 📖 Table of Contents

1. [Executive Summary & Project Overview](#1-executive-summary--project-overview)
2. [Major Reverse Engineering Findings](#2-major-reverse-engineering-findings)
3. [Business Domain & Operating Profile](#3-business-domain--operating-profile)
4. [System Architecture: Legacy vs. Target Modernization](#4-system-architecture-legacy-vs-target-modernization)
5. [Database Schema & Inventory (105 Tables)](#5-database-schema--inventory-105-tables)
6. [Core Operational Workflows](#6-core-operational-workflows)
   - [6.1 Section Seating & Differential Pricing](#61-section-seating--differential-pricing)
   - [6.2 Order Taking & Bilingual KOT Workflow](#62-order-taking--bilingual-kot-workflow)
   - [6.3 Billing, Split Taxes & Dynamic UPI QR Codes](#63-billing-split-taxes--dynamic-upi-qr-codes)
   - [6.4 Multi-Tier Liquor & Stock Management](#64-multi-tier-liquor--stock-management)
   - [6.5 Maharashtra State Excise (FL-III) Compliance](#65-maharashtra-state-excise-fl-iii-compliance)
   - [6.6 The "Day End" Settlement & Rollover Procedure](#66-the-day-end-settlement--rollover-procedure)
7. [System Credentials & Configurations](#7-system-credentials--configurations)
8. [Root Cause Analysis of Production Failure Modes](#8-root-cause-analysis-of-production-failure-modes)
9. [Modernization Roadmap (.NET 9 + Blazor + SQLite + PostgreSQL)](#9-modernization-roadmap-net-9--blazor--sqlite--postgresql)
10. [Documentation Index](#10-documentation-index)

---

## 1. Executive Summary & Project Overview

The **Yashdeep Hotel Management System** is an end-to-end mission-critical Point-of-Sale (POS), Kitchen Order Routing, Multi-Tier Liquor Inventory, Accounting, and State Excise Compliance system operating at **Hotel Yashdeep** (Bhenda, Maharashtra, India).

The business operates a comprehensive hospitality complex comprising:
- Multi-section dine-in restaurant (**Family Room, AC Hall, Main Hall, Casual Dining, Garden**)
- Takeaway / Delivery packaging counter (**Parcel**)
- Maharashtra State Excise **FL-III Hotel & Club Licensed Bar** dispensing Indian Made Foreign Liquor (IMFL), Country Liquor, Beer, and Wine.

The repository originally contained only legacy compiled binaries (`RSS26/RSS.exe`, `RSS_LONGLIFE.exe`, `dinurss.mdb`), 44 historical monthly error logs (2022–2026), and report templates. Through deep binary reflection, cryptographic password extraction, and metadata mining, **100% of the system architecture, schemas, queries, and business rules have been recovered and documented**.

---

## 2. Major Reverse Engineering Findings

| Breakthrough | Prior State | Discovered / Resolved State | Impact |
| :--- | :--- | :--- | :--- |
| **Database Passwords** | Unknown ("Not a valid password") | **`dinurss.mdb`**: `rss1008`<br>**`dinurss - Copy.mdb` / `OLD.mdb`**: `dinu` | Full access to 22.5 MB production database with 105 active tables. |
| **Database Schema** | Locked inside Access MDB | 105 User Tables, 1,000+ Columns fully extracted into PostgreSQL DDL | Complete data migration path unblocked. |
| **Source Logic Recovery** | Zero source code files | 191 Windows Forms, 41 Classes, 863 inline SQL queries extracted | Full operational logic and UI handlers mapped. |
| **Reports Recovery** | 34 Crystal Reports locked | 34 Report manifests identified & schema bound | Clear path to migrate to QuestPDF code-first templates. |
| **Bug History Analysis** | 44 Unparsed Error Logs | 11,000+ Error traces categorized (Jet lock `0x80004005`, B-Tree corruptions) | Pinpointed exact concurrency flaws for modern architecture. |

---

## 3. Business Domain & Operating Profile

### 3.1 Entity Identity
- **Trade Name**: HOTEL YASHDEEP
- **Registered Address**: Bhenda / Nanded, Maharashtra, India
- **Excise License**: `FL III-2151444022D8ADF7` (Maharashtra State Excise Hotel/Club License)
- **State VAT / TIN**: `27900111779v` (State Code 27: Maharashtra)
- **Software Identity**: `REAL SOFT` / `VISION SOFT` / `रिअल सॉफ्ट` (RSS)
- **Management Email**: `Fahadsayyed92@gmail.com`
- **Management Mobile**: `7741870808`

---

## 4. System Architecture: Legacy vs. Target Modernization

```
Legacy Architecture (RSS26)                 Modern Target Architecture (SaaS)
┌──────────────────────────────────────┐     ┌──────────────────────────────────────┐
│ .NET 4.0 Windows Forms Monolith     │     │ .NET 9 Blazor Hybrid (MAUI / Web)    │
│ (RSS.exe - 191 Forms, 34 Reports)    │     │ Cross-platform (Windows, Android POS)│
├──────────────────────────────────────┤     ├──────────────────────────────────────┤
│ Procedural Inline SQL + OleDb        │     │ Clean Architecture + CQRS + EF Core  │
│ (ClassDB + Module1 Global State)     │     │ MediatR, FluentValidation, DTOs      │
├──────────────────────────────────────┤     ├──────────────────────────────────────┤
│ Microsoft Access Jet 4.0 (dinurss.mdb│     │ Offline-First Edge: SQLite + Outbox  │
│ Shared over Windows SMB LAN FileShare│     │ Cloud Central: PostgreSQL 16+ MultiT │
├──────────────────────────────────────┤     ├──────────────────────────────────────┤
│ Crystal Reports 13 + GDI+ Spooler    │     │ QuestPDF Code-First (80mm/58mm/A4)   │
│ Dynamic QRCoder.dll UPI generation   │     │ Direct ESC/POS USB, LAN & Bluetooth  │
└──────────────────────────────────────┘     └──────────────────────────────────────┘
```

For complete technical specifications, see [**`docs/ARCHITECTURE.md`**](docs/ARCHITECTURE.md).

---

## 5. Database Schema & Inventory (105 Tables)

The production database `dinurss.mdb` contains **105 user tables** categorized into 9 domains:

```
├── 1. Sales & Billing (16 tables): BILLFINAL (3,693), finalbill (13,400), GrandBill, BILLFINAL_Dayend...
├── 2. Kitchen Orders / KOT (10 tables): KOTDETAIL (19,382), KOTFINAL (10,625), CancelKot (1,389)...
├── 3. Menu & Pricing (6 tables): item (1,607), ItemDept (39), BRANDML (741), TABLE_NO_GROP (6)...
├── 4. Inventory & Stock (17 tables): GodownStock (1,926), CNTPACK_LIVE (1,934), CNTLOOSE_LIVE (757)...
├── 5. Purchases (6 tables): LiqPurchase, LiqPurchaseFinal, FoodPurchase, OtherPurchase...
├── 6. State Excise Compliance (15 tables): ExPremiteHolder (524), ExciseMonthlyStat (1,873), ExStoreInfo...
├── 7. Accounting & Ledgers (11 tables): AccountHead (256), Voucher (6,770), Receipt (95), CashTransfer...
├── 8. Staff & Customers (4 tables): Waiter (4), NewCustomer (31), ContactList...
└── 9. Configuration & Security (20 tables): HotelInfo, Setup, Login, dateLckMaster, SoftwareName...
```

- **Full Schema Documentation**: [**`docs/DATABASE_SCHEMA.md`**](docs/DATABASE_SCHEMA.md)
- **Ready-to-run PostgreSQL DDL**: [`schema_extracted/postgres_schema.sql`](schema_extracted/postgres_schema.sql)
- **Row & Column Inventory CSV**: [`schema_extracted/tables_inventory.csv`](schema_extracted/tables_inventory.csv)

---

## 6. Core Operational Workflows

Detailed workflow descriptions are provided in [**`docs/BUSINESS_LOGIC.md`**](docs/BUSINESS_LOGIC.md). Key highlights include:

### 6.1 Section Seating & Differential Pricing
Dining tables are grouped into 6 sections (`Family`, `Ac`, `Hall`, `Restaurant`, `Garden`, `Parcel`). The menu catalog (`item`) automatically applies section-specific rate columns:
`salerate` (standard) | `FAMILYRATE` | `ACRATE` | `VIPRATE` | `WHOLESALE`.

### 6.2 Order Taking & Bilingual KOT Workflow
1. Waiter keys in fast item codes or searches by name in `FRMENTRY`.
2. Live counter stock is verified (`LiveSTK()`).
3. Pressing "Save KOT" writes to `KOTDETAIL` and `KOTFINAL`.
4. Slip routes to target printer:
   - **Kitchen Printer**: Uses `item.Marathi` to print bilingual English/Devanagari tickets (e.g., `चिकन टिक्का`).
   - **Bar Printer (BOT)**: Automatically decrements open bottle volume.

### 6.3 Billing, Split Taxes & Dynamic UPI QR Codes
- Computes Subtotal, Discounts, Service Charges, and split taxes (CGST 2.5% + SGST 2.5% on Food; State Excise VAT on Liquor).
- Embeds a dynamic UPI payment QR code (`upi://pay?pa=dinu...&am=TOTAL`) onto the thermal bill receipt for instant contact-free guest payment.

### 6.4 Multi-Tier Liquor & Stock Management
Liquor transitions across three tiers:
1. **Godown (`GodownStock`)**: Bulk storage received via Transport Permits (TP).
2. **Counter (`CNTPACK_LIVE`)**: Sealed bottles stored behind the bar.
3. **Loose Dispensary (`CNTLOOSE_LIVE`)**: Measured in Millilitres (ML) for peg sales (30ml, 60ml, 90ml, 180ml). When an open bottle depletes, the system automatically opens the next sealed bottle.

### 6.5 Maharashtra State Excise (FL-III) Compliance
Maintains legal compliance with the Maharashtra Prohibition Act:
- **Permit Holder Register (`ExPremiteHolder`)**: Tracks individual customer liquor permits.
- **Daily Bulk Litre Statement (`FrmDailyBulkLitre`)**: Summarizes daily spirit, wine, and beer volume.
- **Monthly Register 1 (`ExciseMonthlyStat`)**: Monthly inspection return for State Excise Officers.

### 6.6 The "Day End" Settlement & Rollover Procedure
At night closing (`FrmDtpDayend`), the system:
1. Validates all tables are settled and paid.
2. Bulk copies active daily records into `*_Dayend` tables (`BILLFINAL_Dayend`, `finalbill_Dayend`, `KOTDETAIL_Dayend`).
3. Sets today's Closing stock as tomorrow's Opening stock.
4. Resets daily counters (`BILLNUMBERDAY = 1`, `KOTNO = 1`).
5. Dispatches an automated sales summary email to the management address.

---

## 7. System Credentials & Configurations

| Service / Component | Credential / Configuration | Source / Location |
| :--- | :--- | :--- |
| **Primary Production MDB** | Password: **`rss1008`** | `RSS26/dinurss.mdb` (Jet 4.0 header XOR mask) |
| **Historical & Backup MDBs** | Password: **`dinu`** | `RSS26/dinurss - Copy.mdb`, `RSS26/OLD.mdb` |
| **Default Operator Login 1** | User: **`Admin`** / Password: **`333`** | Table `Login` (clientid: 1) |
| **Default Operator Login 2** | User: **`ADMIN`** / Password: **`admin`** | Table `Login` (clientid: 1) |
| **Date Lock Master Password**| **`333`** (or dynamic master password) | Table `dateLckMaster` |
| **UPI Payment Handle** | VPA: **`dinu`** | Table `HotelInfo` |
| **Management Alert Mobile** | Phone: **`7741870808`** | Table `HotelInfo` |
| **Management Alert Email** | Email: **`Fahadsayyed92@gmail.com`** | Table `HotelInfo` |

---

## 8. Root Cause Analysis of Production Failure Modes

From the 44 historical error log files (2022–2026), the top production failures have been diagnosed:

```
[5,269 Occurrences] OleDbException: The Microsoft Jet database engine stopped the process 
                    because you and another user are attempting to change the same data.
                    --> CAUSE: Multi-client concurrency collisions in single Access MDB file over LAN.
                    --> FIX: Transition to client-server PostgreSQL in cloud & local SQLite per POS terminal.

[  549 Occurrences] OleDbException: The search key was not found in any record.
                    --> CAUSE: Jet B-Tree index corruption after power loss or write collisions.
                    --> FIX: Modern ACID engine with Write-Ahead Logging (WAL).

[   85 Occurrences] OleDbException: No value given for one or more required parameters.
                    --> CAUSE: Dynamic SQL query string concatenation with blank UI inputs (e.g. TABLE_NO='').
                    --> FIX: Strongly-typed EF Core parameterized queries and FluentValidation.
```

---

## 9. Modernization Roadmap (.NET 9 + Blazor + SQLite + PostgreSQL)

```
Phase 1: Database Ingestion & Domain Modeling
├── Set up PostgreSQL 16 schema using schema_extracted/postgres_schema.sql
├── Implement Yashdeep.Domain entities (Table, KOT, Bill, StockItem, ExciseRegister)
└── Build Access-to-PostgreSQL data migration pipeline

Phase 2: Offline-First POS Engine
├── Local SQLite DbContext + EF Core
├── Outbox sync queue & background sync worker
└── ESC/POS thermal printer driver (USB / LAN / Bluetooth)

Phase 3: Touch POS & KOT Interface
├── Blazor Hybrid (MAUI) touch-screen user interface
├── Table layout visualizer with color-coded states (Vacant, KOT Active, Billed)
└── Marathi bilingual KOT slip generation via QuestPDF

Phase 4: Multi-Tier Stock & State Excise Module
├── Godown → Counter → Bottle Open → Peg dispensing calculation engine
└── Maharashtra FL-III daily bulk litre & monthly statement reports

Phase 5: Cloud SaaS Hardening & Multi-Tenancy
├── Multi-tenant tenant isolation (`TenantId` row-level filter)
├── ASP.NET Core Identity with JWT & RBAC
└── Centralized analytics dashboard for multi-branch hotel owners
```

---

## 10. Documentation Index

The repository contains a dedicated documentation layer under [`docs/`](docs/):

| Document | Purpose |
| :--- | :--- |
| [**`AGENTS.md`**](AGENTS.md) | AI Agent onboarding guide, rules, constants, directory map, and task workflows. |
| [**`docs/AGENTS_AND_RULES.md`**](docs/AGENTS_AND_RULES.md) | Comprehensive AI agent operational specifications, safety rules, dangerous commands, and post-change validation. |
| [**`docs/ARCHITECTURE.md`**](docs/ARCHITECTURE.md) | Deep technical breakdown of legacy WinForms vs target .NET 9 Blazor Hybrid SaaS architecture. |
| [**`docs/SAAS_ARCHITECTURE.md`**](docs/SAAS_ARCHITECTURE.md) | Multi-tenant SaaS architecture, tenant lifecycle, PostgreSQL RLS, entitlements, and security boundary specs. |
| [**`docs/BUSINESS_LOGIC.md`**](docs/BUSINESS_LOGIC.md) | Exhaustive domain workflows (Seating sections, KOT, split taxes, UPI, liquor tiers, Excise compliance, Day End). |
| [**`docs/DATABASE_SCHEMA.md`**](docs/DATABASE_SCHEMA.md) | Schema specification for all 105 user tables. |
| [**`docs/DEVELOPMENT_AND_WORKFLOWS.md`**](docs/DEVELOPMENT_AND_WORKFLOWS.md) | Environment setup, CLI/PowerShell commands matrix, C# conventions, testing, DoD, and troubleshooting. |
| [**`docs/CONFIGURATION_AND_ENV.md`**](docs/CONFIGURATION_AND_ENV.md) | Environment variables matrix, configuration management, ESC/POS hardware thermal printing, and security policies. |
| [**`DECOMPILATION_GUIDE.md`**](DECOMPILATION_GUIDE.md) | Reverse-engineering manual & decompilation tool guide. |
| [**`PROJECT_ANALYSIS.md`**](PROJECT_ANALYSIS.md) | Initial binary static analysis findings. |

---

*Documentation compiled and verified for Hotel Yashdeep and Modernization Engineering Teams.*
