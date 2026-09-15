# Technical and Domain Dependency and Implementation Sequence

**Repository**: `https://github.com/suyognaikwade/YashdeepHotel`
**Target Branch**: `botify`
**Purpose**: Define the strict execution order, prerequisite graph, parallelization boundaries, and non-concurrent constraints for modernizing the Yashdeep SaaS platform.

---

## 1. High-Level Dependency Graph

The implementation sequence is strictly governed by fundamental architectural dependencies. Foundational domain abstractions, multi-tenant database contexts, identity contracts, and security boundaries must be established before UI components or complex business workflows are built.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Phase 0: Policy Alignment & Repository Monorepo Scaffold                              │
│ - Finalize PO decisions (7-day connectivity, grace period, restricted mode)            │
│ - Establish /src and /tests monorepo directory tree and .csproj definitions            │
└───────────────────────────────────────────┬────────────────────────────────────────────┘
                                            │
                                            ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Phase 1: Core Domain Abstractions & Contracts (Yashdeep.Core & Yashdeep.Contracts)     │
│ - Base Entity, AggregateRoot, ValueObjects, DomainEvents, Result<T>                    │
│ - TenantId, LocationId, DeviceId, UserId value objects                                 │
└───────────────────────────────────────────┬────────────────────────────────────────────┘
                                            │
                                            ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Phase 2: Database Foundations & Tenant Isolation                                       │
│ - Yashdeep.Infrastructure.Cloud (PostgreSQL 16 EF Core + RLS Interceptor)              │
│ - Yashdeep.Infrastructure.Local (SQLite SQLCipher EF Core + Local Outbox Context)       │
└───────────────────────────────────────────┬────────────────────────────────────────────┘
                                            │
                                            ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Phase 3: Identity, Device Registration & Entitlements                                  │
│ - Ed25519 Token Signing & Verification Engine                                          │
│ - Device Fingerprint Generator & Hardware Trust Handshake                              │
│ - Mandatory 7-Day Connectivity & License Status Evaluator                              │
└───────────────────────────────────────────┬────────────────────────────────────────────┘
                                            │
                                            ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Phase 4: Offline Outbox/Inbox Synchronization Engine                                   │
│ - Edge Outbox Queue Worker (Local SQLite -> HTTP Batch POST)                           │
│ - Cloud Inbox Idempotent Processor & Conflict Resolution Engine                        │
└───────────────────────────────────────────┬────────────────────────────────────────────┘
                                            │
               ┌────────────────────────────┼────────────────────────────┐
               │                            │                            │
               ▼                            ▼                            ▼
┌────────────────────────────┐┌────────────────────────────┐┌────────────────────────────┐
│ Phase 5A: Restaurant & POS ││ Phase 5B: Liquor & Excise  ││ Phase 5C: Hotel & Rooms    │
│ - Sections, Tables, Rates  ││ - Multi-tier Stock Tiers   ││ - Room Types & Grid        │
│ - Bilingual KOT / BOT      ││ - Loose ML & Peg Deduction ││ - Reservations & Check-in  │
│ - Billing, CGST/SGST, UPI  ││ - FL-III Daily/Monthly Reg ││ - Guest Ledger & Check-out │
└──────────────┬─────────────┘└─────────────┬──────────────┘└─────────────┬──────────────┘
               │                            │                            │
               └────────────────────────────┼────────────────────────────┘
                                            │
                                            ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Phase 6: Hardware Thermal Printing & Reporting                                         │
│ - ESC/POS Byte Stream Generator (USB, LAN, Bluetooth)                                  │
│ - QuestPDF Template Engine (Bills, KOT, Excise Returns, Financial Ledgers)             │
└───────────────────────────────────────────┬────────────────────────────────────────────┘
                                            │
                                            ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Phase 7: Client Applications & UI Hosts                                                │
│ - Yashdeep.Client.Blazor (Shared POS, KOT, Billing Razor Components)                  │
│ - Yashdeep.Client.Windows (MAUI / WinUI Host) & Yashdeep.Client.Android (MAUI Host)    │
│ - Yashdeep.Server.AdminWeb (Central Tenant Portal)                                     │
└───────────────────────────────────────────┬────────────────────────────────────────────┘
                                            │
                                            ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Phase 8: Migration Tooling, Auto-Updater & Production Release                          │
│ - Access MDB -> PostgreSQL/SQLite ETL Migration Engine & Dry-Run Verifier             │
│ - Velopack Auto-Updater & Security Hardening                                           │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Execution Phases & Work Stream Categorization

### Sequential Work (Strict Prerequisites)
The following tasks **must be executed in strict sequence**. No downstream task in this list can begin until its prerequisite is merged and verified:
1. **Monorepo Directory & Project Structure**: `/src/BuildingBlocks`, `/src/Domain`, `/src/Infrastructure`, `/src/Clients`, `/src/Server`.
2. **Core Domain Base Abstractions**: `BaseEntity`, `AggregateRoot`, `IDomainEvent`, `TenantId`, `LocationId`.
3. **EF Core Database Contexts**: `CloudDbContext` (PostgreSQL) and `LocalDbContext` (SQLite).
4. **Tenant Isolation Middleware & Interceptors**: PostgreSQL RLS & EF Core Global Query Filters.
5. **Ed25519 Cryptographic Entitlement Engine**: Token verification and 7-day connectivity timer.
6. **Outbox / Inbox Synchronization Engine**: Local SQLite outbox + Cloud Gateway idempotent inbox.

### Parallel Work (Safe Independent Streams)
Once Phase 4 (Synchronization Engine) is merged, the following domain modules can be implemented in parallel by separate engineering streams without file conflicts:
- **Stream A (Restaurant & POS Domain)**: `Yashdeep.Domain.Restaurant`, `Yashdeep.Application.Restaurant`.
- **Stream B (Liquor & Excise Domain)**: `Yashdeep.Domain.Excise`, `Yashdeep.Domain.Inventory`.
- **Stream C (Hotel & Hospitality Domain)**: `Yashdeep.Domain.Hotel`, `Yashdeep.Application.Hotel`.
- **Stream D (Hardware Printing Infrastructure)**: `Yashdeep.Infrastructure.Printing` (ESC/POS drivers & QuestPDF engine).

### Blocked Work (Awaiting Product Owner Approval)
The following features cannot be finalized until explicit product-owner decisions are recorded in `PRODUCT_OWNER_DECISION_REGISTER.md`:
1. **Exact Connectivity Deadline Enforcement**: Confirmation of 7-day hard limit vs. grace period behavior.
2. **Offline Payment Verification Rules**: Policy regarding offline UPI or credit settlement limits.
3. **Cross-Branch Inventory Transfer Rules**: Approval workflow for inter-branch transfers.

### Non-Concurrent Constraints (Do NOT Assign Simultaneously)
- **Database Contexts & Entity Models**: Never assign EF Core entity mapping and DB Context configuration for the same domain to multiple agents concurrently.
- **Outbox Worker & Sync API Endpoint**: Keep outbox queue producer/consumer and sync payload contract locked to a single owner during Phase 4.

---

## 3. Dependency Matrix Summary

| Work Stream | Prerequisites | Dependents | Parallel Safe? |
| :--- | :--- | :--- | :---: |
| **Monorepo Scaffold** | None | Core Domain | No |
| **Core Domain Contracts** | Monorepo Scaffold | DB Contexts, Identity | No |
| **DB Contexts & RLS** | Core Domain | Outbox Sync, All Domains | No |
| **Identity & Entitlements** | Core Domain | Device Trust, Client App | Yes (with DB Context) |
| **Outbox Sync Engine** | DB Contexts, Identity | All Application Domains | No |
| **Restaurant POS Module** | Outbox Sync Engine | Blazor UI | Yes (Stream A) |
| **Liquor & Excise Module** | Outbox Sync Engine | Blazor UI, QuestPDF | Yes (Stream B) |
| **Hotel Management Module** | Outbox Sync Engine | Blazor UI | Yes (Stream C) |
| **Hardware Printing Engine** | Core Domain Contracts | Blazor UI | Yes (Stream D) |
| **Blazor Hybrid POS UI** | POS, Excise, Hotel Modules | Windows/Android Hosts | No |
| **Access Migration Engine** | DB Contexts, All Domains | Production Cutover | Yes |

---

*Baseline established for Jules Task 1 execution roadmap.*
