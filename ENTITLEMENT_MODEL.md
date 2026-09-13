# Dynamic Entitlement Model & Offline Verification Architecture

## Architectural Overview

The modernized **Yashdeep Hotel Management System** operates in real-world hospitality environments where internet connectivity can be intermittent or degraded. To guarantee uninterrupted billing, Kitchen Order Ticket (KOT) generation, and bar dispensing operations, the client app uses a **Signed Local Entitlement Cache**.

This model provides:
1. **Zero Hardcoded Business Rules**: Capabilities are evaluated purely dynamically at runtime based on signed entitlement certificates.
2. **Offline Resilience**: Terminals continue operating offline within cryptographically bounded grace windows.
3. **Tamper Resistance**: Cryptographically signed entitlement payloads (Ed25519) prevent client-side modifications.
4. **Strict Clock Verification**: Anti-rollback clock detection protects against system clock manipulation to bypass subscription expiry.

---

## 1. Entitlement Token Specification (Signed JWT / Compact JSON)

Entitlements are issued by the Cloud SaaS Server as an **Ed25519-signed JSON Web Token (JWT)** or signed JSON payload.

### 1.1 Entitlement Payload Schema

```json
{
  "iss": "https://auth.yashdeep-saas.com",
  "sub": "tenant_8f3a9d21-4b10-48e2-9602",
  "aud": "yashdeep_pos_client",
  "iat": 1711958400,
  "exp": 1712563200,
  "jti": "cert_99f2b1a4-3112-429a",
  "tenant": {
    "tenant_id": "8f3a9d21-4b10-48e2-9602",
    "name": "Hotel Yashdeep - Main Branch",
    "plan": "bar_and_restaurant_v1",
    "status": "Active"
  },
  "location": {
    "location_id": "loc_12948a20-001a",
    "licence_no": "FL III-2151444022D8ADF7"
  },
  "device": {
    "device_id": "dev_mac_3a1b2c3d4e5f",
    "device_role": "PrimaryPOS"
  },
  "features": [
    "feature.billing.kot",
    "feature.billing.bot",
    "feature.excise.fl3",
    "feature.inventory.godown_transfer",
    "feature.reports.dayend",
    "feature.payments.upi_qr"
  ],
  "limits": {
    "limit.max_devices": 5,
    "limit.max_locations": 2,
    "limit.max_active_tables": 100,
    "limit.max_monthly_kot": 50000
  },
  "offline_policy": {
    "max_offline_hours": 168,
    "grace_period_hours": 72,
    "allow_emergency_override": false
  }
}
```

---

## 2. Cryptographic Signature & Verification Architecture

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Cloud SaaS Licensing Authority                  │
│                                                                        │
│  Entitlement Payload + Ed25519 Private Key ──► Cryptographic Signature  │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │ HTTPS / WS Sync
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        Edge POS Terminal (SQLite)                      │
│                                                                        │
│  Validate Signature via Embedded Ed25519 Public Key                    │
│  ├── If Valid   ──► Cache in Local SQLite & Grant Access                │
│  └── If Invalid ──► Reject Token & Revert to Stale / Lock State        │
└────────────────────────────────────────────────────────────────────────┘
```

### 2.1 Key Pair Management

1. **Algorithm**: Ed25519 (Edwards-curve Digital Signature Algorithm). Chosen for ultra-fast verification, compact 64-byte signatures, and resilience against side-channel attacks.
2. **Public Key Distribution**:
   - The public key is bundled into client binaries and stored in local secure storage.
   - Key rotation supported via certificate chains: the root public key signs intermediate tenant licensing keys.

### 2.2 Local Verification Flow

Upon receiving a new token (via API sync, WebSocket push, or QR offline import):
1. Compute token header + payload hash.
2. Verify Ed25519 signature against the stored Public Key.
3. Check `exp` (token expiration timestamp) against local verified time.
4. Verify `tenant_id`, `location_id`, and `device_id` match local hardware registration.
5. If valid, overwrite local SQLite cache (`local_entitlement_cache`).

---

## 3. Clock Skew, Anti-Rollback & Anti-Tamper Security

To prevent users from freezing or rewinding the system clock on offline devices to extend an expired subscription, the client enforces **Monotonic Anti-Rollback Time Tracking**.

### 3.1 Time Verification Strategy

1. **Monotonic Hardware Stopwatch**: In addition to system wall clock (`DateTime.UtcNow`), the client tracks monotonic tick count (`Stopwatch.GetTimestamp()`).
2. **High-Water Mark Persistence**:
   - Every transaction (`KOT`, `Bill`, `DayEnd`) records the highest timestamp observed into SQLite (`last_known_timestamp`).
   - If current wall clock < `last_known_timestamp`, clock tampering or system clock reset is detected.

```sql
-- Anti-Tamper Local State
CREATE TABLE local_security_clock (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    last_known_utc DATETIME NOT NULL,
    last_monotonic_ticks BIGINT NOT NULL,
    tamper_detected BOOLEAN NOT NULL DEFAULT 0,
    updated_at DATETIME NOT NULL
);
```

### 3.2 Clock Violation Handling

- If clock rollback is detected (`CurrentTime < LastKnownUtc`):
  1. Set `tamper_detected = 1` in local database.
  2. Suspend non-critical features (e.g. administrative reports).
  3. Permit critical POS offline operations ONLY if within a hardcoded 24-hour emergency buffer.
  4. Display prominent UI warning requiring administrator internet check-in to clear tamper flag.

---

## 4. Offline Entitlement Lifecycle & Expiration

```
 ◄── Cloud Active ──►◄────────────────── Offline Grace Window ──────────────────►
 ───────────────────┬───────────────────────────────┬────────────────────────────►
                    │ Token Expiration (`exp`)       │ Max Offline Expiration
                    │                               │ (`iat` + `max_offline_hours`)
                    ▼                               ▼
 [ Fully Synchronized ] ──► [ Offline Grace Mode ]  ──► [ Hard Block / Emergency ]
  All operations ok        Non-blocking banner       POS transactions blocked;
                           Operations continue        Read-only data access
```

### 4.1 Expiration Thresholds

1. **Soft Token Expiry (`exp`)**: Typical duration 7 days. If internet is lost, app enters **Offline Grace Mode**.
2. **Max Offline Duration (`max_offline_hours`)**: Hard cap on offline operations (e.g., 168 hours / 7 days). If device remains offline beyond this duration, POS transaction creation (`New KOT`, `Settle Bill`) is blocked until an online sync occurs.

---

## 5. Plan Changes While Offline & Online Reconciliation

When a subscription plan change occurs in the cloud while an edge device is offline:

1. **Current Offline Behavior**: The edge device operates on its existing cached signed token until connectivity is restored or the offline grace limit expires.
2. **Online Reconciliation Protocol**:
   - As soon as network connectivity is re-established, the background sync service sends a heartbeat request to `/api/v1/entitlements/current`.
   - The server inspects tenant subscription status and returns an updated signed entitlement token.
   - If upgraded: New features instantly unlock without application restart.
   - If downgraded: Client gracefully disables locked UI elements and enforces lower limits for subsequent actions (existing records remain preserved).

---

## 6. Edge SQLite Entitlement Schema

```sql
-- Cached Signed Entitlement Certificate
CREATE TABLE local_entitlement_cache (
    cache_id INTEGER PRIMARY KEY AUTOINCREMENT,
    jti TEXT NOT NULL UNIQUE,
    tenant_id TEXT NOT NULL,
    location_id TEXT NOT NULL,
    device_id TEXT NOT NULL,
    raw_token TEXT NOT NULL,          -- Full Ed25519 signed JWT payload
    features_json TEXT NOT NULL,     -- Array of feature strings: ["feature.excise.fl3", ...]
    limits_json TEXT NOT NULL,       -- Key-value limits: {"limit.max_devices": 5, ...}
    issued_at DATETIME NOT NULL,
    expires_at DATETIME NOT NULL,
    max_offline_until DATETIME NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT 1
);

-- Local Usage Tracking (for limit enforcement)
CREATE TABLE local_usage_counters (
    metric_key TEXT PRIMARY KEY,     -- e.g., 'monthly_kot_count', 'active_table_count'
    current_value INTEGER NOT NULL DEFAULT 0,
    reset_at DATETIME NOT NULL
);
```
