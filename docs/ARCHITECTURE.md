# System Architecture & Technical Specifications

This document outlines the architectural blueprint of the **Yashdeep Hotel Management System**, contrasting the legacy legacy implementation (`RSS`) with the target modern cloud-synchronized SaaS platform.

---

## 1. Legacy Architecture Analysis (`RSS`)

### 1.1 High-Level Component Diagram (Legacy)

```
┌────────────────────────────────────────────────────────────────────────┐
│                   RSS.exe (VB.NET Windows Forms MDI)                  │
├────────────────────────────────────────────────────────────────────────┤
│  UI Forms Layer (191 Forms)                                            │
│  ├── RSS_MDI (Main Parent Window & Top Menus)                          │
│  ├── FRMENTRY (Order Entry, POS Grid, Table Picker, Fast Code Input)   │
│  ├── FRM_CALLTBL, FRMTABLEMERGE, FRMTABLESHIFT                         │
│  ├── frmdeptkot, frmCancleKOT, frmViewKOT                              │
│  ├── FRMCOUNTERSTOCK, FRMOPENIGSTOCK, frmGodownStock                   │
│  ├── FRMBILLREPORT, frmShowBill, FRMCORRECTIONBILL                     │
│  ├── frmExDayEnd, FrmDtpDayend (Day End Settlement)                    │
│  └── frmAccountHead, frmCashBook, FRMVOUCHER, FRMPAYMENT, FRMRECEIPT   │
├────────────────────────────────────────────────────────────────────────┤
│  Shared State & Domain Helpers                                         │
│  ├── RSS.Module1 (Global static state variables: BILLNO, CLIENTID...) │
│  ├── RSS.CConstant (Frequencies, calculation modes)                   │
│  ├── RSS.CFunction1 (Input validators, string formatters, UI splitters)│
│  └── RSS.CMessage (Standardized user dialog prompts)                   │
├────────────────────────────────────────────────────────────────────────┤
│  Data Access Layer                                                     │
│  └── RSS.ClassDB (ADO.NET System.Data.OleDb wrapper)                   │
│      ├── GetDataTable(strQuery)                                        │
│      ├── ExecuteNonQuery(strQuery)                                     │
│      ├── ExecuteScalar(strQuery)                                       │
│      └── SaveData(DataTable, DBOperation, TableName, UniqueCol)        │
├────────────────────────────────────────────────────────────────────────┤
│  External Libraries & Engines                                          │
│  ├── CrystalDecisions.CrystalReports.Engine (34 embedded reports)     │
│  ├── QRCoder.dll & messagingtoolkit.qrcode.dll (UPI Payment QR Codes) │
│  └── itextsharp.dll (PDF export & invoice generation)                  │
└────────────────────────────────────────────────────────────────────────┘
                                    │
                         ADO.NET OleDb Provider
                 (Microsoft.Jet.4.0 / Microsoft.ACE.12.0)
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│             Microsoft Access Jet 4.0 Database (dinurss.mdb)            │
│               Password: "<PRODUCTION_MDB_PASSWORD>" | 105 User Tables                    │
│   Shared over Windows LAN File Sharing (SMB) to multiple POS terminals │
└────────────────────────────────────────────────────────────────────────┘
```

### 1.2 Legacy Implementation Details

1. **Monolithic Windows Forms Architecture**:
   - Built on **.NET Framework 4.0 Client Profile** (Runtime `v4.0.30319`).
   - MDI Parent (`RSS.RSS_MDI`) manages child windows for 100+ functions.
   - Heavy procedural logic inside code-behind event handlers (`Button_Click`, `KeyDown`, `FormLoad`).
2. **Global Shared State (`RSS.Module1`)**:
   - Variables like `BILLNUMBER`, `CLIENTID`, `OPERATORNAME`, `dtpdayend`, `btndateclick`, and `AdminMobile` are static global fields accessed across forms.
   - Concurrent actions between windows can lead to race conditions in the UI thread.
3. **Data Access Layer (`RSS.ClassDB`)**:
   - Uses `System.Data.OleDb.OleDbConnection` with connection string:
     `Provider=Microsoft.ACE.OLEDB.12.0;Data Source=|DataDirectory|\dinurss.mdb;Jet OLEDB:Database Password=<PRODUCTION_MDB_PASSWORD>;`
   - Direct inline SQL concatenation (e.g. `INSERT INTO BILLFINAL SELECT * FROM [BILLFINAL_Dayend] where FORMAT(DATE,'yyyy-MM-dd')='...`).
4. **Day-End Archive Pattern**:
   - Rather than using temporal indexing, active transaction tables (`BILLFINAL`, `finalbill`, `KOTFINAL`, `KOTDETAIL`, `GrandBillDetails`) are kept minimal during business hours.
   - At "Day End" closing (`FrmDtpDayend`), rows are bulk-copied into corresponding `*_Dayend` tables and active tables are truncated/reset.
5. **Hardware Integration**:
   - Thermal receipt printers (ESC/POS) via Windows print spooler and raw socket/LPT printing.
   - Dual Crystal Reports and GDI+ direct print page drawing (`PrintBILL`, `PrintKOT`, `PrintKotDept`).
   - Dynamic UPI payment QR codes generated on the fly via `QRCoder.dll` and embedded onto receipts.

### 1.3 Root Cause Analysis of Legacy Failure Modes (From 44 Error Logs)

| Error Code / Signature | Root Cause | Architectural Flaw |
| :--- | :--- | :--- |
| `0x80004005: Jet database engine stopped process (concurrency lock)` | Multiple POS terminals accessing `dinurss.mdb` simultaneously over SMB network share. | Access Jet does not support client-server transaction isolation; file-level and page-level locks collide. |
| `0x80004005: The search key was not found in any record` | Corrupted B-Tree indices in Access `.mdb` caused by sudden power cuts, network drops, or write collisions. | Lack of write-ahead logging (WAL) and lack of ACID crash-recovery in Jet 4.0. |
| `0x80040E10: No value given for one or more required parameters` | Empty UI text fields concatenated into dynamic SQL queries without parameterized validation (e.g. `TABLE_NO=''`). | Lack of input sanitization and parameterized queries. |
| `0x80040E37: Could not find output table 'KOTDETAIL_TEMP'` | Dynamic temporary table creation collision when multiple terminals trigger KOT printing simultaneously. | Schema modification at runtime inside operational transaction paths. |
| `0x80040E4D: Not a valid password` | Hardcoded password mismatch (`dinu` in older binaries vs `<PRODUCTION_MDB_PASSWORD>` in production `dinurss.mdb`). | Hardcoded credentials without configuration flexibility. |

---

## 2. Target Modern Architecture (Cloud-Synchronized SaaS)

### 2.1 Target Solution Topology

```
┌────────────────────────────────────────────────────────────────────────┐
│                        POS Terminals / Tablets                         │
│             (Windows PC / Android Tablet / Touchscreen POS)            │
├────────────────────────────────────────────────────────────────────────┤
│  Client App: .NET 9 Blazor Hybrid / MAUI Desktop & Mobile              │
│  ├── Touch POS UI (Table Grid, KOT Sender, Fast Menu Search)           │
│  ├── Local Hardware Controller: ESC/POS Thermal Printing & Barcode     │
│  ├── Local Database: SQLite (Encrypted via SQLCipher)                  │
│  ├── Local EF Core Context: Full offline read/write capability          │
│  └── Outbox Sync Engine (Reliable background HTTP sync client)         │
└────────────────────────────────────────────────────────────────────────┘
                                    │
                             HTTPS / WebSockets
                      (JWT Bearer Auth + SignalR Realtime)
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                      Cloud / Local Gateway API                         │
│                    ASP.NET Core 9 Web API Service                      │
├────────────────────────────────────────────────────────────────────────┤
│  Application Layer (Clean Architecture / CQRS via MediatR)             │
│  ├── BillingService (Taxes, Discounts, Split Bills, Payments)          │
│  ├── KotService (Kitchen Routing, Marathi Slip Translation)            │
│  ├── InventoryService (Godown → Counter → Bottle → Peg tracking)       │
│  ├── ExciseComplianceService (Maharashtra FL-III Registers & Returns)  │
│  ├── ReportingService (QuestPDF Document Generation)                   │
│  └── SyncCoordinator (Outbox processing, Conflict resolution)          │
├────────────────────────────────────────────────────────────────────────┤
│  Data Layer & Tenancy                                                  │
│  ├── Multi-tenant EF Core (TenantId column filter on all tables)       │
│  └── Primary Database: PostgreSQL 16+ (ACID, JSONB, Row-Level Security)│
└────────────────────────────────────────────────────────────────────────┘
```

### 2.2 Offline-First Outbox Synchronization Flow

```
User Action (e.g., Create KOT or Bill)
        │
        ▼
[ Local Transaction in SQLite ]
   ├── Insert TableOrder / Bill
   ├── Decrement Local Stock
   └── Insert Record into OutboxQueue (Operation, PayloadJson, CreatedAt)
        │
        ├──► Print Receipt Immediately (Local Thermal ESC/POS)
        │
        ▼
[ Background Sync Worker (Every 15-30s or WebSocket Trigger) ]
   ├── Read Pending Outbox Messages
   ├── POST /api/v1/sync/batch (Batch of mutations)
   │     │
   │     ▼
   │  [ Cloud API Server ]
   │     ├── Validate Tenant & Idempotency Key
   │     ├── Apply Changes to PostgreSQL Database
   │     ├── Resolve Conflicts (Server-authoritative timestamps)
   │     └── Return Sync Acknowledgement & Inbound Delta Updates
   │
   └── Mark Outbox Messages as Sent / Apply Inbound Updates to Local SQLite
```

### 2.3 Technology Stack Comparison

| Dimension | Legacy Stack (`RSS26`) | Target Modern Stack |
| :--- | :--- | :--- |
| **Framework** | .NET Framework 4.0 | .NET 9 LTS |
| **Language** | VB.NET (Option Strict Off) | C# 13 (Nullable Enabled, Clean Architecture) |
| **UI Framework** | Windows Forms (MDI) | Blazor Hybrid (MAUI) + Tailwind CSS |
| **Local POS DB** | Access Jet 4.0 (`.mdb`) | SQLite + EF Core (Embedded, zero setup) |
| **Cloud Server DB** | None (Single machine LAN) | PostgreSQL 16 (Multi-tenant SaaS) |
| **Reporting** | Crystal Reports 13 | QuestPDF (Code-first, pixel-perfect, thermal 80mm/58mm/A4) |
| **Printing** | GDI+ Windows Spooler / LPT | Direct ESC/POS USB, Network/LAN, Bluetooth & Spooler |
| **Authentication** | Plain text password in MDB | ASP.NET Core Identity + JWT + RBAC |
| **Multi-tenancy** | Hardcoded single hotel | Shared schema with Row-Level Security (`TenantId`) |
| **Bilingual** | Basic font hacks for Devanagari | Native UTF-8 Unicode across entire stack |
