# Architecture Review Board (ARB) Master Findings & Decision Report

**Project**: Yashdeep Hotel Management & FL-III Bar Modernization SaaS Platform
**Target Architecture**: Offline-First, Cloud-Synchronized Multi-Tenant Platform (.NET 9 Blazor Hybrid / MAUI, Local SQLite SQLCipher, Cloud PostgreSQL 16, QuestPDF, Outbox/Inbox Synchronization Engine)
**Review Board Authority**: Authoritative Cross-Domain Architecture Review Board
**Document Status**: Final Approved Architectural Baseline

---

## Executive Summary

The Architecture Review Board (ARB) has conducted a total cross-check of all 19 architecture specifications, domain models, database schema extractions, security protocols, subscription mechanics, offline strategies, and migration blueprints produced across all sub-domains.

This review verifies that the modernization strategy successfully preserves the complex, high-value domain capabilities of the legacy VB.NET / Access Jet 4.0 monolith (`RSS.exe` / `dinurss.mdb`)—specifically Maharashtra State Excise FL-III statutory compliance, multi-tier Godown/Counter/Loose peg inventory tracking, section-based differential pricing, and billing/KOT workflows—while completely resolving legacy concurrency failures, print spooler deadlocks, and data corruption risks.

All cross-domain contradictions, ambiguous claims, duplicate concepts, missing workflows, and security edge cases identified during the review have been explicitly analyzed and resolved.

---

## 1. Cross-Domain Cross-Check Verifications

| Architecture Cross-Check Axis | Status | Key Verification Findings & Resolutions |
| :--- | :---: | :--- |
| **Domain Model vs. SaaS Multi-Tenancy** | **VERIFIED** | The `TenantId` boundary is consistently enforced as a mandatory root claim across all entities, aggregates, and EF Core Global Query Filters. Multi-branch organizations map cleanly to `LocationId` (Branch) within a single `TenantId` boundary. |
| **SaaS Model vs. Database Schema** | **VERIFIED** | PostgreSQL 16 schema enforces Row-Level Security (RLS) on `tenant_id`. Every table includes indexed `tenant_id` foreign keys. Local SQLite schemas contain `TenantId` to guarantee offline entity scope integrity. |
| **Database Schema vs. Offline Model** | **VERIFIED** | Local SQLite (SQLCipher AES-256) maintains an **Operational Working Set** (rolling 30 days) and explicitly excludes legacy `*_Dayend` duplicated archive tables in favor of unified temporal tables with `BusinessDate` and `DayEndId`. |
| **Offline Model vs. Sync Protocol** | **VERIFIED** | Offline mutations adhere strictly to the **Atomic Local Outbox Pattern** (local write + outbox record in a single local SQLite transaction). Background workers push queued messages sequentially via `POST /api/v1/sync/batch` with idempotency keys. |
| **Sync Protocol vs. Inventory, Billing, KOT & Day End** | **VERIFIED** | Core operational transactions are completely autonomous on the edge. Local sequence numbers, dual-sequence bill numbering, and stock decrements execute offline without remote blocking HTTP calls. Day End executes locally, creating a immutable financial snapshot and advancing `BusinessDate`. |
| **Subscription Model vs. Offline Behavior** | **VERIFIED** | Dynamic entitlements and capacity limits are bundled into cryptographically signed Entitlement Tokens (`.lic` / JWS). Terminals operate up to their plan limit (e.g., 30 days) plus a deterministic 7-day soft grace period before hard read-only lock. |
| **Security Architecture vs. Client Secrets** | **VERIFIED** | Zero cloud database credentials, KMS root keys, or JWT signing private keys exist on or pass through client devices. Cloud API acts as the sole secure proxy for external services (payment gateways, SMS, cloud DB). |
| **Migration Architecture vs. Domain Model** | **VERIFIED** | Legacy `dinurss.mdb` tables (105 user tables) map cleanly to modern C# domain entities. Legacy `BILLFINAL` / `finalbill` and `*_Dayend` tables consolidate into unified `Orders`, `Bills`, and `BillLineItems` with temporal business date tracking. |
| **Testing Architecture vs. Failure Modes** | **VERIFIED** | Comprehensive coverage designed for network partitions, local power loss (SQLite WAL mode recovery), out-of-order outbox sync, system clock tampering, and printer hardware paper-out fallback. |
| **Deployment Architecture vs. Runtime** | **VERIFIED** | Edge POS runs natively on .NET 9 Blazor Hybrid (Windows & Android MAUI), Cloud API runs containerized on Linux Docker (PostgreSQL 16, Redis, ASP.NET Core 9). |

---

## 2. Identified Contradictions & Authoritative Resolutions

During the review, 5 primary cross-document contradictions were identified. The ARB has resolved each using the strongest evidence from legacy operational realities and SaaS target requirements:

### Contradiction 1: Offline Grace Period Duration & Token Lifespan
- **Conflict**: `SYSTEM_ARCHITECTURE.md` mentioned "30 days offline", `OFFLINE_ARCHITECTURE.md` defined a "14-day token + 7-day grace period", `SUBSCRIPTION_ARCHITECTURE.md` defined tier-based limits (7/30/60 days), and `DEVICE_MANAGEMENT.md` referenced 3-30 days.
- **Resolution**: Standardized on **Tier-Based Token Expiration + Deterministic 7-Day Soft Grace Period**.
  - **Starter POS**: 7-Day Token + 7-Day Grace (Hard lock at Day 14).
  - **Pro Bar & Rest**: 30-Day Token + 7-Day Grace (Hard lock at Day 37).
  - **Enterprise**: 60-Day Token + 7-Day Grace (Hard lock at Day 67).
  - All affected documents (`SYSTEM_ARCHITECTURE.md`, `SUBSCRIPTION_ARCHITECTURE.md`, `ENTITLEMENT_MODEL.md`, `OFFLINE_ARCHITECTURE.md`) have been harmonized.

### Contradiction 2: Token Cryptographic Signing Algorithm
- **Conflict**: `SUBSCRIPTION_ARCHITECTURE.md` and `ENTITLEMENT_MODEL.md` specified **RS256 (RSA-4096)**, while `SECURITY_ARCHITECTURE.md` specified **ECDSA P-256** or **Ed25519**.
- **Resolution**: Authoritatively standardizing on **Ed25519 (EdDSA / Curve25519)** as the primary asymmetric signing standard for offline entitlement tokens and device JWTs due to its compact signature size and ultra-fast CPU verification performance on low-power POS tablets, with **RS256 (RSA-4096)** as the permitted fallback for strict enterprise compliance environments.

### Contradiction 3: Sync Transport Protocol Scope
- **Conflict**: `docs/SAAS_ARCHITECTURE.md` made a passing reference to "HTTP / gRPC", while all other documents specified REST over HTTPS and SignalR WebSockets.
- **Resolution**: **gRPC is REJECTED** for edge-to-cloud synchronization. Transport is authoritatively established as **HTTPS REST Web API (JSON payloads over TLS 1.3)** for batch outbox sync and **SignalR WebSockets** for real-time local/cloud event notifications.

### Contradiction 4: Bill Number Sequence Strategy across Offline POS Terminals
- **Conflict**: `DOMAIN_MODEL.md` implied a single global autoincrement `BillNumber`, whereas `BILLING_ARCHITECTURE.md` specified a dual-sequence block allocation strategy, and legacy `LEGACY_SYSTEM_ANALYSIS.md` showed separate `BILLNO` (absolute) vs `BILLDAY` (daily reset) counters.
- **Resolution**: **Dual-Sequence Strategy Approved**.
  1. **Daily Sequence (`BillDailyNumber`)**: Resets to 1 every business date at Day End. Formatted with terminal and section prefix for customer receipts (e.g., `AC-20260330-0042`).
  2. **Global Audit Sequence (`BillGlobalSequence`)**: Offline POS terminals receive pre-allocated sequence blocks (e.g. 10000–10999) from Cloud API during sync to prevent collisions and comply with GST continuous numbering rules.

### Contradiction 5: Legacy Table Duplication (`*_Dayend` Archive Tables)
- **Conflict**: Legacy Access database copied active rows from `BILLFINAL` to `BILLFINAL_Dayend` and truncated active tables during Day End, creating duplicate schemas.
- **Resolution**: **Schema Duplication strictly REJECTED**. Modern PostgreSQL and SQLite schemas eliminate all `*_Dayend` tables. Active and settled transactions persist in unified `Orders` and `Bills` tables, indexed temporally by `BusinessDate` (Value Object) and `DayEndId`.

---

## 3. Approved Architectural Decisions

1. **Offline-First Atomic Outbox Pattern**: Every local POS transaction writes domain entity changes and an `OutboxMessage` entry in a single local SQLite transaction boundary.
2. **SQLCipher 4.x (AES-256) Database Encryption**: Local edge databases encrypted with 256-bit AES keys derived via PBKDF2 (256,000 iterations) and bound to hardware TPM / Secure Enclaves.
3. **QuestPDF Code-First Engine**: Complete replacement of Windows COM-bound Crystal Reports (`.rpt`) with QuestPDF C# fluent layout definitions for thermal receipts and A4 statutory registers.
4. **SkiaSharp Devanagari ESC/POS Rasterization**: Marathi script rendered as 1-bit high-contrast ESC/POS bitmaps (`GS v 0`) to guarantee correct Devanagari printing on hardware lacking native Marathi codepages.
5. **Maharashtra Excise FL-III Bulk Litre Precision**: Strict enforcement of bottle ML conversions, auto-opening of sealed bottles upon loose peg sales, and immutable HMAC-SHA256 hash-chaining on daily excise registers.
6. **Zero-Trust Client Secret Isolation**: Absolute prohibition of cloud database connection strings, KMS master root keys, or JWT signing private keys on client devices.

---

## 4. Rejected Approaches

1. **Direct Client Database Connections**: Edge POS terminals connecting directly to Cloud PostgreSQL via Npgsql/ODBC is **REJECTED** due to critical security risks and loss of offline autonomy.
2. **gRPC Transport for Edge Sync**: Using gRPC for offline edge outbox sync is **REJECTED** in favor of REST HTTPS batch POST endpoints.
3. **Legacy Crystal Reports Runtime (`CRyReport.msi`)**: Retaining Crystal Reports is **REJECTED** due to Windows OS COM lock-in and cross-platform incompatibility.
4. **Hardcoded Entitlement Checks in Client Code**: Binary checks such as `if (plan == "Pro")` are **REJECTED**. All client UI and business guards query dynamic claims via `IEntitlementService`.
5. **Destructive Table Truncation at Day End**: Deleting active records and moving them to `*_Dayend` tables is **REJECTED**.

---

## 5. Open Decisions

1. **Automated Offline Payment Verification (UPI)**: Offline dynamic UPI QR codes display payment requests, but confirmation relies on cashier visual/audio validation or cloud webhook receipt. Offline bank-rail automated settlement verification remains open pending NPCI offline framework standards.
2. **Inter-Terminal Peer-to-Peer Local Network Fallback**: In multi-terminal locations where cloud internet is lost, peer-to-peer SignalR over local Wi-Fi allows waiter tablets to send orders to main cashier POS. Production deployment topology for local DNS/mDNS discovery across mixed OS devices is flagged for final hardware testing.

---

## 6. System Risks & Mitigation Strategies

| Risk Description | Severity | Mitigation Strategy |
| :--- | :---: | :--- |
| **System Clock Tampering**: Cashiers setting OS clock back to bypass offline subscription grace expiration. | **HIGH** | Monotonic hardware tick counter verification ($\Delta_{Time} \text{ vs } \Delta_{Ticks}$) stored in OS Secure Enclaves; locks offline billing if clock rollback is detected. |
| **Database Corruption from sudden Power Failure**: Loss of mains power during active SQLite transaction. | **HIGH** | Enforce `PRAGMA journal_mode = WAL;`, `PRAGMA synchronous = NORMAL;`, automated shadow SQLite daily backups, and boot-time corruption recovery routines. |
| **Sequence Number Collision across Disconnected POS Terminals**: Multiple offline cashier terminals issuing overlapping tax invoice numbers. | **HIGH** | Terminals use pre-allocated Cloud Sequence Blocks for global invoice numbers and terminal-specific prefixes (`POS1-`, `POS2-`) for daily display numbers. |
| **Excise Audit Discrepancies**: Physical liquor stock not matching digital bulk litre records during Excise Officer inspection. | **CRITICAL** | Real-time peg decrement engine, auto-opening bottle triggers, immutable local audit trail, and daily Day End closing locks. |

---

## 7. Required Changes Before Code Implementation

1. **EF Core Model Configuration**: Explicitly apply `builder.Entity<T>().HasQueryFilter(x => x.TenantId == _tenantContext.TenantId)` on all 105 entity definitions.
2. **Migration Ingestion Tool Verification**: Ensure `Yashdeep.Migration` service correctly parses legacy `dinurss.mdb` password `<PRODUCTION_MDB_PASSWORD>` (`rss1008`) and maps legacy OLEDB data types to PostgreSQL 16 standard types.
3. **Hardware Printer Test Suite**: Establish physical testing harnesses for 80mm and 58mm ESC/POS thermal printers over USB, LAN (port 9100), and Bluetooth SPP.

---

## 8. Final Authoritative System Specifications

### 8.1 Authoritative Technology Stack

```
┌───────────────────────────────────────────────────────────────────────────────────────────┐
│                               FINAL AUTHORITATIVE TECH STACK                              │
├───────────────────────────────┬───────────────────────────────────────────────────────────┤
│ Edge Client Runtime           │ .NET 9 Blazor Hybrid (MAUI) — Windows & Android POS       │
│ Edge Local Database           │ SQLite 3 with SQLCipher 4.x (AES-256 Encryption)          │
│ Cloud Backend Runtime         │ ASP.NET Core 9 Web API (Docker Containerized on Linux)     │
│ Cloud Master Database         │ PostgreSQL 16+ (Row-Level Security, Managed TDE)          │
│ Caching & Event Bus           │ Redis 7.2 (Distributed Cache & Revocation List)           │
│ Reporting & Invoicing Engine  │ QuestPDF 2024.x (Code-first fluent PDF generation)        │
│ Graphics & Receipt Rendering  │ SkiaSharp 3.0 (Devanagari Marathi ESC/POS rasterization)  │
│ Background Scheduler          │ Quartz.NET 3.x (Cloud) / HostedServices (Edge)            │
│ Logging & Observability       │ Serilog + OpenTelemetry + Seq / Grafana Loki              │
│ Auth & Cryptography           │ Argon2id / PBKDF2-SHA512 + Ed25519 Token Signing           │
└───────────────────────────────┴───────────────────────────────────────────────────────────┘
```

### 8.2 Final Authoritative System Boundaries

- **Inside Boundary**:
  - Multi-tenant cloud API services, tenant portal, and central analytics.
  - Edge POS application (.NET 9 Blazor Hybrid) for Windows/Android.
  - Encrypted local SQLite persistence and local outbox queue.
  - ESC/POS thermal printer driver and QuestPDF report renderer.
  - Automated legacy `dinurss.mdb` data migration ingestion pipeline.
- **Outside Boundary**:
  - Payment gateway banking networks (Razorpay / Stripe / Bank UPI rails).
  - Physical thermal printer hardware devices.
  - External Maharashtra State Excise official web portal (files exported as compliant PDFs).

### 8.3 Final Authoritative Synchronization Model

- **Pattern**: Event-Driven Asynchronous Outbox Pattern.
- **Transport**: HTTPS REST API (`POST /api/v1/sync/batch`) with JSON payloads, Gzip compression, and Bearer JWT authentication.
- **Consistency**: Eventual consistency with server-authoritative conflict resolution for master catalogs and edge-authoritative immutable sequence log for transactions (KOTs, Bills, Payments).
- **Idempotency**: Message deduplication enforced via `InboxMessages` table storing `SourceDeviceId` and `OutboxMessageId`.

### 8.4 Final Authoritative Subscription Model

- **Commercial Tiers**: *Starter POS*, *Professional Bar & Excise*, *Enterprise Multi-Branch*.
- **Enforcement Mechanism**: Dynamic Entitlement Token (`.lic` JWS format) issued by Cloud Authority and signed using **Ed25519**.
- **Offline Behavior**: Operates up to Subscribed Token Duration (7 to 60 days) plus a deterministic **7-Day Soft Grace Warning Period** before transitioning to Read-Only Hard Lockout. Zero hardcoded entitlement checks in client code.

### 8.5 Final Authoritative Security Model

- **Secret Isolation**: Zero cloud secrets, master KMS keys, or database connection strings on client devices.
- **Authentication**: Dual-mode — OAuth 2.0 / PKCE OIDC for Cloud API; local Argon2id / PBKDF2 salted hash verification for Edge POS offline PIN login.
- **Data Protection**: Local SQLite encrypted with **SQLCipher 4.x (AES-256)** with key stored in OS Secure Enclave (DPAPI / KeyStore / Keychain). Cloud PostgreSQL encrypted at rest via TDE and in transit via TLS 1.3.

---

*Approved by the Architecture Review Board (ARB).*
*This document serves as the final, binding specification for system implementation.*
