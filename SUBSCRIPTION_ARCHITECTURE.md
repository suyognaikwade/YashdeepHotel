# Subscription Architecture Specification

## 1. Overview & Architectural Principles

This document defines the commercial SaaS subscription architecture for the modernized **Yashdeep Hotel Management & FL-III Bar System**.

The target architecture transitions the legacy standalone desktop system into an offline-first, cloud-synchronized multi-tenant SaaS platform built with **.NET 9**, **Blazor Hybrid (MAUI)**, **SQLite (Edge POS)**, and **PostgreSQL 16 (Cloud)**.

### Core Architectural Principles
1. **Zero Hardcoded Client Logic**: Subscription tier names, features, entitlements, and limits MUST NEVER be hardcoded into client binaries. The Blazor Hybrid client evaluates dynamic entitlement payloads issued and cryptographically signed by the server authority.
2. **Server Authority**: The cloud backend (`ASP.NET Core 9 Web API`) serves as the single source of truth for subscription state, billing events, entitlement definitions, device registrations, and revocation lists.
3. **Offline-First Resilience**: Local POS terminals must operate seamlessly during internet outages. Local capabilities are governed by cryptographically signed offline entitlement tokens (`Entitlement Token / License Cert`) cached locally in SQLite.
4. **Deterministic Lifecycle & Failure Modes**: Subscription transitions (Trial → Active → Grace Period → Suspended → Terminated) follow an explicit finite state machine with strict dunning, notification, and fallback rules.

---

## 2. Multi-Tenant Entity Hierarchy

The SaaS subscription structure is organized in a hierarchical multi-tenant model:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Organization (Customer Account)                 │
│                 (e.g., Yashdeep Hospitality Group Pvt Ltd)             │
└────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                      Tenant (Legal Licensing Boundary)                 │
│             (Tied to Tax TIN / State Excise License FL-III)            │
└────────────────────────────────────────────────────────────────────────┘
                                    │
                   ┌────────────────┴────────────────┐
                   ▼                                 ▼
┌─────────────────────────────────────┐  ┌─────────────────────────────────────┐
│    Location 1 (Main Hotel & Bar)    │  ┌    Location 2 (Express Outlet)    │
│  (Tables, Kitchens, Bars, Godown)   │  │  (Tables, Kitchens, Bars, Godown)  │
└─────────────────────────────────────┘  └─────────────────────────────────────┘
                   │
         ┌─────────┴─────────┐
         ▼                   ▼
┌───────────────────┐ ┌───────────────────┐
│  POS Device 1     │ │  POS Device 2     │
│  (Billing Desktop)│ │  (Waiter Tablet)  │
└───────────────────┘ └───────────────────┘
```

### Entity Definitions & Schema Relationships

| Entity Level | Responsibility & Business Scope | Governance & Limits |
| :--- | :--- | :--- |
| **Organization** | Billing owner, primary contact, payment method, invoice aggregator. | Can own 1 or more Tenants under a single commercial billing agreement. |
| **Tenant** | Multi-tenant isolation boundary (`TenantId`). Tied to a specific State Excise License (e.g. `FL III-2151444022D8ADF7`) and VAT/GST TIN. | Bound to 1 active **Subscription Plan**. Aggregates total allowed Locations, Devices, and Users. |
| **Location (Branch)** | Physical operational premises (e.g. Hotel Yashdeep Bhenda). Manages distinct dining sections, kitchens, bar counters, and godown stock. | Plan limits control maximum allowed active Locations per Tenant. |
| **Device** | Physical terminal (Windows POS Desktop, Android Waiter Tablet, Kitchen Display). Registered with unique Hardware Fingerprint. | Plan limits control maximum registered & active POS devices per Location/Tenant. |
| **User & Role** | System users (Cashiers, Waiters, Captains, Bar Managers, Auditors, Admins) assigned RBAC permissions. | Plan limits control maximum active user accounts and role tier access. |

---

## 3. Commercial Subscription Plans

The subscription system defines standard commercial tiers, each targeted at specific hospitality operating profiles:

```
┌────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                   COMMERCIAL TIER CATALOG                                      │
├───────────────────────────────┬────────────────────────────────┬───────────────────────────────┤
│         STARTER POS           │    PROFESSIONAL BAR & REST    │    ENTERPRISE MULTI-CHAIN     │
│  (Dine-in / QSR Express)      │   (Full Restaurant + FL-III)   │     (Multi-Location Group)    │
├───────────────────────────────┼────────────────────────────────┼───────────────────────────────┤
│ • 1 Location                  │ • Up to 3 Locations            │ • Unlimited Locations         │
│ • Up to 2 Devices             │ • Up to 10 Devices             │ • Unlimited Devices           │
│ • Up to 5 User Accounts       │ • Up to 25 User Accounts       │ • Unlimited User Accounts     │
│ • Dine-in & KOT Routing       │ • All Starter Features         │ • All Pro Features            │
│ • Standard Tax & Split Bill   │ • Maharashtra FL-III Compliance│ • Multi-Branch Godown Sync    │
│ • Thermal ESC/POS Printing    │ • Multi-Tier Stock (Godown/Peg)│ • Centralized Master Menu     │
│ • Dynamic UPI QR Integration  │ • QuestPDF Custom Layouts      │ • Custom SLA & Dedicated API  │
│ • SQLite Edge Outbox Sync     │ • Advanced Sales Analytics     │ • Custom Feature Flags        │
│ • 7 Days Token + 7D Grace     │ • 30 Days Token + 7D Grace     │ • 60 Days Token + 7D Grace    │
└───────────────────────────────┴────────────────────────────────┴───────────────────────────────┘
```

### Dynamic Plan Configuration (Cloud DB Metadata)
Plans are stored in the Cloud PostgreSQL database (`subscription_plans` table) and hydrated dynamically:

```json
{
  "planId": "plan_pro_fl3_v1",
  "code": "PROFESSIONAL_BAR_REST",
  "name": "Professional Bar & Restaurant T1",
  "billingPeriod": "Monthly",
  "currency": "INR",
  "price": 2999.00,
  "maxLocations": 3,
  "maxDevicesPerLocation": 5,
  "maxUsers": 25,
  "maxOfflineDays": 30,
  "features": [
    "kot_routing",
    "bilingual_marathi_kot",
    "dynamic_upi_qr",
    "excise_fl3_compliance",
    "multi_tier_inventory",
    "questpdf_reporting",
    "outbox_sqlite_sync"
  ]
}
```

---

## 4. Subscription Lifecycle State Machine

The complete subscription lifecycle is modeled as a deterministic Finite State Machine (FSM):

```
                       ┌─────────────────────────┐
                       │       Creation          │
                       └────────────┬────────────┘
                                    │
                                    ▼
                       ┌─────────────────────────┐
                       │   Trial (14-30 Days)    │◄─────────────────────────────┐
                       └────────────┬────────────┘                              │
                                    │ Payment Success                           │ Reset / Extension
                                    ▼                                           │
                       ┌─────────────────────────┐                              │
                       │         Active          │                              │
                       └──────┬───────────▲──────┘                              │
                              │           │ Renewal / Payment Cleared           │
             Payment Failure /│           │                                     │
             Grace Window     ▼           │                                     │
                       ┌──────────────────┴──────┐                              │
                       │      Grace Period       │                              │
                       │   (7 to 14 Days Soft)   │                              │
                       └────────────┬────────────┘                              │
                                    │ Grace Expired                             │
                                    ▼                                           │
                       ┌─────────────────────────┐                              │
                       │        Suspended        │──────────────────────────────┘
                       │ (Sync Lock/Read-Only)   │
                       └────────────┬────────────┘
                                    │ Uncured (30 Days)
                                    ▼
                       ┌─────────────────────────┐
                       │       Terminated        │
                       │   (Archived/De-provision│
                       └─────────────────────────┘
```

### State Definitions & Transitions

#### 4.1 Trial State
- **Trigger**: Tenant registration / self-service onboarding.
- **Duration**: 14 calendar days (configurable up to 30 days by sales admin).
- **Entitlements**: Access to Professional plan features with trial limits (1 Location, 2 Devices, 3 Users).
- **Offline Allowance**: Offline cache max duration capped at 3 days to mandate frequent cloud validation.
- **Transition Out**:
  - Payment details added & initial charge succeeds → **Active**.
  - Trial expires without payment → **Suspended** (Direct to read-only mode).

#### 4.2 Active State
- **Trigger**: Payment success on trial conversion, invoice payment, or recurring auto-debit.
- **Entitlements**: Full feature set and capacity limits according to the subscribed plan.
- **Token Issuance**: Cloud API issues cryptographically signed Entitlement Token valid for the billing period + maximum offline allowance.
- **Transition Out**:
  - Renewal payment fails → **Grace Period**.
  - Customer cancels mid-cycle → Remains **Active** until cycle end, then transitions to **Cancelled/Suspended**.
  - Immediate plan upgrade/downgrade → Recalculates limits, re-issues signed Entitlement Token.

#### 4.3 Grace Period State
- **Trigger**: Payment charge failure (e.g. card decline, NACH bounce, UPI failure) on renewal date.
- **Duration**: 7 days (Starter) or 14 days (Professional/Enterprise).
- **Behavior & User Experience**:
  - POS operations remain **100% functional** to prevent customer disruption during service hours.
  - Persistent banner displayed on POS UI: *"Subscription payment pending. Grace period active (X days remaining)."*
  - Automated dunning notifications dispatched to Organization admin (SMS/Email/WhatsApp).
- **Transition Out**:
  - Retry payment succeeds → **Active** (Token refreshed).
  - Grace period expires without payment → **Suspended**.

#### 4.4 Payment Failure & Dunning Workflow
- **Dunning Schedule**:
  - Day 0 (Billing Date): Initial attempt fails. Alert sent. Tenant enters **Grace Period**.
  - Day 3: Automated retry attempt #1. Secondary warning email & SMS.
  - Day 5: Automated retry attempt #2. POS admin alert dialog on startup.
  - Day 7/14: Final automated retry attempt #3. If failed, transition to **Suspended**.

#### 4.5 Suspended State
- **Trigger**: Grace period expiry or unpaid trial expiration.
- **POS Edge Enforcement**:
  - **Outbox Sync Engine**: Suspended. Local SQLite mutations accumulate locally but are rejected by Cloud Sync API (HTTP 402 Payment Required).
  - **POS Operational Capability**: Locked to **Read-Only Mode**. Cashiers can view past bills, audit reports, and stock levels, but CANNOT create new KOTs, bills, or day-end settlements.
  - POS UI displays fullscreen modal: *"Subscription Suspended. Please update payment method to unlock POS terminals."*
- **Transition Out**:
  - Invoice settled → **Active** (Immediate sync unlock signal via SignalR + new signed token).
  - Unpaid for > 30 days → **Terminated**.

#### 4.6 Terminated State
- **Trigger**: Sustained non-payment (> 30 days in Suspended state) or explicit account deletion request.
- **Behavior**: Cloud database schema soft-deleted/archived. Tenant cryptographic signing key revoked in Global Key Registry. Devices forcibly de-registered.

---

## 5. Lifecycle Event Transitions (Upgrades, Downgrades & Cancellations)

### 5.1 Plan Upgrades (Immediate)
- **Effect**: Takes effect **immediately**.
- **Billing**: Prorated charge calculated for remaining days in cycle.
- **Enforcement**: Cloud API instantly pushes new `Entitlement Token` via SignalR WebSocket to all active POS terminals. Terminal capacity limits (devices, users, locations) increase instantly without application restart.

### 5.2 Plan Downgrades (End-of-Cycle or Immediate)
- **Effect**: Scheduled to take effect at end of current billing cycle (or forced immediately by admin).
- **Validation Rules**:
  - System checks if tenant usage exceeds target lower plan limits (e.g., target plan allows 2 devices, but tenant has 5 registered).
  - If usage exceeds lower plan, admin MUST select which devices/users/locations to deactivate BEFORE downgrade is saved.
- **Enforcement**: Upon cycle boundary, Cloud API revokes excess device tokens and issues downgraded signed Entitlement Token.

### 5.3 Cancellation Protocol
- **Effect**: Subscription set to cancel at end of current paid billing period.
- **Behavior**: Tenant retains **Active** status until paid period ends. Upon period end, transitions to **Grace Period** (3 days) → **Suspended** (Read-Only). Data retained for 90 days for statutory export (State Excise registers).

---

## 6. Server Authority Model & Client Validation Architecture

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Cloud Server Authority                          │
│                    (ASP.NET Core 9 Cloud API)                          │
├────────────────────────────────────────────────────────────────────────┤
│ • Controls Subscription FSM & Billing Webhooks (Stripe / Razorpay)    │
│ • Manages RSA-4096 / Ed25519 Private Key Pair for Token Signing        │
│ • Pushes Revocation Lists & Entitlement Updates via SignalR            │
│ • Single Source of Truth for Tenant Entitlements & Capacity            │
└────────────────────────────────────────────────────────────────────────┘
                                    │
                         HTTPS Sync / SignalR
                (Bearer JWT + Signed Entitlement Token)
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        Edge POS Terminal Client                        │
│                     (.NET 9 Blazor Hybrid / MAUI)                      │
├────────────────────────────────────────────────────────────────────────┤
│ • Validates Signed Entitlement Token locally using Cached Public Key    │
│ • Caches Token in Local Encrypted SQLite (`SubscriptionCache`)         │
│ • Enforces Feature Flags & Capability Checks in UI & Business Logic    │
│ • Monitors Offline Clock & Expiration Timers                           │
└────────────────────────────────────────────────────────────────────────┘
```

### Key Security & Authority Directives
1. **Clock Skew & Tamper Resistance**: The local POS client cross-verifies monotonic hardware clock counters (system uptime ticks) against token issuance dates to detect local OS system clock tampering (e.g. setting system clock backward to extend license).
2. **Server-Issued Cryptographic Signatures**: Entitlement payloads are signed by the Cloud Authority using **Ed25519** (EdDSA) or **RS256** (RSA-SHA256 fallback). POS clients verify token signatures using public verification keys cached locally in SQLite.
3. **Zero Local License Generators**: Edge terminals possess ONLY public verification keys. License generation or modification on the edge terminal is mathematically impossible.

---

## 7. Verification and File Integrity

This document forms the core subscription architecture specification.

- Target File Path: `SUBSCRIPTION_ARCHITECTURE.md`
- Markdown Format Verified: Yes
- Aligned with Architecture Specs (`docs/ARCHITECTURE.md`) and Rules (`AGENTS.md`).
