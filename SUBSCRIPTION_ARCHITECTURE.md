# Subscription Architecture & Commercial SaaS Strategy

## Executive Summary

This document specifies the commercial SaaS subscription architecture for the modernized **Yashdeep Hotel Management System**. Designed for multi-tenant, cloud-synchronized, offline-first Point-of-Sale (POS) operations, this architecture decouples application software binaries from plan definitions, features, and operational limits.

The system relies on a **Server-Authoritative Dynamic Entitlement Engine** where subscription plans, feature flags, usage caps, and location/device limits are governed by central server services and delivered to edge devices via cryptographically signed entitlement tokens.

---

## 1. Domain Taxonomy & Hierarchy

The commercial SaaS model enforces a multi-tier hierarchy spanning tenants, locations, devices, users, and feature capabilities.

```
┌────────────────────────────────────────────────────────────────────────┐
│                              Tenant (Organization)                     │
│  - Tax Identification (GSTIN/VAT)                                      │
│  - Primary Subscription State & Billing Cycle                         │
└────────────────────────────────────────────────────────────────────────┘
                                    │
         ┌──────────────────────────┴──────────────────────────┐
         ▼                                                     ▼
┌─────────────────────────┐                           ┌─────────────────────────┐
│  Location 1 (Property)  │                           │  Location 2 (Property)  │
│  - FL-III Bar License   │                           │  - Lodging / Hotel      │
│  - Regional Tax Code    │                           │  - Restaurant / Cafe    │
└─────────────────────────┘                           └─────────────────────────┘
         │                                                     │
    ┌────┴──────────────┐                                 ┌────┴──────────────┐
    ▼                   ▼                                 ▼                   ▼
┌─────────┐         ┌─────────┐                       ┌─────────┐         ┌─────────┐
│ Device 1│         │ Device 2│                       │ Device 3│         │ Device 4│
│ (POS)   │         │ (KDS)   │                       │ (Admin) │         │ (Mobile)│
└─────────┘         └─────────┘                       └─────────┘         └─────────┘
```

### 1.1 Organizational Units

1. **Tenant**: The primary billing account representing the business entity (e.g., Hotel Yashdeep Group).
2. **Location**: Physical operational property (e.g., Main Branch - Bhenda, Highway Branch - Nanded). Each location carries unique state license attributes (e.g., Maharashtra Excise FL-III License Number, VAT/GSTIN).
3. **User**: Operators, Captains, Managers, and Accountants assigned to a Tenant with Role-Based Access Control (RBAC).
4. **Device**: Registered hardware terminal (Windows POS PC, Android Tablet, Kitchen Display System terminal, Mobile Waiter Device) bound to a specific Location.

### 1.2 Capability Entities

1. **Plan**: A commercial packaging bundle (e.g., *Essential*, *Bar & Restaurant*, *Enterprise Hotel Suite*) defining baseline entitlement templates and pricing.
2. **Feature**: A discrete binary capability flag (e.g., `feature.excise.fl3`, `feature.inventory.godown_transfer`, `feature.analytics.realtime`).
3. **Limit**: A quantitative cap governing system scale (e.g., `limit.devices_per_location = 5`, `limit.monthly_orders = 10000`, `limit.active_tables = 50`).
4. **Entitlement**: The effective combination of granted features and active quantitative limits evaluated for a specific Tenant, Location, and Device at runtime.

---

## 2. Dynamic Plan & Capability Model (Zero Client Hardcoding)

To support seamless commercial updates, promotional upgrades, custom enterprise contracts, and instant tier toggles without requiring client app updates, **no subscription plans or feature boundaries are hardcoded into client binaries**.

### 2.1 Server-Driven Configuration

- Client binaries (.NET 9 Blazor Hybrid / MAUI app) check capabilities solely through abstract capability keys (e.g., `CanAccessFeature("feature.excise.fl3")` or `GetLimit("limit.active_tables")`).
- The cloud server dynamically generates JSON Entitlement Payloads containing:
  - Allowed Feature Keys array.
  - Limit Keys with numeric thresholds.
  - Expiration timestamps and maximum offline grace durations.
- If a new plan or add-on is created in the SaaS admin panel, the server pushes or signs an updated Entitlement Payload. The client consumes this payload dynamically without code recompilation.

---

## 3. Subscription Lifecycle State Machine

The tenant subscription follows a strictly governed state machine enforced by the Cloud SaaS Server.

```
                            ┌────────────────┐
                            │   1. TRIAL     │
                            └───────┬────────┘
                                    │ Payment Verified / Activation
                                    ▼
┌────────────────┐  Renewal │  2. ACTIVE     │◄───────────────────────┐
│  4. SUSPENDED  ├─────────►└───────┬────────┘                        │
└───────▲────────┘                  │ Payment Fails / Invoice Due     │
        │ Past Grace Expiry         ▼                                 │ Successful
        │                   ┌────────────────┐                        │ Payment
        └───────────────────┤ 3. GRACE PERIOD├────────────────────────┘
                            └───────┬────────┘
                                    │ Non-payment beyond Grace / Canceled
                                    ▼
                            ┌────────────────┐
                            │ 5. CANCELED /  │
                            │   EXPIRED      │
                            └────────────────┘
```

### 3.1 State Definitions & Transition Rules

| State | Trigger / Description | Permitted Capabilities | Offline Behavior |
| :--- | :--- | :--- | :--- |
| **1. Trial** | Initiated upon tenant registration. Defaults to 14 days full feature access. | All core features enabled; limits set to trial caps. | Full offline operation permitted up to trial expiration date. |
| **2. Active** | Payment confirmed via webhook or manual invoice settlement. | Full entitlements per subscribed Plan and Add-ons. | Standard offline operation enabled (up to maximum offline limit, e.g., 7 days). |
| **3. Grace Period** | Renewal payment fails or invoice overdue. Default duration: 7 days. | Unrestricted POS operations; persistent non-blocking billing alerts shown to admins. | Offline operation permitted within remaining grace period. |
| **4. Suspended** | Grace period expires without payment. | Billing portal access ONLY. POS operations blocked (read-only historical data access). | Client denies new transaction creation (`Order`, `Bill`, `KOT`). |
| **5. Canceled / Expired** | User explicit cancellation or administrative termination. | Account frozen. Data archived for retention period (e.g., 90 days). | Terminal locked; data access restricted. |

### 3.2 Subscription Lifecycle Operations

1. **Activation**: Triggered by payment gateway webhook (`charge.succeeded` or `subscription.created`). Generates initial active Entitlement Certificate.
2. **Renewal**: Automated recurring billing. Upon payment verification, the server issues a new signed certificate extending the validity window.
3. **Upgrade**: Tenant increases plan tier or adds device/location capacity. Server generates an immediate revised certificate; client receives it on next sync or WebSocket push.
4. **Downgrade**: Scheduled for the end of the current billing cycle. On cycle rollover, server issues certificate reflecting lowered limits.
5. **Cancellation**: Scheduled at period end or immediate with refund. Gracefully disables recurring billing and moves tenant to Expired upon term end.
6. **Payment Failure Protocol**:
   - T+0: Payment attempt fails -> Tenant transitions to `Grace Period`.
   - T+1 to T+6: Retries attempted at exponential backoff. Admin notified via Email/SMS/In-App banner.
   - T+7: Grace period expires -> Tenant transitions to `Suspended`.

---

## 4. Server Authority & Licensing Architecture

1. **Centralized Authority**: The Cloud SaaS Platform (ASP.NET Core Web API + PostgreSQL) is the sole authority for evaluating plan eligibility, granting add-ons, and computing entitlements.
2. **Cryptographic Certificate Issuance**:
   - The cloud server signs Entitlement Payloads using an **Ed25519** asymmetric private key.
   - Edge client devices store the corresponding public key and validate signatures locally without needing live internet access for every check.
3. **Idempotent Webhook Processing**:
   - Inbound payment notifications from gateways (Stripe / Razorpay / Cashfree) are validated for signatures and processed idempotently via an event processing outbox.

---

## 5. Multi-Tenant Data Schema (PostgreSQL SaaS Core)

Below is the database DDL for the subscription core in the Cloud SaaS server:

```sql
-- Subscription Plans Definition
CREATE TABLE saas_plans (
    plan_id VARCHAR(64) PRIMARY KEY,
    name VARCHAR(128) NOT NULL,
    description TEXT,
    billing_interval VARCHAR(32) NOT NULL, -- 'monthly', 'annual'
    price_inr DECIMAL(12, 2) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Core Capabilities Catalog (Features and Limits)
CREATE TABLE saas_capabilities (
    capability_key VARCHAR(128) PRIMARY KEY, -- e.g., 'feature.excise.fl3', 'limit.devices'
    type VARCHAR(32) NOT NULL, -- 'feature' (boolean) or 'limit' (numeric)
    default_value VARCHAR(256) NOT NULL,
    description TEXT
);

-- Plan Capability Mapping
CREATE TABLE saas_plan_capabilities (
    plan_id VARCHAR(64) REFERENCES saas_plans(plan_id),
    capability_key VARCHAR(128) REFERENCES saas_capabilities(capability_key),
    granted_value VARCHAR(256) NOT NULL,
    PRIMARY KEY (plan_id, capability_key)
);

-- Tenant Subscriptions
CREATE TABLE saas_subscriptions (
    subscription_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    plan_id VARCHAR(64) NOT NULL REFERENCES saas_plans(plan_id),
    status VARCHAR(32) NOT NULL, -- 'Trial', 'Active', 'GracePeriod', 'Suspended', 'Canceled'
    current_period_start TIMESTAMPTZ NOT NULL,
    current_period_end TIMESTAMPTZ NOT NULL,
    grace_period_end TIMESTAMPTZ,
    canceled_at TIMESTAMPTZ,
    external_subscription_id VARCHAR(128), -- Stripe/Razorpay Subscription ID
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Tenant Custom Capability Overrides (Add-ons / Custom Enterprise Limits)
CREATE TABLE saas_tenant_entitlement_overrides (
    tenant_id UUID NOT NULL,
    capability_key VARCHAR(128) NOT NULL REFERENCES saas_capabilities(capability_key),
    override_value VARCHAR(256) NOT NULL,
    expires_at TIMESTAMPTZ,
    PRIMARY KEY (tenant_id, capability_key)
);
```
