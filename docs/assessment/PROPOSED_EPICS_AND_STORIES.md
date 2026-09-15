# Proposed Epics, User Stories, and Recommended Implementation Tasks

**Repository**: `https://github.com/suyognaikwade/YashdeepHotel`
**Target Branch**: `botify`
**Purpose**: Comprehensive backlog of technical and functional epics, user stories, and the explicit first 30 recommended Jules tasks in dependency order.

---

## 1. Master Epic Summary Table

| Epic Identifier | Epic Title | Primary Scope & Objective | Suggested Priority | Target Phase |
| :--- | :--- | :--- | :--- | :--- |
| **EPIC-01** | Tenant & Multi-Branch Foundations | Tenant isolation, PostgreSQL RLS, EF Core Global Query Filters, Branch hierarchy. | P0 (Critical) | Phase 1-2 |
| **EPIC-02** | Local SQLite Offline Engine | SQLCipher AES-256 local database, operating working set, edge transaction context. | P0 (Critical) | Phase 2 |
| **EPIC-03** | Subscriptions & Entitlement Model | Ed25519 token validation, mandatory 7-day connectivity timer, grace periods, editions. | P0 (Critical) | Phase 3 |
| **EPIC-04** | Outbox/Inbox Synchronization | Atomic local outbox queue, background sync worker, Cloud Inbox idempotency API. | P0 (Critical) | Phase 4 |
| **EPIC-05** | Security, Identity & Device Trust | Hardware fingerprinting, device registration, role-based access control, security logs. | P1 (High) | Phase 3 |
| **EPIC-06** | Restaurant, KOT & POS Workflows | Table sections, bilingual Marathi/English KOTs, BOT routing, keyboard-first POS. | P0 (Critical) | Phase 5A |
| **EPIC-07** | Billing, Split Taxes & Dynamic UPI | Order billing, CGST/SGST/VAT engines, dynamic UPI payment QR codes, split payments. | P0 (Critical) | Phase 5A |
| **EPIC-08** | Multi-Tier Liquor Inventory | Godown -> Counter -> Loose ML auto-conversions, peg deductions (30/60/90ml). | P0 (Critical) | Phase 5B |
| **EPIC-09** | Maharashtra FL-III Excise Compliance | Permit holders, FrmDailyBulkLitre generator, Monthly Register 1, statutory returns. | P0 (Critical) | Phase 5B |
| **EPIC-10** | Hardware Thermal Printing | ESC/POS byte generator, USB/LAN/Bluetooth print drivers, paper-out fallback. | P0 (Critical) | Phase 6 |
| **EPIC-11** | Day End Settlement & Audit | Table clearance check, business date advancing, closing stock calculation, audit log. | P0 (Critical) | Phase 5A |
| **EPIC-12** | Hotel & Hospitality Management | Room types, reservation grid, guest check-in/out, housekeeping, guest ledgers. | P2 (Medium) | Phase 5C |
| **EPIC-13** | Access MDB Data Migration | Jet 4.0 OleDb reader, data transformation ETL, reconciliation engine, dry-run mode. | P1 (High) | Phase 8 |
| **EPIC-14** | Client Auto-Updater | Velopack update driver, Authenticode signature checks, background installer. | P1 (High) | Phase 8 |
| **EPIC-15** | QuestPDF Reporting Engine | Code-first QuestPDF templates for Itemwise Sales, Tax Registers, Excise Returns. | P1 (High) | Phase 6 |

---

## 2. Recommended First 30 Jules Implementation Tasks (In Dependency Order)

The following 30 tasks are strictly ordered by technical dependencies. Each task is self-contained, specifies prerequisites, affected areas, deliverables, required tests, and parallelization safety.

```
+---------------------------------------------------------------------------------------+
| TASK-01: Monorepo Directory Scaffold & Architecture Solution Configuration            |
+---------------------------------------------------------------------------------------+
| Objective: Establish standard C# monorepo directory tree under /src and /tests.        |
| Prerequisites: None                                                                   |
| Affected Areas: /src, /tests, .gitignore, Directory.Build.props                      |
| Deliverable: Monorepo structure with empty building block .csproj files.              |
| Required Tests: Build validation script.                                              |
| Parallel Guidance: Sequential (must be executed first).                               |
| Risk Level: Low                                                                       |
+---------------------------------------------------------------------------------------+
| TASK-02: Core Domain Abstractions & Base Contracts                                    |
+---------------------------------------------------------------------------------------+
| Objective: Implement BaseEntity, AggregateRoot, IDomainEvent, Result<T> in Core.      |
| Prerequisites: TASK-01                                                                |
| Affected Areas: /src/BuildingBlocks/Yashdeep.Core                                     |
| Deliverable: Core domain interfaces and generic result types.                         |
| Required Tests: Unit tests for Result<T> and Entity equality.                        |
| Parallel Guidance: Sequential.                                                        |
| Risk Level: Low                                                                       |
+---------------------------------------------------------------------------------------+
| TASK-03: Shared Contracts & Value Objects                                             |
+---------------------------------------------------------------------------------------+
| Objective: Implement TenantId, LocationId, DeviceId, UserId, Money value objects.      |
| Prerequisites: TASK-02                                                                |
| Affected Areas: /src/BuildingBlocks/Yashdeep.Contracts                                |
| Deliverable: Immutable value objects with serialization attributes.                   |
| Required Tests: Unit tests for value object equality and immutability.                |
| Parallel Guidance: Sequential.                                                        |
| Risk Level: Low                                                                       |
+---------------------------------------------------------------------------------------+
| TASK-04: PostgreSQL Cloud DbContext & Tenant RLS Interceptor                          |
+---------------------------------------------------------------------------------------+
| Objective: Implement EF Core CloudDbContext with tenant_id RLS interceptor.           |
| Prerequisites: TASK-03                                                                |
| Affected Areas: /src/Infrastructure/Yashdeep.Infrastructure.Cloud                      |
| Deliverable: CloudDbContext with SaveChanges tenant injection & query filters.        |
| Required Tests: Integration tests for RLS SQL execution and tenant isolation.        |
| Parallel Guidance: Sequential.                                                        |
| Risk Level: High                                                                      |
+---------------------------------------------------------------------------------------+
| TASK-05: SQLite Local DbContext & SQLCipher Encryption Configuration                  |
+---------------------------------------------------------------------------------------+
| Objective: Implement LocalDbContext with SQLCipher AES-256 pragma setup.              |
| Prerequisites: TASK-03                                                                |
| Affected Areas: /src/Infrastructure/Yashdeep.Infrastructure.Local                      |
| Deliverable: LocalDbContext configured for local SQLite operating working set.        |
| Required Tests: SQLite file encryption verification tests.                            |
| Parallel Guidance: Sequential.                                                        |
| Risk Level: High                                                                      |
+---------------------------------------------------------------------------------------+
| TASK-06: Ed25519 Entitlement Token Verification Engine                                |
+---------------------------------------------------------------------------------------+
| Objective: Implement Ed25519 signature validator for .lic / JWT entitlement tokens.   |
| Prerequisites: TASK-03                                                                |
| Affected Areas: /src/Infrastructure/Yashdeep.Infrastructure.Security                   |
| Deliverable: EntitlementTokenValidator verifying signed token payloads.                |
| Required Tests: Cryptographic signature verification and tamper detection tests.      |
| Parallel Guidance: Parallel safe (with TASK-05).                                      |
| Risk Level: Medium                                                                    |
+---------------------------------------------------------------------------------------+
| TASK-07: Mandatory 7-Day Connectivity & Grace Period State Machine                    |
+---------------------------------------------------------------------------------------+
| Objective: Implement local connectivity timer, clock tamper detector, and grace lock. |
| Prerequisites: TASK-06                                                                |
| Affected Areas: /src/Application/Yashdeep.Application.Identity                        |
| Deliverable: LicenseEvaluator enforcing 7-day sync check, grace, and read-only mode.  |
| Required Tests: Unit tests simulating offline timelines and system clock rollback.    |
| Parallel Guidance: Sequential.                                                        |
| Risk Level: High                                                                      |
+---------------------------------------------------------------------------------------+
| TASK-08: Local Outbox Message Store & Queue Producer                                  |
+---------------------------------------------------------------------------------------+
| Objective: Implement OutboxMessage entity and atomic transaction outbox producer.     |
| Prerequisites: TASK-05                                                                |
| Affected Areas: /src/Infrastructure/Yashdeep.Infrastructure.Local                      |
| Deliverable: OutboxInterceptor enqueuing domain events in local SQLite transaction.   |
| Required Tests: Unit and integration tests for transactional outbox persistence.     |
| Parallel Guidance: Sequential.                                                        |
| Risk Level: High                                                                      |
+---------------------------------------------------------------------------------------+
| TASK-09: Background HTTP Outbox Sync Worker                                           |
+---------------------------------------------------------------------------------------+
| Objective: Implement edge background service sending batch POST to sync endpoint.      |
| Prerequisites: TASK-08                                                                |
| Affected Areas: /src/Infrastructure/Yashdeep.Infrastructure.Sync                       |
| Deliverable: OutboxSyncWorker polling local outbox and marking synced records.        |
| Required Tests: Mock HTTP integration test verifying batching & retry backoff.        |
| Parallel Guidance: Sequential.                                                        |
| Risk Level: High                                                                      |
+---------------------------------------------------------------------------------------+
| TASK-10: Cloud Gateway Inbox Endpoint & Idempotent Handler                            |
+---------------------------------------------------------------------------------------+
| Objective: Implement POST /api/v1/sync/batch cloud endpoint with inbox dedup.         |
| Prerequisites: TASK-04, TASK-09                                                       |
| Affected Areas: /src/Server/Yashdeep.Server.Api                                       |
| Deliverable: SyncController executing batch events idempotently via Inbox table.     |
| Required Tests: Integration test verifying duplicate payload rejection.               |
| Parallel Guidance: Sequential.                                                        |
| Risk Level: High                                                                      |
+---------------------------------------------------------------------------------------+
| TASK-11: Device Hardware Fingerprinting & Trust Engine                                |
+---------------------------------------------------------------------------------------+
| Objective: Implement cross-platform hardware ID generator (CPU/MB/MAC hash).           |
| Prerequisites: TASK-03                                                                |
| Affected Areas: /src/Infrastructure/Yashdeep.Infrastructure.Security                   |
| Deliverable: DeviceFingerprintService returning unique hashed device token.           |
| Required Tests: Unit tests verifying hash consistency across reboots.                 |
| Parallel Guidance: Parallel safe (Stream Security).                                   |
| Risk Level: Medium                                                                    |
+---------------------------------------------------------------------------------------+
| TASK-12: Identity & RBAC Domain Entities                                              |
+---------------------------------------------------------------------------------------+
| Objective: Implement User, Role, Permission, UserRole entities in Domain.Identity.    |
| Prerequisites: TASK-02                                                                |
| Affected Areas: /src/Domain/Yashdeep.Domain.Identity                                  |
| Deliverable: Domain entities for multi-tenant RBAC.                                   |
| Required Tests: Unit tests for role permission evaluation.                            |
| Parallel Guidance: Parallel safe (Stream Identity).                                   |
| Risk Level: Low                                                                       |
+---------------------------------------------------------------------------------------+
| TASK-13: Restaurant Table & Section Domain Models                                     |
+---------------------------------------------------------------------------------------+
| Objective: Implement Section, DiningTable, TableStatus, RateCategory entities.        |
| Prerequisites: TASK-02                                                                |
| Affected Areas: /src/Domain/Yashdeep.Domain.Restaurant                                |
| Deliverable: Domain entities for dining seating and section-based pricing.           |
| Required Tests: Unit tests verifying table state transitions.                         |
| Parallel Guidance: Parallel safe (Stream Restaurant).                                 |
| Risk Level: Low                                                                       |
+---------------------------------------------------------------------------------------+
| TASK-14: Menu Item & Differential Section Pricing Domain Aggregate                    |
+---------------------------------------------------------------------------------------+
| Objective: Implement MenuItem, Category, Department, SectionRate entities.            |
| Prerequisites: TASK-13                                                                |
| Affected Areas: /src/Domain/Yashdeep.Domain.Restaurant                                |
| Deliverable: Aggregate resolving item price based on active table section.            |
| Required Tests: Unit tests verifying section rate lookup (Family, AC, Garden).        |
| Parallel Guidance: Parallel safe (Stream Restaurant).                                 |
| Risk Level: Medium                                                                    |
+---------------------------------------------------------------------------------------+
| TASK-15: Kitchen Order Ticket (KOT / BOT) Domain Aggregate                            |
+---------------------------------------------------------------------------------------+
| Objective: Implement KOT, KOTItem, Printers, Bilingual ItemName value object.         |
| Prerequisites: TASK-14                                                                |
| Affected Areas: /src/Domain/Yashdeep.Domain.Restaurant                                |
| Deliverable: KOT aggregate handling Marathi Devanagari item names and routing.        |
| Required Tests: Unit tests for KOT item addition, quantity updates, and cancel.      |
| Parallel Guidance: Parallel safe (Stream Restaurant).                                 |
| Risk Level: High                                                                      |
+---------------------------------------------------------------------------------------+
| TASK-16: Billing, Split Taxes & Dynamic UPI QR Engine                                 |
+---------------------------------------------------------------------------------------+
| Objective: Implement OrderBill, TaxLineItem, CGST/SGST/VAT calculators, UPI QR code.   |
| Prerequisites: TASK-15                                                                |
| Affected Areas: /src/Application/Yashdeep.Application.Restaurant                      |
| Deliverable: BillCalculationService creating settled bills with split taxes & UPI QR. |
| Required Tests: Precision math unit tests for tax rounding and split payments.        |
| Parallel Guidance: Parallel safe (Stream Restaurant).                                 |
| Risk Level: High                                                                      |
+---------------------------------------------------------------------------------------+
| TASK-17: Multi-Tier Liquor Stock Domain Entities                                      |
+---------------------------------------------------------------------------------------+
| Objective: Implement GodownStock, CounterPackStock, CounterLooseStock, Brand.         |
| Prerequisites: TASK-02                                                                |
| Affected Areas: /src/Domain/Yashdeep.Domain.Inventory                                 |
| Deliverable: Multi-tier stock domain models for IMFL, Country Liquor, Wine, Beer.     |
| Required Tests: Unit tests for volume conversions (Bulk Litres <-> ML).              |
| Parallel Guidance: Parallel safe (Stream Inventory).                                  |
| Risk Level: High                                                                      |
+---------------------------------------------------------------------------------------+
| TASK-18: Bottle Opening & Loose ML Peg Deduction Engine                               |
+---------------------------------------------------------------------------------------+
| Objective: Implement auto-bottle transfer when loose dispensary reaches 0 ML.         |
| Prerequisites: TASK-17                                                                |
| Affected Areas: /src/Application/Yashdeep.Application.Inventory                      |
| Deliverable: LiquorDispenseService deducting 30/60/90/180ml pegs & opening bottles.   |
| Required Tests: Unit tests verifying automatic sealed bottle decrement on peg sale.   |
| Parallel Guidance: Parallel safe (Stream Inventory).                                  |
| Risk Level: High                                                                      |
+---------------------------------------------------------------------------------------+
| TASK-19: Maharashtra FL-III Permit Holder Register                                    |
+---------------------------------------------------------------------------------------+
| Objective: Implement ExcisePermitHolder entity and permit validation service.          |
| Prerequisites: TASK-18                                                                |
| Affected Areas: /src/Domain/Yashdeep.Domain.Excise                                    |
| Deliverable: Domain logic for tracking FL-III permit numbers and validity dates.      |
| Required Tests: Unit tests verifying permit expiration checks.                        |
| Parallel Guidance: Parallel safe (Stream Excise).                                     |
| Risk Level: Medium                                                                    |
+---------------------------------------------------------------------------------------+
| TASK-20: Maharashtra FL-III Daily Bulk Litre Statement Generator                     |
+---------------------------------------------------------------------------------------+
| Objective: Implement FrmDailyBulkLitre summary logic aggregating daily spirit sales.   |
| Prerequisites: TASK-19                                                                |
| Affected Areas: /src/Application/Yashdeep.Application.Excise                         |
| Deliverable: BulkLitreCalculator generating daily Excise spirit/wine/beer summaries.  |
| Required Tests: Unit tests verifying daily bulk litre aggregation accuracy.           |
| Parallel Guidance: Parallel safe (Stream Excise).                                     |
| Risk Level: High                                                                      |
+---------------------------------------------------------------------------------------+
| TASK-21: Day End Settlement & Business Date Rollover Engine                           |
+---------------------------------------------------------------------------------------+
| Objective: Implement DayEndProcess executing table clearance, date advance, snapshots. |
| Prerequisites: TASK-16, TASK-20                                                       |
| Affected Areas: /src/Application/Yashdeep.Application.Restaurant                      |
| Deliverable: DayEndService performing immutable daily financial & inventory snapshot. |
| Required Tests: Integration tests for Day End rollover and unbilled table guards.     |
| Parallel Guidance: Sequential.                                                        |
| Risk Level: High                                                                      |
+---------------------------------------------------------------------------------------+
| TASK-22: Thermal ESC/POS Byte Generator                                               |
+---------------------------------------------------------------------------------------+
| Objective: Implement ESC/POS command encoder for thermal receipts (58mm/80mm).        |
| Prerequisites: TASK-03                                                                |
| Affected Areas: /src/Infrastructure/Yashdeep.Infrastructure.Printing                 |
| Deliverable: EscPosBuilder generating raw binary byte streams for thermal printers.  |
| Required Tests: Unit tests verifying ESC/POS byte sequence generation.                |
| Parallel Guidance: Parallel safe (Stream Printing).                                   |
| Risk Level: Medium                                                                    |
+---------------------------------------------------------------------------------------+
| TASK-23: Cross-Platform Thermal Printer Drivers (USB / LAN / Bluetooth)              |
+---------------------------------------------------------------------------------------+
| Objective: Implement print port drivers for Windows raw spooling and Android BT/LAN. |
| Prerequisites: TASK-22                                                                |
| Affected Areas: /src/Infrastructure/Yashdeep.Infrastructure.Printing                 |
| Deliverable: ThermalPrinterService dispatching raw ESC/POS bytes to ports.             |
| Required Tests: Mock driver tests verifying retry and paper-out exception handling.   |
| Parallel Guidance: Parallel safe (Stream Printing).                                   |
| Risk Level: High                                                                      |
+---------------------------------------------------------------------------------------+
| TASK-24: QuestPDF Code-First Receipt & Report Templates                               |
+---------------------------------------------------------------------------------------+
| Objective: Implement QuestPDF document generators for Bills, KOTs, and Excise returns. |
| Prerequisites: TASK-16, TASK-20                                                       |
| Affected Areas: /src/Infrastructure/Yashdeep.Infrastructure.Printing                 |
| Deliverable: QuestPdfReportEngine generating PDF byte streams and print previews.     |
| Required Tests: Visual regression / PDF layout generation unit tests.                 |
| Parallel Guidance: Parallel safe (Stream Printing).                                   |
| Risk Level: Medium                                                                    |
+---------------------------------------------------------------------------------------+
| TASK-25: Blazor POS Keyboard-First Touch Layout Components                            |
+---------------------------------------------------------------------------------------+
| Objective: Implement Razor POS components with shortcut key handling (F2, F5, F8).  |
| Prerequisites: TASK-15, TASK-16                                                       |
| Affected Areas: /src/Clients/Yashdeep.Client.Blazor                                  |
| Deliverable: TableGrid.razor, OrderEntry.razor, BillSettlement.razor components.      |
| Required Tests: Component tests verifying keyboard shortcut invocation.               |
| Parallel Guidance: Parallel safe (Stream UI).                                         |
| Risk Level: Medium                                                                    |
+---------------------------------------------------------------------------------------+
| TASK-26: .NET 9 MAUI Windows Host Shell                                               |
+---------------------------------------------------------------------------------------+
| Objective: Construct Windows desktop host embedding Blazor Hybrid POS client.         |
| Prerequisites: TASK-25                                                                |
| Affected Areas: /src/Clients/Yashdeep.Client.Windows                                 |
| Deliverable: Executable Windows WinUI/MAUI application wrapper.                       |
| Required Tests: WinUI startup and local SQLite connection verification.               |
| Parallel Guidance: Parallel safe (Stream UI Hosts).                                   |
| Risk Level: Medium                                                                    |
+---------------------------------------------------------------------------------------+
| TASK-27: .NET 9 MAUI Android POS Host Shell                                           |
+---------------------------------------------------------------------------------------+
| Objective: Construct Android tablet host embedding Blazor Hybrid POS client.          |
| Prerequisites: TASK-25                                                                |
| Affected Areas: /src/Clients/Yashdeep.Client.Android                                 |
| Deliverable: Android APK project targeting Android 12+ (API 31+).                     |
| Required Tests: Android build validation and local SQLite storage test.               |
| Parallel Guidance: Parallel safe (Stream UI Hosts).                                   |
| Risk Level: Medium                                                                    |
+---------------------------------------------------------------------------------------+
| TASK-28: ASP.NET Core REST Gateway API & JWT Tenant Authentication                    |
+---------------------------------------------------------------------------------------+
| Objective: Construct Cloud REST API Gateway with ASP.NET Core Identity & JWT auth.    |
| Prerequisites: TASK-04, TASK-12                                                       |
| Affected Areas: /src/Server/Yashdeep.Server.Api                                       |
| Deliverable: Web API project hosting tenant auth, sync endpoints, and RBAC middleware.|
| Required Tests: API integration tests for token issue, validation, and multi-tenant.  |
| Parallel Guidance: Sequential.                                                        |
| Risk Level: High                                                                      |
+---------------------------------------------------------------------------------------+
| TASK-29: Access MDB Jet 4.0 ETL Migration Pipeline Engine                             |
+---------------------------------------------------------------------------------------+
| Objective: Implement migration tool reading dinurss.mdb and transforming into PostgreSQL.|
| Prerequisites: TASK-04, TASK-16, TASK-18                                              |
| Affected Areas: /src/Tools/Yashdeep.Tools.Migration                                  |
| Deliverable: CLI migration tool supporting dry-run execution & financial audit.      |
| Required Tests: Integration tests against sample dinurss.mdb verifying row parity.   |
| Parallel Guidance: Parallel safe (Stream Tools).                                      |
| Risk Level: High                                                                      |
+---------------------------------------------------------------------------------------+
| TASK-30: Velopack Client Auto-Updater & Authenticode Verification                      |
+---------------------------------------------------------------------------------------+
| Objective: Implement background update downloader verifying signed update packages.    |
| Prerequisites: TASK-26                                                                |
| Affected Areas: /src/Tools/Yashdeep.Tools.Updater                                    |
| Deliverable: AutoUpdateService checking release feeds and applying atomic patches.   |
| Required Tests: Mock update feed test verifying signature validation & rollback.      |
| Parallel Guidance: Parallel safe (Stream Tools).                                      |
| Risk Level: High                                                                      |
+---------------------------------------------------------------------------------------+
```

---

*Formulated strictly in alignment with target modernization architecture.*
