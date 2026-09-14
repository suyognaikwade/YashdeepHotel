# Authoritative Modern SaaS Implementation Contract

**Document Status:** Freeze / Authoritative Master Implementation Contract (Amended Baseline)
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
* **Specification vs. Code Status:** Architecture specifications, package references, interface definitions, schema DDLs, and design blueprints represent **approved target architectures**. They MUST NOT be construed as completed production functionality or pre-existing source code.
* **Operational Scope:** This document freezes the contract for transition from the architecture phase to initial vertical slice implementation.

### 1.2 Non-Negotiable Technical Directives
1. **Migration-Only Legacy Boundary:** Microsoft Access, `.mdb` files, OLEDB/Jet 4.0 drivers, Crystal Reports, and the legacy VB.NET `RSS.exe` WinForms application are strictly migration and reference sources. **The customer runtime must NEVER depend on Microsoft Access, Jet, ACE, Crystal Reports, or the legacy WinForms executable.**
2. **Strict Cloud API Mediation:** Production clients (Blazor Hybrid desktop/mobile terminals, web clients) **NEVER connect directly to PostgreSQL or any cloud database**. All cloud data access and mutations MUST pass through HTTPS REST API endpoints (supplemented by SignalR for real-time notifications and server-push events where appropriate) mediated by strict authentication and tenant context filters. gRPC is NOT required as an edge synchronization protocol unless an explicitly approved future ARB decision introduces it.
3. **Single Codebase Architecture & Required Target Platforms:** Multi-edition product variants (Bar & Restaurant, Hotel, Hybrid, Custom bundles) MUST be delivered from a **single repository and single unified codebase**. Creating separate codebases, repositories, or compiled branches for different product editions is strictly prohibited. **Windows and Android remain required target platforms.**
4. **Prohibition of Direct Cloud DB Access:** Direct client connections to cloud PostgreSQL databases, raw direct-to-cloud SQL client connections, and bypass of cloud Web API authorization are **strictly prohibited**.
5. **Production Secrets Integrity:** Plain-text passwords, private keys, database credentials, or signing keys must never be committed to repository documentation or code. Production configurations must utilize environment variables or secure key vaults.

---

## 2. Target Technology Stack & Repository Structure

### 2.1 Approved Target Technology Stack
* **Cloud API & Backend:** .NET 9 Web API, C# 13, EF Core 9.
* **Cloud Database:** PostgreSQL 16 with Row-Level Security (RLS), JSONB audit trails, and transactional Outbox/Inbox tables.
* **Edge / Local POS Storage:** SQLite with SQLCipher (256-bit AES encryption), EF Core 9 SQLite provider, Write-Ahead Logging (WAL) mode.
* **Client Frontend:** Blazor Hybrid (.NET MAUI cross-platform host for Windows & Android terminals; responsive WebAssembly/Server for Cloud Management Portal).
* **Edge-to-Cloud Sync Transport:** HTTPS REST JSON API endpoints for Outbox/Inbox batch event synchronization, with SignalR for real-time notifications and server-push events where appropriate. (gRPC is NOT required for edge sync).
* **Document & Thermal Printing:** QuestPDF (C# code-first layout engine for A4/A5 compliance reports & invoices) + ESC/POS binary driver engine for 58mm/80mm thermal receipt printers with Marathi/Devanagari rasterization support.
* **Security & Entitlements:** Ed25519 asymmetric cryptographic token validation for dynamic capability entitlements. ASP.NET Core Identity with **Argon2id** as the single approved production password-hashing strategy, and JWT bearer authentication with tenant claims.

*Note: All technology specifications describe approved target technologies for future implementation tasks, NOT completed production code.*

### 2.2 Standardized Solution Structure
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
└── tests/                                 # Automated test suites (unit, integration, sync)
```

---

## 3. Architectural & Component Boundaries

### 3.1 Explicit Responsibility Split Across Architecture Layers
To ensure Clean Architecture principles are strictly preserved and infrastructure logic never leaks into core domain business rules, responsibilities are partitioned as follows:

* **Domain Layer (`Yashdeep.Domain`):** Pure domain entities, value objects, domain aggregates, domain events, business invariants, and repository interface definitions. **Zero external dependencies** (no EF Core, no HTTP, no JSON serialization attributes, no hardware driver references).
* **Application Layer (`Yashdeep.Application`):** Use case orchestrations, CQRS commands and queries (MediatR), FluentValidation validators, application DTOs, capability authorization pipeline behaviors, and interface contracts.
* **Persistence Layer (`Yashdeep.Persistence.*`):** EF Core DbContext implementations (Cloud PostgreSQL and Edge SQLite SQLCipher), Global Query Filters, database migrations, and repository implementations.
* **Infrastructure Layer (`Yashdeep.Infrastructure`):** External hardware driver implementations (ESC/POS thermal printing engine, Marathi rasterizer), QuestPDF document renderers, cryptographic operations, and external notification adapters.
* **Synchronization Layer (`Yashdeep.SyncEngine`):** Edge Outbox event queueing, Cloud Inbox idempotency tracking, HTTP event payload batching, retry policies with exponential backoff, Dead-Letter Queue (DLQ) isolation, and conflict handling.
* **API Layer (`Yashdeep.Server.Api`):** ASP.NET Core Web API controllers, tenant context middleware, JWT authorization handlers, SignalR hubs for real-time events, and OpenAPI/Swagger specifications.
* **Client Layer (`Yashdeep.Client.*`):** Blazor Hybrid UI components, Razor views, Blazor view models, and native .NET MAUI desktop/mobile shell hosts for Windows and Android.

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
                                         Physical USB/LAN            HTTPS REST JSON Sync
                                               │                     (+ SignalR Events)
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

### 4.2 Contextual Identifiers on Transactional Entities
Transactional entities (e.g., `Order`, `KOT`, `Bill`, `Payment`, `StockMovement`, `ExciseRegisterEntry`, `AuditLog`) MUST carry the contextual identifiers required by the approved domain model and security boundary (minimally `TenantId` and `BranchId`, plus `OutletId` or `CreatedByUserId` where applicable).

**Identifier Optimization Rule:** Arbitrary duplication of all 8 hierarchy identifiers on every single transactional record is NOT required if ownership can be derived safely through authoritative relationships (for example, deriving `TerminalId` or `OrganizationId` from an active terminal session or branch context). Every transactional record must nevertheless be provably tenant-scoped and branch-scoped in database queries and domain invariants.

### 4.3 Database & Application Security Isolation
1. **Cloud Layer (PostgreSQL):** Defense in depth is mandatory. Database-level Row-Level Security (RLS) policies MUST be enabled on every tenant-scoped table (`SET LOCAL app.current_tenant_id = '...'`). Application-level EF Core DbContext instances configure Global Query Filters (`builder.Entity<T>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId)`).
2. **Edge Layer (SQLite SQLCipher):** Local database files store data strictly for the single signed-in tenant and branch assigned to that terminal device. Cross-tenant data co-mingling on edge devices is explicitly forbidden.

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

## 6. Offline Model & Operational Connectivity States

### 6.1 Weekly Mandatory Connectivity Requirement
* **Mandatory Product Rule:** All edge POS terminals operating in offline mode are subject to a **Mandatory Weekly Connectivity Check**. An edge terminal MUST successfully connect to the cloud Web API and complete an entitlement check-in at least once every 7 calendar days (168 hours).
* **Authoritative Server Time:** Server time returned in signed API check-in responses is **authoritative for all connectivity, entitlement, and license expiration decisions**. Edge local system clocks are untrusted.
* **Clock Tampering Detection:** The edge application tracks monotonically increasing UTC timestamps (`LastVerifiedServerTimeUtc` and local hardware monotonic uptime ticks). If an edge terminal detects backward clock alteration, local entitlement validation fails immediately.
* **Configurable Soft Grace Period:** The mandatory connectivity check occurs every 7 days (168 hours). Any additional soft grace period duration beyond 7 days is a **configurable Product Owner decision** and MUST NOT be silently fixed to fourteen total days or any fixed default without explicit Product Owner sign-off.

### 6.2 Explicitly Defined Operational Connectivity States
The client connectivity evaluator manages 8 explicitly defined operational connectivity states:

1. **Normal offline operation (`Active` / `Offline-Within-Window`):** Operational mode when an edge terminal is offline but within the valid 7-day (168-hour) window. Full POS order entry, billing, payment processing, thermal printing, and local Outbox event generation are fully enabled.
2. **Connectivity warning:** Operational state triggered when the edge terminal has been continuously offline for 5 to 7 days (120 to 168 hours). Full POS operations remain enabled, but a prominent non-blocking UI banner warns staff to connect the terminal to the network before expiration.
3. **Connectivity expiry:** Operational state triggered when continuous offline duration exceeds 7 days (168 hours). The terminal enters license expiration evaluation pending online check-in or configurable soft grace period validation.
4. **Restricted operation:** Operational state triggered when the connectivity check and any configured Product Owner soft grace period have fully expired without online check-in. Terminal enters read-only lockout mode: creating new orders, bills, or payments is blocked, but historical receipt re-printing and local data inspection remain accessible.
5. **Synchronization failure:** Operational state triggered when Outbox batch upload encounters network drops or transient server errors. Local POS operations continue normally while background sync retries with exponential backoff and Dead-Letter Queue (DLQ) isolation.
6. **Update requirement:** Operational state triggered when the cloud Web API mandates a client software version update. Terminal blocks transactional operations until the signed client update package is applied.
7. **Device suspension:** Operational state triggered when a cloud administrator revokes device authorization. Local session authentication state is cleared, and the device requires managerial re-provisioning.
8. **Recovery:** Transition state triggered when online check-in completes successfully after a warning, expiry, or failure state. Restores normal active status and resumes background sync processing.

---

## 7. Outbox/Inbox Synchronization Protocol & Strategy

### 7.1 Protocol Architecture & Transport
* **Implementation Status:** Design & Contract Specification (0% production sync code exists in baseline).
* **Sync Transport:** HTTPS REST JSON API endpoints for event batch push/pull synchronization. SignalR may be used for real-time notifications and server-push events where appropriate. gRPC is NOT required for edge sync.
* **Sync Pattern:** Asynchronous Outbox/Inbox pattern with At-Least-Once delivery and Client-Side Idempotent Replay guarantees.

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

### 7.3 Incremental Synchronization Strategy for Initial Vertical Slice
The required initial business sequence is:

```
[1. Restaurant/POS Order] ──► [2. KOT / BOT Creation] ──► [3. Bill Generation]
                                                                   │
[6. Offline Persistence]  ◄── [5. Inventory Movement] ◄── [4. Payment Split]
           │
           ▼
[7. Audit Log Entry]     ──► [8. Outbox Event Sync]   ──► [9. Cloud API Verification]
```

**Incremental Implementation Strategy:** This sequence does **NOT** imply that all advanced synchronization complexity (such as multi-device conflict resolution, cross-branch merges, or complex DLQ inspection UI) must be fully completed before the first local transactional POS workflow can be demonstrated.

The implementation strategy permits establishing foundational synchronization contracts and a minimal safe synchronization implementation (e.g. local Outbox event generation and basic HTTPS event batch push) while advanced conflict-handling capabilities are added incrementally in subsequent tasks.

---

## 8. Modular Product Edition Architecture & Dynamic Capability Model

### 8.1 Unified Single-Codebase Editions
To satisfy commercial flexibility without code duplication, the system supports four edition variants from a **single repository and single unified codebase**:
1. **Bar & Restaurant Only Edition:** Dining table management, KOT/BOT lifecycle, differential section pricing, stock conversion (loose peg dispensing), Maharashtra State Excise FL-III compliance.
2. **Hotel / General Hospitality Only Edition:** Guest check-in/check-out, room tariff master, reservation ledger, room housekeeping status, night audit.
3. **Hotel + Bar & Restaurant Hybrid Edition:** Combined lodging and dining operations with cross-outlet room billing transfer capability (`Post to Room`).
4. **Custom Entitlement Bundles:** Subscription-driven custom capability combinations enabled dynamically via Ed25519 cloud-signed JWT tokens.

### 8.2 Architectural Prohibition of UI Conditionals
Editions MUST NOT be implemented via scattered UI `@if` conditionals or hardcoded `#if EDITION_BAR` compiler directives. Modules MUST be defined as strongly-typed capabilities evaluated consistently across all application boundaries using the `ICapabilityEvaluator` contract.

---

## 9. Initial Vertical Development Strategy

### 9.1 The First Business Vertical Slice
All subsequent Jules development tasks MUST execute the first end-to-end business slice in strict sequence before expanding capability width:

```
Restaurant/POS Order → KOT/BOT → Bill → Payment → Inventory Movement → Audit → Offline Persistence → Synchronization → Cloud Verification
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
Once the initial Bar & Restaurant vertical slice is complete and verified, the Hotel & Lodging module will plug directly into the established platform foundations without modifying POS core aggregates.

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

---

## 12. Security, Cryptography & Privacy Specifications

### 12.1 Cryptographic & Security Specifications
* **Database Encryption at Rest (Edge):** SQLite SQLCipher using 256-bit AES encryption (`PRAGMA key`, `PRAGMA journal_mode=WAL`). Key derived via PBKDF2 with unique per-device salt stored in OS Secure Storage (Windows Credential Manager / Android KeyStore).
* **Entitlement Signing:** Asymmetric Ed25519 signature verification. Cloud holds Private Key; Client holds embedded Public Key to cryptographically verify license JWT tokens offline. Key handling, rotation, storage, and implementation details must follow secure implementation review before identity deployment.
* **Network Security in Transit:** TLS 1.3 enforced for all Web API HTTPS communication. Certificate pinning enabled on mobile/desktop edge clients.
* **Password Hashing Strategy:** **Argon2id** is frozen as the single approved production password-hashing strategy for ASP.NET Core Identity on the cloud server. Plain-text passwords and legacy hashes are strictly prohibited.
* **Software Update Signing:** Software updates are distributed via HTTPS using platform-standard application package delivery (Windows MSIX package signing, Android APK signatures), supplemented by Ed25519 signed manifest verification before execution.

---

## 13. Quality Engineering & Testing Gates

### 13.1 Mandatory PR Testing Categories & Boundaries
Every future implementation Pull Request (PR) MUST include corresponding automated tests satisfying the following required testing categories and boundaries:

1. **Domain Unit Tests:** Pure business rules, excise peg conversions, differential section price calculations, and domain invariants.
2. **Application & Capability Tests:** CQRS command and query handlers, MediatR pipeline behaviors, FluentValidation rules, and dynamic capability authorization.
3. **Persistence & Isolation Integration Tests:** Local SQLite SQLCipher transactions, PostgreSQL EF Core Query Filters, and Row-Level Security (RLS) policies.
4. **Outbox Sync & Idempotency Tests:** Outbox queue event ordering, duplicate payload replay rejection, and DLQ handling.
5. **Security & Multi-Tenant Tests:** Cross-tenant query data leak prevention and cryptographic token forgery rejection.

*Note: Testing gates define required test categories and coverage boundaries while allowing the exact solution project layout to follow the approved architecture without imposing rigid project file names.*

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
4. Complex CI/CD production pipeline automation or cloud provider provisioning.
5. Unrelated repository restructuring or non-essential environment refactoring.

---

## 15. Final Decision Matrix

| Item / Topic | Contract Decision Category | Decision Details & Directives |
| :--- | :--- | :--- |
| **.NET 9 + C# 13 + Blazor Hybrid Stack** | **Authoritative and Approved** | Final approved modern application stack across all client and cloud components. |
| **PostgreSQL 16 + RLS Cloud DB** | **Authoritative and Approved** | Cloud system of record with mandatory Row-Level Security isolation. |
| **SQLite SQLCipher 256-bit AES Edge DB** | **Authoritative and Approved** | Encrypted local edge database engine operating in WAL journal mode. |
| **HTTPS REST JSON Edge Sync Transport** | **Authoritative and Approved** | Edge-to-cloud Outbox/Inbox batch sync via HTTPS REST JSON; SignalR for real-time events. |
| **Argon2id Password Hashing** | **Authoritative and Approved** | Single approved production password-hashing strategy for identity. |
| **QuestPDF + ESC/POS Printing Engine** | **Authoritative and Approved** | QuestPDF for compliance reports & ESC/POS binary streams for thermal printing. |
| **Single Repository / Codebase for Editions** | **Authoritative and Approved** | Capability-driven single codebase for Bar, Restaurant, Hotel, and Hybrid editions. |
| **Required Target Platforms** | **Authoritative and Approved** | Windows and Android remain required target client platforms. |
| **Legacy Access / MDB Exclusion** | **Authoritative and Approved** | Access, MDB, Jet 4.0, and RSS.exe are migration sources ONLY; zero runtime dependency. |
| **Direct Cloud Database Connections** | **Authoritative and Approved** | Clients connect exclusively through cloud Web API endpoints; direct DB connections prohibited. |
| **Server Authority for Time & Licensing** | **Authoritative and Approved** | Server time is authoritative for connectivity window and entitlement calculations. |
| **Mandatory 7-Day Weekly Connectivity Check**| **Authoritative and Approved** | POS must check in online at least once every 168 hours to maintain full offline POS privileges. |
| **Soft Grace Period Duration** | **Product-Owner Decision Required** | Grace duration beyond 7 days pending explicit Product Owner sign-off (must not be fixed to 14 days). |
| **Lodging Double-Booking Escalation Workflow**| **Product-Owner Decision Required** | Specific UI workflow for manual resolution when offline room bookings collide pending PO review. |
| **gRPC as Edge Sync Protocol** | **Rejected Approach** | gRPC is NOT required for edge sync unless an approved ARB decision explicitly introduces it. |
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

## 17. Implementation Rules for Future Jules Tasks

Every future Jules implementation task MUST strictly obey the following core directives:

1. **Identify Dependency Prerequisites:** Prior to writing code or creating solution files, each task must inspect existing architectural contracts and abstractions, explicitly identifying all required dependency prerequisites.
2. **Do Not Duplicate Established Platform Abstractions:** Tasks must reuse established platform abstractions, contexts, and contracts (e.g. `TenantContext`, `ICapabilityEvaluator`, standard DTOs). Creating parallel, redundant, or competing context classes is strictly prohibited.
3. **Do Not Introduce Competing Frameworks:** Implementations must strictly adhere to the approved technology stack (.NET 9, Blazor Hybrid, EF Core, QuestPDF, MediatR, FluentValidation). Introducing unapproved third-party frameworks or duplicate libraries is forbidden.
4. **Do Not Bypass Tenant Authorization:** All domain queries, application handlers, and API endpoints must enforce tenant authorization through context filters or MediatR pipeline behaviors. Direct un-partitioned data access is prohibited.
5. **Do Not Connect Directly to Cloud Databases:** Client applications and edge terminals must never connect directly to cloud PostgreSQL databases; all cloud data access must pass through cloud Web API endpoints.
6. **Do Not Use Access/Jet at Runtime:** Microsoft Access, `.mdb` files, Jet OLEDB drivers, and Crystal Reports engines must NEVER be imported, referenced, or executed in customer runtime code.
7. **Include Automated Tests:** Every code modification, new component, or feature addition must include corresponding automated unit or integration tests verifying the functionality and preventing regressions.

---

## 18. Change Log

| Amendment Version | Date | Section / Area | Summary of Corrections & Clarifications |
| :--- | :--- | :--- | :--- |
| **v1.0.0** | Sep 2026 | Initial Contract Freeze | Frozen baseline implementation contract (PR #33). |
| **v1.1.0** | Sep 2026 | Sec 1.2, 2.1, 3.2, 7.1, 15 | Clarified client-to-cloud sync transport as HTTPS REST JSON API, with SignalR for real-time notifications/server-push events where appropriate; removed gRPC as required edge sync protocol. |
| **v1.1.0** | Sep 2026 | Sec 1.1, 2.1 | Clarified that target technology specifications represent approved specs, not completed production functionality. |
| **v1.1.0** | Sep 2026 | Sec 6.1, 6.2, 15 | Explicitly defined the 8 operational connectivity states (Normal offline operation, Connectivity warning, Connectivity expiry, Restricted operation, Synchronization failure, Update requirement, Device suspension, Recovery); clarified soft grace period as configurable PO decision (must not be fixed to 14 days). |
| **v1.1.0** | Sep 2026 | Sec 7.3 | Clarified that the first vertical slice sequence does not require all sync complexity before local POS demonstration, permitting foundational sync contracts and incremental conflict resolution. |
| **v1.1.0** | Sep 2026 | Sec 4.2 | Clarified tenant contextual identifier rules on transactional records, eliminating redundant field duplication where ownership is derived safely through authoritative relationships. |
| **v1.1.0** | Sep 2026 | Sec 3.1 | Explicitly defined responsibility splits across Domain, Application, Persistence, Infrastructure, Synchronization, API, and Client layers to prevent infrastructure leaking into Domain. |
| **v1.1.0** | Sep 2026 | Sec 2.1, 12.1, 15 | Froze Argon2id as the single approved production password-hashing strategy, eliminating multi-option ambiguity. |
| **v1.1.0** | Sep 2026 | Sec 12.1 | Aligned software update signing terminology with platform update mechanisms (MSIX/APK and Ed25519 manifest verification). |
| **v1.1.0** | Sep 2026 | Sec 13.1 | Reconfigured testing gates to define required test categories and coverage boundaries without forcing rigid project file names. |
| **v1.1.0** | Sep 2026 | Sec 17 | Added mandatory "Implementation Rules for Future Jules Tasks" governing future agent development. |

---
**End of Implementation Contract.**
