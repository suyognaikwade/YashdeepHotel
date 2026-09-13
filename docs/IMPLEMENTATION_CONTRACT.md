# Authoritative Modern SaaS Implementation Contract

**Document Status:** Freeze / Authoritative Master Implementation Contract
**Repository:** `suyognaikwade/YashdeepHotel`
**Target Branch:** `botify`
**Effective Date:** September 2026 (Modernization Phase 1 Baseline)
**Precedence:** Primary source of truth for all subsequent engineering tasks. Supersedes all prior contradictory architecture drafts unless explicitly amended by an approved Architecture Review Board (ARB) decision.

---

## 1. Implementation Baseline & Non-Negotiable Directives

### 1.1 Verified Repository Baseline
As verified in `docs/verification/instance-02-modern-application-verification.md` and `docs/INDEPENDENT_VERIFICATION_REPORT.md`:
* **Modern Source Code Status:** **0% Implemented**. Zero modern C# source files (`.cs`), Blazor components (`.razor`), MAUI XAML files (`.xaml`), C# project files (`.csproj`), or solution files (`.sln`) exist in the repository.
* **Legacy Assets Baseline:** The repository contains legacy binaries under `RSS26/` (VB.NET .NET Framework 4.0 WinForms executable `RSS.exe`, Jet 4.0 database `dinurss.mdb`, Crystal Reports `.rpt`), schema extraction tooling (`extract_schema.sh`, `extract_schema.ps1`), extracted DDL (`schema_extracted/postgres_schema.sql`), and architectural specifications.
* **Operational Scope:** This document freezes the contract for transition from architecture phase to initial vertical slice implementation.

### 1.2 Non-Negotiable Technical Directives
1. **Migration-Only Legacy Legacy Boundary:** Microsoft Access, `.mdb` files, OLEDB/Jet 4.0 drivers, and the legacy VB.NET `RSS.exe` WinForms application are strictly migration and reference sources. **They can NEVER become production runtime dependencies.**
2. **Strict Cloud API Mediation:** Production clients (Blazor Hybrid desktop/mobile terminals, web clients) **NEVER connect directly to PostgreSQL or any cloud database**. All cloud data access and mutations MUST pass through HTTPS REST/gRPC API endpoints mediated by strict authentication and tenant context filters.
3. **Single Codebase Architecture:** Multi-edition product variants (Bar & Restaurant, Hotel, Hybrid) MUST be delivered from a **single repository and single unified codebase**. Creating separate codebases, repositories, or compiled branches for different product editions is strictly prohibited.
4. **Prohibition of Obsolete Runtimes & Peripherals:** Direct production use of Microsoft Access, Crystal Reports, legacy VB.NET executables, and raw direct-to-cloud SQL client connections is **strictly prohibited**.
5. **Production Secrets Integrity:** Plain-text passwords, private keys, database credentials, or signing keys must never be committed to repository documentation or code. Production configurations must utilize environment variables or secure key vaults.

---

## 2. Target Technology Stack & Repository Structure

### 2.1 Approved Target Technology Stack
* **Cloud API & Backend:** .NET 9 Web API, C# 13, EF Core 9.
* **Cloud Database:** PostgreSQL 16 with Row-Level Security (RLS), JSONB audit trails, and transactional Outbox/Inbox tables.
* **Edge / Local POS Storage:** SQLite with SQLCipher (256-bit AES encryption), EF Core 9 SQLite provider, Write-Ahead Logging (WAL) mode.
* **Client Frontend:** Blazor Hybrid (.NET MAUI cross-platform host for Windows & Android terminals; responsive WebAssembly/Server for Cloud Management Portal).
* **Document & Thermal Printing:** QuestPDF (C# code-first layout engine for A4/A5 compliance reports & invoices) + ESC/POS binary driver engine for 58mm/80mm thermal receipt printers with Marathi/Devanagari rasterization support.
* **Security & Entitlements:** Ed25519 asymmetric cryptographic token validation, ASP.NET Core Identity with bcrypt/PBKDF2 password hashing, JWT bearer authentication with tenant claims.

### 2.2 Standardized Repository Structure
Future implementation PRs must establish and adhere to the following Clean Architecture directory structure:

```
YashdeepHotelMS/
├── docs/                                  # Master specifications & contracts
│   ├── IMPLEMENTATION_CONTRACT.md         # THIS AUTHORITATIVE CONTRACT
│   └── verification/                      # Independent audit reports
├── src/
│   ├── Client/                            # Blazor Hybrid & MAUI Edge UI
│   │   ├── Yashdeep.Client.Blazor/        # Shared Razor components & UI logic
│   │   └── Yashdeep.Client.Maui/          # WinUI3 & Android shell host
│   ├── Shared/                            # Dynamic entitlement & DTO contracts
│   │   └── Yashdeep.Shared/               # Capability models, DTOs, Sync payloads
│   ├── Domain/                            # Pure domain aggregates, entities & rules
│   │   └── Yashdeep.Domain/               # Bounded contexts, value objects, domain events
│   ├── Application/                       # CQRS handlers, validation & workflows
│   │   └── Yashdeep.Application/          # MediatR handlers, FluentValidation, interfaces
│   ├── Infrastructure/                    # External services & hardware integration
│   │   └── Yashdeep.Infrastructure/       # ESC/POS drivers, QuestPDF builders, Security
│   ├── Persistence/                       # EF Core DbContexts & Repositories
│   │   ├── Yashdeep.Persistence.Cloud/    # PostgreSQL EF Core DbContext & RLS
│   │   └── Yashdeep.Persistence.Local/    # SQLite SQLCipher EF Core DbContext & WAL
│   ├── SyncEngine/                        # Outbox/Inbox offline synchronization
│   │   └── Yashdeep.SyncEngine/           # Edge Outbox processor & Cloud Inbox processor
│   └── Server/                            # Cloud Web API Host
│       └── Yashdeep.Server.Api/           # ASP.NET Core Web API controllers & middleware
└── tests/
    ├── Yashdeep.Domain.Tests/             # Unit tests for domain aggregates & excise rules
    ├── Yashdeep.Application.Tests/        # CQRS handler & entitlement unit tests
    ├── Yashdeep.Persistence.Tests/        # Integration tests for SQLite & EF Core RLS
    ├── Yashdeep.SyncEngine.Tests/         # Outbox idempotency & conflict resolution tests
    └── Yashdeep.Security.Tests/           # Tenant isolation & token verification tests
```

---

## 3. Architectural & Component Boundaries

### 3.1 Layered Dependency Direction
Strict Clean Architecture dependency flow must be enforced at compile-time:
* **Domain Layer (`Yashdeep.Domain`):** Zero external dependencies. Defines pure domain entities, value objects, aggregates, domain events, and repository interfaces.
* **Application Layer (`Yashdeep.Application`):** Depends strictly on `Domain`. Contains application use cases, CQRS commands/queries (MediatR), FluentValidation validators, DTOs, and interface definitions.
* **Infrastructure / Persistence Layers (`Infrastructure`, `Persistence.*`, `SyncEngine`):** Depend on `Application` and `Domain`. Implement database contexts, external network services, file storage, hardware printers, and cryptography.
* **Presentation / Host Layers (`Server.Api`, `Client.*`):** Depend on `Application` and `Infrastructure`. Provide API endpoints, Blazor UI views, and native MAUI bindings. Dependency inversion guarantees infrastructure details never leak into core domain business rules.

### 3.2 Subsystem Communication Boundaries
```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                    EDGE LOCAL BOUNDARY                                 │
│  ┌──────────────────────┐      ┌─────────────────────────┐      ┌───────────────────┐  │
│  │  Blazor Hybrid UI    │ ───► │ Application CQRS Engine │ ───► │ SQLite SQLCipher  │  │
│  └──────────────────────┘      └─────────────────────────┘      └───────────────────┘  │
│                                             │                             │            │
│                                             ▼                             ▼            │
│                                   ┌───────────────────┐        ┌────────────────────┐  │
│                                   │ Hardware Drivers  │        │ Edge Outbox Engine │  │
│                                   │ (ESC/POS Printer) │        └────────────────────┘  │
│                                   └───────────────────┘                   │            │
└──────────────────────────────────────────────│────────────────────────────│────────────┘
                                               │                            │
                                         Physical USB/LAN            HTTPS API Outbox Sync
                                               │                            │
┌──────────────────────────────────────────────│────────────────────────────│────────────┘
│                                              ▼                            ▼            │
│  ┌──────────────────────┐      ┌─────────────────────────┐      ┌───────────────────┐  │
│  │ QuestPDF Reporting   │ ◄─── │ ASP.NET Core Web API    │ ───► │ Cloud Inbox Engine│  │
│  └──────────────────────┘      └─────────────────────────┘      └───────────────────┘  │
│                                             │                             │            │
│                                             ▼                             ▼            │
│                                ┌──────────────────────────┐     ┌───────────────────┐  │
│                                │ Identity & Entitlements  │     │ PostgreSQL Cloud  │  │
│                                └──────────────────────────┘     └───────────────────┘  │
│                                    CLOUD SERVER BOUNDARY                               │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Tenant, Organization & Security Hierarchy

### 4.1 Enterprise Multi-Tenant Hierarchy
The platform structures multi-tenancy into an 8-level explicit structural hierarchy:

```
1. Tenant (Subscription / Business Entity, e.g., Yashdeep Hospitality Group)
   └── 2. Organization (Legal Business Entity / Excise Licensee)
        └── 3. Branch (Physical Property / Location, e.g., Bhenda Branch)
             └── 4. Outlet (Operational Cost Center, e.g., Main Dining, AC Bar, Permit Room, Front Desk)
                  └── 5. Terminal / Workstation (Physical Computer, Tablet, POS Counter)
                       └── 6. Device (Registered Hardware Instance with Ed25519 Device ID)
                            └── 7. User (Staff Member, Manager, Cashier, Steward)
                                 └── 8. Role & Permissions (RBAC + Dynamic Capability Entitlements)
```

### 4.2 Mandatory Identifiers on Transactional Entities
Every transactional entity (e.g., `Order`, `KOT`, `Bill`, `Payment`, `StockMovement`, `ExciseRegisterEntry`, `AuditLog`) **MUST** contain the following immutable contextual identifiers:
* `Guid TenantId` — Foreign key & query filter boundary.
* `Guid OrganizationId` — Legal/Excise entity boundary.
* `Guid BranchId` — Physical store location boundary.
* `Guid OutletId` — Operational section/cost center boundary.
* `Guid TerminalId` — Logical POS workstation reference.
* `Guid DeviceId` — Physical registered hardware identifier.
* `Guid CreatedByUserId` — Audit trail reference of initiating staff member.

### 4.3 Database & Application Security Isolation
1. **Cloud Layer (PostgreSQL):** PostgreSQL Row-Level Security (RLS) policies MUST be enabled on every tenant-scoped table. Every cloud database query executes within an active session setting `SET LOCAL app.current_tenant_id = '...'`. EF Core DbContext configures Global Query Filters (`builder.Entity<T>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId)`).
2. **Edge Layer (SQLite SQLCipher):** Local database files store data strictly for the single signed-in tenant assigned to that terminal device. Cross-tenant data co-mingling on edge devices is explicitly forbidden.

---

## 5. Local vs. Cloud Database Responsibilities & Boundaries

### 5.1 Local SQLite Database Responsibilities
* **Scope:** Single branch/outlet transactional operations for local POS terminals.
* **Storage Engine:** SQLite 3 with SQLCipher 256-bit AES encryption (`PRAGMA key`, `PRAGMA journal_mode=WAL`).
* **Allowed Data (Offline Permitted):**
  * Active Branch Configuration, Menus, Pricing rules, Section Tax Rates.
  * Local Active Orders, KOTs, BOTs, Bills, Payments for current open Day End session.
  * Local Inventory Item Masters & Counter Stock Balances.
  * Cached Ed25519 Signed Entitlement Token & Device Registration Certificate.
  * Edge Outbox Event Queue & Edge Inbox Sync State.
* **Prohibited Data (Server-Side Only):**
  * Central Tenant Billing, Subscription Payment Master Records, Global Entitlement Private Keys.
  * Cross-branch aggregated financial reports and historic multi-year analytical ledger archives.
  * Other branches' private transactional histories or customer PII across properties.

### 5.2 Cloud PostgreSQL Database Responsibilities
* **Scope:** Master system of record for all tenants, global analytics, multi-branch aggregation, compliance reporting, identity, entitlement authority, and cloud outbox/inbox broker.
* **Storage Engine:** PostgreSQL 16 with RLS policies, JSONB audit logging, partitioned transactional history tables.
* **Data Classification Matrix:**
  * **Authoritative Transactional Data:** Server-side PostgreSQL database state once synchronized and committed to master ledger.
  * **Derived & Replicated Data:** Edge SQLite local tables, materialized analytics views, cached menu items, local stock balance mirrors.

---

## 6. Offline Model & Weekly Connectivity Requirement

### 6.1 Weekly Mandatory Connectivity Requirement
* **Mandatory Product Rule:** All edge POS terminals operating in offline mode are subject to a **Mandatory Weekly Connectivity Check**. An edge terminal MUST successfully connect to the cloud Web API and complete an entitlement check-in at least once every 7 calendar days (168 hours).
* **Authoritative Server Time:** Server time returned in signed API responses is **authoritative for all connectivity, entitlement, and license expiration decisions**. Edge local system clocks are untrusted.
* **Clock Tampering Detection:** The edge application tracks monotonically increasing UTC timestamps (`LastVerifiedServerTimeUtc` and local hardware monotonic uptime ticks). If an edge terminal detects backward clock alteration, local entitlement validation fails immediately.
* **Soft Grace Period Configurability:** While weekly connectivity (7 days) is mandatory, any additional soft grace period duration (e.g., 7 days grace after expiration) is recorded as a **configurable product decision** subject to explicit Product Owner approval.

### 6.2 Edge Connectivity State Machine
The client connectivity evaluator enforces a 10-state deterministic state machine:

| Connectivity State | Operational Capability | Trigger Condition | Required Action |
| :--- | :--- | :--- | :--- |
| `Active` | Full POS, Billing & Sync | Online, active valid token | Normal operations & real-time sync. |
| `Offline-Within-Window` | Full POS & Local Outbox | Offline < 7 days (168 hrs) | Continue local operations; queue Outbox messages. |
| `Warning` | Full POS + Warning Banner | Offline 5 to 7 days | Display UI banner warning staff to connect network. |
| `Expired` | Read-Only POS | Offline > 7 days (grace active) | Block new order creation; permit receipt re-printing. |
| `Restricted` | Read-Only Lockout | Token / Grace fully expired | Block all transactional operations until re-authenticated. |
| `Synchronized` | Full POS & Idle Sync | Outbox queue drained (0 pending) | Edge and cloud databases fully aligned. |
| `Synchronization-Failed` | Full POS + Alert Indicator | Outbox upload error / network drop | Retry background sync with exponential backoff. |
| `Update-Required` | Blocked until Client Update | Server mandates client version bump | Trigger secure auto-updater download payload. |
| `Device-Suspended` | Locked Terminal | Cloud admin revoked device | Clear local auth state; require re-provisioning. |
| `Recovered` | Full POS & Sync Resumed | Cloud check-in success after lock | Restore active status; resume sync engine. |

---

## 7. Outbox/Inbox Synchronization Protocol

### 7.1 Protocol Architecture & Core Guarantees
* **Implementation Status:** Design & Contract Specification only (0% production sync code exists in baseline).
* **Sync Pattern:** Asynchronous Outbox/Inbox pattern with At-Least-Once delivery and Client-Side Idempotent Replay guarantees.

```
┌──────────────────────────────────────────────┐        ┌──────────────────────────────────────────────┐
│                  EDGE CLIENT                 │        │                 CLOUD SERVER                 │
│  ┌────────────────┐      ┌────────────────┐  │        │  ┌────────────────┐      ┌────────────────┐  │
│  │ Local Mutation │ ───► │  Edge Outbox   │  │        │  │  Cloud Inbox   │ ───► │ PostgreSQL Master│
│  └────────────────┘      └────────────────┘  │        │  └────────────────┘      └────────────────┘  │
│                                  │           │        │          ▲                   │               │
│                                  └───────────┼────────┼──────────┘                   │               │
│                                              │ HTTP POST Push                        │               │
│                                              │ (Idempotent Batch)                    │               │
│                                              │                                       ▼               │
│                                  ┌───────────┼────────┼──────────────────────────────┐               │
│                                  │           ▼        │                              │               │
│                                  │  Edge Inbox        │ ◄────────────────────────────┘               │
│                                  └────────────────────┘    HTTP GET Delta Pull                       │
└──────────────────────────────────────────────┘        └──────────────────────────────────────────────┘
```

### 7.2 Outbox Event Payload Contract
Every Outbox event generated on the edge POS terminal MUST conform to the standard schema:

```json
{
  "eventId": "018f3a5b-7c9d-7000-8000-000000000001",
  "eventType": "OrderPlacedEvent",
  "aggregateType": "Order",
  "aggregateId": "018f3a5b-7c9d-7000-8000-000000000099",
  "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "branchId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "deviceId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "sequenceNumber": 1042,
  "createdUtc": "2026-09-14T01:15:00.000Z",
  "payloadJson": "{}",
  "payloadSignature": "Ed25519_Signature_Hex"
}
```

### 7.3 Sync Operational Rules & Failure Handling
1. **Event Identity & Idempotency:** `eventId` (UUIDv7 time-ordered) is globally unique. Cloud Inbox tracks processed `eventId`s per `tenantId`. Duplicate submissions are acknowledged as successful (`HTTP 200`) without re-executing domain handlers.
2. **Device Sequence Ordering:** Outbox events from a device are processed strictly in `sequenceNumber` order per `deviceId`.
3. **Acknowledgment & Purge:** Cloud responds to batch push with acknowledged `eventId` list. Edge marks local Outbox items as `Synchronized` and retains them for a 30-day purge retention window.
4. **Retry & Failure Isolation:** Network or transient server errors trigger exponential backoff retry (1s, 2s, 4s, 8s... capped at 5 minutes). If an event payload fails domain validation (e.g., malformed schema), it is moved to local **Dead Letter Queue (DLQ)** with error diagnostics, blocking dependent events for that aggregate while unblocking unrelated aggregates.
5. **Conflict Resolution Strategy:**
   * **Financial & Inventory Transactions (Orders, Bills, Stock Deductions):** **Append-Only Event Sourcing**. Transactions are immutable sequence points; conflict overriding is prohibited.
   * **Master Data & Configurations (Menu items, Section prices):** Server Last-Write-Wins (LWW) with server authority.
   * **Lodging Room Allocation (Zero-Sum Physical Resource):** Strict pessimistic server-side check upon sync. If edge created an offline room booking that conflicts with a server booking, the sync raises a `BookingConflictException` requiring manual managerial front-desk resolution.

---

## 8. Modular Product Edition Architecture & Dynamic Capability Model

### 8.1 Unified Single-Codebase Editions
To satisfy commercial flexibility without code duplication, the system supports three core editions from a single codebase:
1. **Bar & Restaurant Only Edition:** Dining table management, KOT/BOT lifecycle, differential section pricing, stock conversion (loose peg dispensing), Maharashtra State Excise FL-III compliance.
2. **Hotel / General Hospitality Only Edition:** Guest check-in/check-out, room tariff master, reservation ledger, room housekeeping status, night audit.
3. **Hotel + Bar & Restaurant Hybrid Edition:** Combined lodging and dining operations with cross-outlet room billing transfer capability (`Post to Room`).
4. **Custom Entitlement Bundles:** Subscription-driven custom capability combinations enabled dynamically via Ed25519 cloud signed JWT tokens.

### 8.2 Architectural Prohibition of UI Conditionals
Editions MUST NOT be implemented via scattered UI `@if` conditionals or hardcoded `#if EDITION_BAR` compiler directives. Modules MUST be defined as strongly-typed capabilities evaluated consistently across all application boundaries.

### 8.3 Boundary Capability Evaluation Engine
Capabilities are evaluated using the standard `ICapabilityEvaluator` contract:

```csharp
public interface ICapabilityEvaluator
{
    bool IsCapabilityEnabled(TenantEntitlement entitlement, string capabilityCode);
}
```

* **Domain Boundary:** Aggregate methods validate capability prerequisites (e.g., `Order.PostToRoom()` verifies `CapabilityCodes.HotelRoomBilling`).
* **Application Boundary:** MediatR pipeline behaviors (`CapabilityAuthorizationBehavior<TRequest, TResponse>`) intercept CQRS requests and reject unauthorized actions with `CapabilityDeniedException`.
* **API Boundary:** ASP.NET Core authorization policies (`[AuthorizeCapability(CapabilityCodes.ExciseReports)]`) protect Web API controllers.
* **UI Boundary:** Blazor layout wrappers (`<RequireCapability Code="HotelRoomManagement">`) conditionally render navigation menus and action buttons.

---

## 9. Initial Vertical Development Strategy

### 9.1 The First Business Vertical Slice
All subsequent Jules development tasks MUST execute the first end-to-end business slice in strict sequence before expanding capability width:

```
[1. Restaurant/POS Order] ──► [2. KOT / BOT Creation] ──► [3. Bill Generation]
                                                                   │
[6. Offline Persistence]  ◄── [5. Inventory Movement] ◄── [4. Payment Split]
           │
           ▼
[7. Audit Log Entry]     ──► [8. Outbox Event Sync]   ──► [9. Cloud API Verification]
```

### 9.2 Foundation Reusability Guarantees
This initial vertical slice MUST be engineered so that all foundational infrastructure is immediately reusable by future hotel capabilities:
* **Identity & Multi-Tenancy:** Shared `TenantContext`, `BranchContext`, `UserContext`.
* **Offline & Persistence:** Shared SQLite SQLCipher EF Core DbContext, transaction units of work.
* **Outbox Sync Engine:** Shared event serialization, local outbox queue, HTTPS push protocol.
* **Hardware & Printing:** Shared ESC/POS thermal printer driver engine and QuestPDF document renderer.
* **Audit & Compliance:** Shared JSONB append-only audit logger.

---

## 10. Hotel Module Architectural Integration

### 10.1 Future Module Integration Plan
Once the initial Bar & Restaurant vertical slice is complete and verified, the Hotel & Lodging module will plug directly into the established platform foundations:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                              SHARED PLATFORM FOUNDATION                                │
│  Tenancy (TenantId/BranchId) │ Security (Ed25519) │ Outbox Sync Engine │ QuestPDF Printing  │
└───────────────────────────────────────────┬────────────────────────────────────────────┘
                                            │
                    ┌───────────────────────┴───────────────────────┐
                    ▼                                               ▼
┌───────────────────────────────────────┐       ┌───────────────────────────────────────┐
│     POS / RESTAURANT BOUNDED CONTEXT    │       │     HOTEL LODGING BOUNDED CONTEXT     │
│  - Tables, KOT/BOT, Menu Items        │       │  - Rooms, Guest Check-In / Out        │
│  - Differential Section Pricing       │       │  - Seasonal Tariff Matrix             │
│  - Loose ML Stock & Excise (FL-III)   │       │  - Advance Deposits & Guest Ledger    │
└───────────────────────────────────────┘       └───────────────────────────────────────┘
                    │                                               │
                    └───────────────────────┬───────────────────────┘
                                            ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                CROSS-CONTEXT INTEGRATION                               │
│  - "Post to Room" Settlement Handler (Transfers POS Bill to Open Guest Folio)          │
│  - Unified Night Audit & Day End Reconciliation                                         │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

### 10.2 Architectural Guarantees for Hotel Integration
1. **Zero Architecture Refactoring:** Hotel implementation will introduce new domain aggregates (`Room`, `Reservation`, `GuestFolio`) within `Yashdeep.Domain` without modifying POS core aggregates.
2. **Unified Guest Folio Settlement:** POS billing will expose an `IPaymentSettlementHandler` interface, allowing `PostToRoomSettlementHandler` to bind POS bills to active lodging folios seamlessly.
3. **Harmonized Day End:** Night Audit operations will orchestrate both lodging date roll and dining Day End audits atomically.

---

## 11. Migration Boundary & Legacy Knowledge Preservation

### 11.1 Controlled ETL Migration Boundary
Data migration from legacy Microsoft Access databases (`dinurss.mdb`) MUST be executed as an offline, controlled ETL (Extract, Transform, Load) pipeline utility (`Yashdeep.EtlTool`).
* **Strict Boundary Isolation:** Migration tools and Jet OLEDB drivers **must never be compiled into or shipped with the modern customer runtime client or server application**.
* **ETL Pipeline Lifecycle:** Legacy MDB → Schema Transformation & Validation → Reconciliation Check → PostgreSQL Cloud Ingestion.

```
┌────────────────────┐      ┌─────────────────────────┐      ┌──────────────────────────┐
│ Legacy Access MDB  │ ───► │ Offline Migration ETL   │ ───► │ Target PostgreSQL Cloud  │
│ (dinurss.mdb)      │      │ (Yashdeep.EtlTool)      │      │ (Clean SaaS Architecture)│
└────────────────────┘      └─────────────────────────┘      └──────────────────────────┘
  LEGACY ARCHIVE SOURCE          ISOLATED CONVERSION TOOL         MODERN PRODUCTION RUNTIME
```

### 11.2 Legacy Knowledge Preservation Matrix

| Knowledge Category | Legacy Element to DISCARD | Business Knowledge to PRESERVE & IMPLEMENT |
| :--- | :--- | :--- |
| **Database Engine** | Jet 4.0, `.mdb` binary locks, unindexed MS Access tables | 105 domain entity relationships, exact tax fields, historic sales records. |
| **Billing & POS** | WinForms UI handlers, raw SQL string queries in VB code | Table lifecycle states, KOT item customization, split payment modes. |
| **Excise Legal** | Hardcoded Crystal Reports `.rpt` templates | Maharashtra FL-III legal compliance: Form FL-9, D-7, Brand-wise ML tracking, peg conversions (30/60/90/180/375/750ml). |
| **Pricing Rules** | Scattered VB.NET `If...Else` section price overrides | Multi-section differential pricing (Permit Room, AC Dining, Garden, Bar). |
| **Day End Closing** | Physical table copying into `*_Dayend` Access tables | Atomic Day End closing audit, daily stock snapshotting, ledger roll. |

---

## 12. Security, Cryptography & Privacy Specifications

### 12.1 Cryptographic & Security Controls Matrix
* **Database Encryption at Rest (Edge):** SQLite SQLCipher using 256-bit AES encryption. Key derived via PBKDF2 with unique per-device salt stored in OS Secure Storage (Windows Credential Manager / Android KeyStore).
* **Entitlement Signing:** Asymmetric Ed25519 signature verification. Cloud holds Private Key; Client holds embedded Public Key to cryptographically verify license JWT tokens offline.
* **Network Security in Transit:** TLS 1.3 enforced for all Web API HTTPS communication. Certificate pinning enabled on mobile/desktop edge clients.
* **Authentication & Credentials:** ASP.NET Core Identity on cloud server. Password hashing via bcrypt (work factor 12) or Argon2id / PBKDF2. Passwords in plain text are strictly forbidden.
* **Audit Records:** Append-only JSONB audit logs for all financial mutations, price changes, table cancellations, and stock adjustments.
* **Software Updates:** Automatic updates distributed via HTTPS. Update manifest and installer binaries MUST be signed with Ed25519 signature before execution on edge devices.

---

## 13. Quality Engineering & Testing Gates

### 13.1 Mandatory PR Verification Gates
Every future implementation Pull Request (PR) MUST include corresponding automated tests satisfying the following coverage standard:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                              MANDATORY TESTING PIPELINE GATES                          │
│                                                                                        │
│  1. Unit Tests (`Yashdeep.Domain.Tests`)                                               │
│     - Pure business rules, excise peg conversions, section price calculations.          │
│                                                                                        │
│  2. Application & Capability Tests (`Yashdeep.Application.Tests`)                      │
│     - CQRS command handlers, MediatR pipeline behaviors, entitlement checks.           │
│                                                                                        │
│  3. Persistence & RLS Integration Tests (`Yashdeep.Persistence.Tests`)                 │
│     - SQLite SQLCipher local transactions, PostgreSQL EF Core Query Filters & RLS.     │
│                                                                                        │
│  4. Outbox Sync & Idempotency Tests (`Yashdeep.SyncEngine.Tests`)                      │
│     - Outbox queue event ordering, duplicate replay rejection, DLQ handling.           │
│                                                                                        │
│  5. Security & Isolation Tests (`Yashdeep.Security.Tests`)                             │
│     - Cross-tenant data leak prevention, Ed25519 token forgery rejection.              │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

### 13.2 Quality Gate Thresholds
* **Build Zero Warnings Policy:** Modern C# projects must compile with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
* **Tenant Leak Zero Tolerance:** Integration tests MUST attempt cross-tenant query access and assert `0` records returned.
* **Offline Resilience Gate:** Unit tests MUST verify full POS checkout completion when network connection interfaces are mocked as offline.

---

## 14. Out of Scope for Implementation Phase

To maintain focus on core modern SaaS application development, the following DevOps and infrastructure tasks are **EXPLICITLY OUT OF SCOPE** during the initial implementation phase:
1. Docker containerization setup or multi-stage Dockerfile tuning.
2. Kubernetes (k8s) helm charts, deployment manifests, or cluster orchestration.
3. Cloud infrastructure automation scripts (Terraform, Pulumi, Bicep, ARM templates).
4. Complex CI/CD production pipeline automation or cloud cloud-provider provisioning.
5. Unrelated repository restructuring or non-essential environment refactoring.

---

## 15. Final Decision Matrix

| Item / Topic | Contract Decision Category | Decision Details & Directives |
| :--- | :--- | :--- |
| **.NET 9 + C# 13 + Blazor Hybrid Stack** | **Authoritative and Approved** | Final approved modern application stack across all client and cloud components. |
| **PostgreSQL 16 + RLS Cloud DB** | **Authoritative and Approved** | Cloud system of record with mandatory Row-Level Security isolation. |
| **SQLite SQLCipher 256-bit AES Edge DB** | **Authoritative and Approved** | Encrypted local edge database engine operating in WAL journal mode. |
| **Outbox/Inbox Sync Architecture** | **Authoritative and Approved** | Asynchronous event push/pull sync protocol with client-side idempotency. |
| **QuestPDF + ESC/POS Printing Engine** | **Authoritative and Approved** | QuestPDF for compliance reports & ESC/POS binary streams for thermal printing. |
| **Single Repository / Codebase for Editions** | **Authoritative and Approved** | Capability-driven single codebase for Bar, Restaurant, Hotel, and Hybrid editions. |
| **Legacy Access / MDB Exclusion** | **Authoritative and Approved** | Access, MDB, Jet 4.0, and RSS.exe are migration sources ONLY; zero runtime dependency. |
| **Direct Cloud Database Connections** | **Authoritative and Approved** | Clients connect exclusively through cloud Web API endpoints; direct DB connections prohibited. |
| **Server Authority for Time & Licensing** | **Authoritative and Approved** | Server time is authoritative for connectivity window and entitlement calculations. |
| **Mandatory 7-Day Weekly Connectivity Check**| **Authoritative and Approved** | POS must check in online at least once every 168 hours to maintain full offline POS privileges. |
| **Soft Grace Period Duration** | **Product-Owner Decision Required** | Grace duration after 7 days (e.g., 7-day soft grace period vs 0-day) pending explicit PO sign-off. |
| **Lodging Double-Booking Escalation Workflow**| **Product-Owner Decision Required** | Specific UI workflow for manual resolution when offline room bookings collide pending PO review. |
| **Marathi Rasterization Custom Font Assets** | **Required but Implementation Detail Open**| Selection of specific Noto Sans Devanagari font asset package for ESC/POS bitmap generation. |
| **Local SQLite Encryption Key Storage** | **Recommended Default** | Secure OS Storage (Windows Credential Manager / Android KeyStore) with fallback salt. |
| **Separate Codebases for Product Editions** | **Rejected Approach** | Explicitly rejected. Multi-edition logic must be handled via dynamic capability evaluation. |
| **Client Direct Connection to Cloud Postgres** | **Rejected Approach** | Explicitly rejected due to severe security, tenant isolation, and connection scaling risks. |
| **Crystal Reports Runtime Engine** | **Rejected Approach** | Explicitly rejected. Replaced 100% by code-first QuestPDF reporting engine. |

---

## 16. Classification of Existing Documentation

All existing architectural and assessment documentation in the repository is classified against this authoritative contract:

| Document Path | Document Title | Classification Status | Reconciliation & Contradiction Resolution Notes |
| :--- | :--- | :--- | :--- |
| `SYSTEM_ARCHITECTURE.md` | Master System Architecture Blueprint | **Authoritative & Subordinated** | Aligned. Contract formalizes single-codebase capability model and Outbox protocol. |
| `ARCHITECTURE_REVIEW.md` | Architecture Review Board Report | **Authoritative & Subordinated** | Aligned. Resolves legacy contradictions; grace period details deferred to PO decision. |
| `DOMAIN_MODEL.md` | Canonical Domain Model Specification | **Authoritative & Subordinated** | Aligned. Defines aggregates, entity lifecycles, and tenant ID requirements. |
| `SAAS_ARCHITECTURE.md` | Multi-Tenant SaaS Architecture | **Authoritative & Subordinated** | Aligned. Governs 8-level hierarchy, PostgreSQL RLS, and global query filters. |
| `SECURITY_ARCHITECTURE.md` | Security Architecture | **Authoritative & Subordinated** | Aligned. Specifies ASP.NET Core Identity, JWT bearer auth, and secret isolation. |
| `IP_PROTECTION.md` | IP Protection Architecture | **Authoritative & Subordinated** | Aligned. Specifies client protection, code obfuscation, and key isolation. |
| `OFFLINE_ARCHITECTURE.md` | Offline Architecture & Resilience | **Authoritative & Subordinated** | Aligned. Governs SQLite SQLCipher, Outbox queue, and offline state machine. |
| `MIGRATION_ARCHITECTURE.md` | Migration Architecture | **Authoritative & Subordinated** | Aligned. Establishes isolated offline ETL boundary for legacy `dinurss.mdb`. |
| `SUBSCRIPTION_ARCHITECTURE.md` | Subscription Architecture | **Authoritative & Subordinated** | Aligned. Governs commercial tiers and subscription state transitions. |
| `ENTITLEMENT_MODEL.md` | Entitlement Model & Offline Token Spec | **Authoritative & Subordinated** | Aligned. Specifies Ed25519 token signatures and dynamic capability evaluation. |
| `DEVICE_MANAGEMENT.md` | Device Provisioning & Revocation | **Authoritative & Subordinated** | Aligned. Governs hardware device identity and cloud revocation workflows. |
| `BILLING_ARCHITECTURE.md` | Billing & Payment Architecture | **Authoritative & Subordinated** | Aligned. Governs POS billing, KOT/BOT lifecycle, and split payments. |
| `PRINTING_ARCHITECTURE.md` | Printing & Hardware Architecture | **Authoritative & Subordinated** | Aligned. Governs ESC/POS binary driver engine and Marathi bitmap printing. |
| `REPORTING_ARCHITECTURE.md` | Reporting Architecture | **Authoritative & Subordinated** | Aligned. Governs QuestPDF code-first report definitions and exports. |
| `INVENTORY_ARCHITECTURE.md` | Inventory Architecture | **Authoritative & Subordinated** | Aligned. Governs multi-tier stock (Godown, Counter) and loose peg conversions. |
| `EXCISE_ARCHITECTURE.md` | Excise Compliance Architecture | **Authoritative & Subordinated** | Aligned. Governs Maharashtra State Excise (FL-III) Form FL-9/D-7 compliance. |
| `BUSINESS_LOGIC.md` | Business Logic & Operational Workflows | **Authoritative & Subordinated** | Aligned. Legacy business rules preserved for modern domain re-implementation. |
| `LEGACY_SYSTEM_ANALYSIS.md` | Legacy System Reverse Engineering | **Authoritative & Subordinated** | Aligned. Technical implementation discarded; domain logic preserved. |
| `docs/assessment/*` | Product Assessment Reports (6 files) | **Authoritative & Subordinated** | Aligned. Baseline assessment confirming 0% modern source code implementation. |
| `docs/verification/*` | Verification Audit Reports (10 files) | **Authoritative & Subordinated** | Aligned. Verification evidence confirming architecture baseline and audit gaps. |

---
**End of Implementation Contract.**
