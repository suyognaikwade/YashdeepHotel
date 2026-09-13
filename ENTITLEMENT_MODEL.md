# Entitlement Model & Offline Token Specification

## 1. Executive Summary & Entitlement Architecture

This document specifies the dynamic entitlement evaluation engine, granular feature matrix, limit enforcement rules, and cryptographically signed offline entitlement tokens for the **Yashdeep SaaS Platform**.

### Core Entitlement Principles
1. **Dynamic Evaluation**: The Blazor Hybrid client client binaries MUST NOT hardcode plan checks (e.g., `if (plan == "Pro")`). Instead, UI components and business handlers query a dynamic capability service: `IEntitlementService.HasFeature("excise_fl3_compliance")` or `IEntitlementService.GetLimit("max_devices")`.
2. **Offline Resilience via Cryptographic Claims**: Entitlements and capacity limits are bundled into a cryptographically signed **Entitlement Token** (JSON Web Token / JWS format using **Ed25519** or **RS256** fallback).
3. **Local SQLite Cache**: Terminals cache the signed Entitlement Token locally in SQLite. The terminal validates the token's cryptographic signature offline using the Cloud Authority's Public Key.
4. **Deterministic Grace Expiry**: Tokens specify both `ExpiresAt` (standard cloud check-in interval) and `OfflineGraceExpiresAt` (absolute hard lock date if offline).

---

## 2. Dynamic Entitlement Engine & Payload Schema

### 2.1 Signed Entitlement Token Payload (JWS Claim Structure)

When a POS terminal synchronizes with the Cloud API, it receives a signed Entitlement Token (`.lic` / JWT format):

```json
{
  "iss": "https://auth.yashdeep-saas.com",
  "sub": "tenant_fl3_21514440",
  "aud": "yashdeep_pos_client",
  "jti": "ent_tok_994821038472",
  "iat": 1741500000,
  "nbf": 1741500000,
  "exp": 1744092000,
  "offline_grace_exp": 1746684000,
  "tenant_id": "ten_yashdeep_bhenda_01",
  "organization_id": "org_yashdeep_group",
  "plan_id": "plan_pro_fl3_v1",
  "plan_code": "PROFESSIONAL_BAR_REST",
  "subscription_status": "Active",
  "hardware_fingerprint": "HW-WIN-984F-221A-BB89",
  "device_id": "dev_pos_main_cashier_01",
  "limits": {
    "max_locations": 3,
    "max_devices_per_location": 5,
    "max_users": 25,
    "max_sections": 10,
    "max_tables_per_section": 50,
    "max_offline_days": 30
  },
  "features": {
    "pos_billing": true,
    "kot_routing": true,
    "bilingual_marathi_kot": true,
    "dynamic_upi_qr": true,
    "excise_fl3_compliance": true,
    "multi_tier_inventory": true,
    "loose_peg_dispensing": true,
    "questpdf_reporting": true,
    "outbox_sqlite_sync": true,
    "advanced_analytics": true,
    "multi_branch_stock_transfer": false,
    "custom_api_webhooks": false
  },
  "grace_period_info": {
    "is_in_grace_period": false,
    "grace_period_ends_at": null
  }
}
```

---

## 3. Feature Matrix & Granular Entitlements

The table below outlines feature flag keys and their availability across commercial subscription tiers:

| Feature Claim Key (`features.*`) | Description & System Target | Starter POS | Pro Bar & Rest | Enterprise |
| :--- | :--- | :---: | :---: | :---: |
| `pos_billing` | Order entry, table grid, split billing, payment settlement. | ✅ | ✅ | ✅ |
| `kot_routing` | Kitchen Order Ticket routing to thermal kitchen printers. | ✅ | ✅ | ✅ |
| `bilingual_marathi_kot` | Dynamic translation of menu items to Marathi script (`item.Marathi`). | ✅ | ✅ | ✅ |
| `dynamic_upi_qr` | Real-time generation of UPI payment QR codes on receipts. | ✅ | ✅ | ✅ |
| `outbox_sqlite_sync` | Background outbox pattern synchronization to Cloud API. | ✅ | ✅ | ✅ |
| `excise_fl3_compliance` | Maharashtra State Excise FL-III daily bulk litre & monthly registers. | ❌ | ✅ | ✅ |
| `multi_tier_inventory` | Godown → Counter → Loose peg inventory tracking. | ❌ | ✅ | ✅ |
| `loose_peg_dispensing` | Automatic bottle opening triggers upon peg sale depletion (30/60/90/180ml).| ❌ | ✅ | ✅ |
| `questpdf_reporting` | Custom code-first thermal and A4 GST tax invoice reports. | Standard | Advanced | Custom |
| `advanced_analytics` | Historical trends, section profitability, waiter sales audit. | Basic | ✅ | ✅ |
| `multi_branch_stock_transfer` | Inter-branch Godown transfers & stock consolidation. | ❌ | ❌ | ✅ |
| `centralized_master_menu` | Multi-branch dynamic menu push and rate management. | ❌ | ❌ | ✅ |
| `custom_api_webhooks` | Third-party ERP / Accounting integration webhooks. | ❌ | ❌ | ✅ |

---

## 4. Capacity Limit Enforcement Protocol

Limits are evaluated locally on the edge terminal using cached token claims:

```
                                [ POS Operation Request ]
                                 (e.g., Register New Device)
                                            │
                                            ▼
                           [ Check Local SQLite Entitlement Token ]
                                            │
                    ┌───────────────────────┴───────────────────────┐
                    ▼                                               ▼
         [ Token Expired / Invalid ]                      [ Token Signature Valid ]
                    │                                               │
                    ▼                                               ▼
      [ Deny Action / Prompt Sync ]               [ Read Claim: max_devices_per_location ]
                                                                    │
                                            ┌───────────────────────┴───────────────────────┐
                                            ▼                                               ▼
                                 [ Current Count >= Max ]                        [ Current Count < Max ]
                                            │                                               │
                                            ▼                                               ▼
                                 [ Block Device Registration ]                  [ Grant Device Registration ]
                                 [ Display Upgrade Prompt ]
```

### Limit Keys & Enforcement Logic

| Limit Claim Key | Enforcement Point | Behavior When Exceeded |
| :--- | :--- | :--- |
| `max_locations` | Cloud API (Location creation endpoint). | Prevents adding a new physical branch in Tenant Portal. Prompts plan upgrade. |
| `max_devices_per_location` | Device Registration Protocol. | Prevents binding new POS terminal hardware fingerprint. |
| `max_users` | User Management Handler. | Blocks creation of new employee login credentials. |
| `max_sections` | Dining Section Setup Handler. | Restricts adding extra dining sections (e.g. Garden, AC Hall, VIP). |
| `max_offline_days` | Edge Clock Monitoring Worker. | Triggers Read-Only Lockout if offline duration exceeds allowed threshold. |

---

## 5. Offline Entitlement Cache & Cryptographic Security

### 5.1 Local Storage Security (SQLite Encrypted Cache)
The signed Entitlement Token is cached in local encrypted SQLite database (`SubscriptionCache` table):

```sql
CREATE TABLE subscription_cache (
    tenant_id TEXT PRIMARY KEY,
    device_id TEXT NOT NULL,
    signed_token TEXT NOT NULL,          -- Full JWS Compact String (header.payload.signature)
    cached_at INTEGER NOT NULL,          -- Unix timestamp
    expires_at INTEGER NOT NULL,         -- Token expiration timestamp
    offline_grace_exp INTEGER NOT NULL,  -- Absolute offline lock timestamp
    hardware_fingerprint TEXT NOT NULL,  -- Bound hardware ID
    public_key_pem TEXT NOT NULL         -- Cloud Ed25519 / RSA Public Key for validation
);
```

### 5.2 Cryptographic Signature Verification Flow
Every time the POS application initializes or performs a restricted capability check, `IEntitlementService` executes local signature verification:

1. **Read Cached Token**: Load `signed_token` string from SQLite `subscription_cache`.
2. **Retrieve Public Key**: Retrieve `public_key_pem` stored in secure application vault / SQLite.
3. **Verify Signature**: Perform Ed25519 or RS256 signature verification on the JWS payload. If signature is invalid or tampered with, **immediately halt write operations** and switch to Read-Only mode.
4. **Hardware Fingerprint Check**: Compare `hardware_fingerprint` in payload against local machine BIOS GUID / MAC hash. If mismatch, token is rejected (prevents copying SQLite database file to unauthorized hardware).
5. **Timestamp Validation**:
   - Verify `iat <= current_time` (Issued at or before now).
   - Verify `current_time <= offline_grace_exp` (Current time within offline grace window).

---

## 6. Offline Expiration & Tamper-Prevention Protocol

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Offline Expiration Timeline                     │
├────────────────────────────────────────────────────────────────────────┤
│ Day 0: Cloud Sync Successful. Token Cache updated (exp = Day 30).      │
│ Day 1-25: Normal Offline POS Operation. All features active.           │
│ Day 26: Soft Warning Warning Banner: "Sync required within 5 days."   │
│ Day 30 (exp): Offline Grace Period Begins (Soft Lock, Day 30-44).      │
│ Day 44 (offline_grace_exp): HARD LOCK OUT. Pos moves to Read-Only.    │
└────────────────────────────────────────────────────────────────────────┘
```

### 6.1 System Clock Tamper Detection Algorithm
To prevent users from setting local OS system clock backward to bypass expiration:

1. **Monotonic Tick Counter**: The POS client stores `LastKnownUnixTimestamp` and `LastMonotonicTicks` in encrypted local storage.
2. **Tick Delta Verification**: On startup and periodically:
   $$\Delta_{Time} = \text{CurrentSystemTime} - \text{LastKnownUnixTimestamp}$$
   $$\Delta_{Ticks} = \text{CurrentMonotonicTicks} - \text{LastMonotonicTicks}$$
3. **Detection Rule**: If $\Delta_{Time} < 0$ (clock moved backward) or $\Delta_{Time} \gg \Delta_{Ticks}$ (inconsistent jump), system detects **Clock Tampering**.
4. **Clock Tamper Enforcement**:
   - POS flags `ClockTamperDetected = true`.
   - Disables offline billing immediately.
   - Mandates internet reconnection to fetch cloud NTP time and fresh signed token.

---

## 7. Emergency Offline Extension Protocol

In extreme connectivity crises (e.g. natural disaster or regional fiber cut lasting > 30 days):

1. Tenant contacts Customer Support via phone/SMS.
2. Support Admin generates an **Offline Extension License Key** (`.lic` file containing signed payload with extended `offline_grace_exp` and specific `device_id`).
3. Operator loads key into POS via USB Flash Drive.
4. POS verifies signature against built-in Cloud Public Key and unlocks for extended period.

---

## 8. Verification and File Integrity

This document forms the core entitlement model specification.

- Target File Path: `ENTITLEMENT_MODEL.md`
- Markdown Format Verified: Yes
- Aligned with Architecture Specs (`docs/ARCHITECTURE.md`) and Rules (`AGENTS.md`).
