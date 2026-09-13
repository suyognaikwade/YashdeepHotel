# Device Management, Hardware Activation & Revocation Architecture

## Executive Summary

In a modern Point-of-Sale (POS) SaaS architecture, devices (POS Touch Terminals, Android Waiter Tablets, Kitchen Display Systems, Bar Dispensing Monitors) represent physical operational endpoints. Controlling device access is essential to enforce commercial tier limits (`limit.max_devices`), secure sensitive billing/excise data, and prevent unauthorized hardware proliferation.

This document details hardware fingerprinting, enrollment, location binding, heartbeat monitoring, revocation lists, and offline authorization rules for the modernized **Yashdeep Hotel Management System**.

---

## 1. Hardware Fingerprinting & Identity Strategy

To uniquely identify a physical device and prevent cloning or virtual machine duplication, the client application generates a **Composite Cryptographic Hardware Fingerprint**.

### 1.1 Multi-Factor Hardware Attributes

| Component | Platform API | Extracted Attribute |
| :--- | :--- | :--- |
| **Motherboard** | WMI (`Win32_BaseBoard`) / Android System Prop | Motherboard Serial Number (`SerialNumber`) |
| **CPU** | WMI (`Win32_Processor`) / `/proc/cpuinfo` | CPU Processor ID (`ProcessorId`) |
| **Primary Storage** | WMI (`Win32_DiskDrive`) / Storage Manager | Disk Volume Serial Number (`VolumeSerialNumber`) |
| **Network Interface** | System Network Interfaces | Primary Active Physical MAC Address |

### 1.2 Fingerprint Hash Generation

The raw hardware strings are concatenated and hashed using SHA-256 to create a deterministic 64-character hex string (`DeviceFingerprintHash`).

$$\text{DeviceFingerprintHash} = \text{SHA256}(\text{MotherboardSerial} + "|" + \text{CpuId} + "|" + \text{DiskSerial} + "|" + \text{MacAddress})$$

- If a single non-critical hardware component changes (e.g. Wi-Fi adapter replaced), the client uses a **Fuzzy Matching Threshold** (allowing 3 of 4 factors to match) to prevent accidental device lockouts while flagging hardware changes to the server during the next sync.

---

## 2. Device Registration & Onboarding Lifecycle

```
[ New Unregistered Device ]
           │
           ▼
1. Launch App ──► Display 6-Digit Activation Code or QR Code
           │
           ▼
2. Admin Portal ──► Operator enters code in SaaS Admin Console
           │       Selects Tenant, Location, Device Name & Role (e.g. Bar POS)
           ▼
3. Cloud Server ──► Validates `limit.max_devices` for Location
           │       Generates Device Token & Ed25519 Signed Registration Payload
           ▼
4. Device Client ──► Receives Payload via WS/HTTP -> Saves to Secure Storage
           │       Device is ACTIVE
```

### 2.1 Device Roles

- `PrimaryPOS`: Full read/write billing, KOT/BOT creation, Day-End audit settlement, thermal printer control.
- `SecondaryPOS`: Order entry and bill printing; requires `PrimaryPOS` approval for discounts/cancellations.
- `WaiterTablet`: Mobile KOT/BOT ordering table-side; no cash drawer or Day-End access.
- `KDS` (Kitchen Display System): Kitchen order display and status updating only.
- `AdminDashboard`: Reporting, stock adjustments, and administrative view.

---

## 3. Location Allocation & Capacity Limits

1. **Per-Location Binding**: Every registered device is permanently bound to a single `LocationId` (e.g., Hotel Yashdeep - Main Bar).
2. **Limit Enforcement**: When registering a new device, the cloud server checks `saas_tenant_entitlement_overrides` and `saas_plan_capabilities` for `limit.max_devices`.
   - If `ActiveDevices >= MaxDevices`, registration is rejected with error code `ERR_DEVICE_LIMIT_EXCEEDED`.
   - Admin must either deactivate an existing registered device or upgrade the subscription plan.

---

## 4. Heartbeat & Device Revocation List (DRL) Synchronization

Devices communicate with the Cloud Server to maintain active session validity and receive instant configuration/revocation updates.

### 4.1 Periodic Heartbeat Protocol

- **Frequency**: Every 60 seconds when online (exponential backoff up to 15 minutes on poor connections).
- **Payload**: Includes `DeviceId`, `DeviceFingerprintHash`, `CurrentAppVersion`, `LocalClockUtc`, `PendingOutboxCount`.
- **Response**: Returns `ServerStatus` (`OK`, `REVOKED`, `CONFIG_UPDATE_REQUIRED`) and updated **Device Revocation List (DRL)** hash.

### 4.2 Device Revocation Vector / Revocation List

When an administrator revokes a device from the SaaS Web Console (e.g., lost/stolen tablet, decommissioned terminal):

```json
{
  "drl_version": 1042,
  "revoked_device_ids": [
    "dev_mac_3a1b2c3d4e5f",
    "dev_android_99a8b7c6d5"
  ],
  "reason": "Administrative revocation",
  "issued_at": "2026-03-31T10:00:00Z"
}
```

---

## 5. Device Revocation & Remote Lockout Execution

Upon receiving a revocation signal (via active WebSocket push, Heartbeat response, or updated DRL sync):

```
                        ┌──────────────────────────────┐
                        │ Device Revocation Triggered  │
                        └──────────────┬───────────────┘
                                       │
                                       ▼
                     ┌───────────────────────────────────┐
                     │ 1. Terminate Active Operator      │
                     │    Sessions Immediately           │
                     └─────────────────┬─────────────────┘
                                       │
                                       ▼
                     ┌───────────────────────────────────┐
                     │ 2. Purge Local Session Keys &     │
                     │    Cached Entitlement Tokens      │
                     └─────────────────┬─────────────────┘
                                       │
                                       ▼
                     ┌───────────────────────────────────┐
                     │ 3. Lock Client UI to              │
                     │    "Device Revoked" Terminal Screen│
                     └─────────────────┬─────────────────┘
                                       │
                                       ▼
                     ┌───────────────────────────────────┐
                     │ 4. Preserve Unsynced SQLite       │
                     │    Transactions for Forensic Export│
                     └───────────────────────────────────┘
```

1. **Active Session Termination**: All logged-in users are immediately logged out.
2. **Local Token Invalidation**: `local_entitlement_cache` and auth tokens are deleted or marked as revoked.
3. **UI Terminal Lock**: Application screen transitions to a permanent locked state displaying: `"This terminal has been revoked by your system administrator. Contact support for re-activation."`
4. **Data Protection**: Local unsynced sales records in SQLite are kept encrypted to prevent data loss, but creation of new records is strictly prohibited.

---

## 6. Offline Device Operation Rules

When a device operates offline:
1. **DRL Enforcement**: The device evaluates its status against the last successfully received Device Revocation List stored in local SQLite.
2. **Offline Hardware Check**: On startup, the app re-computes `DeviceFingerprintHash`. If hardware mismatch is detected offline (e.g., database moved to unauthorized PC), boot is aborted.
3. **Offline Time Expiry**: If the offline duration exceeds `max_offline_hours`, terminal functionality locks until connected to the internet for a heartbeat check.

---

## 7. SaaS Cloud & Edge Local Database Schema

### 7.1 Cloud Database DDL (PostgreSQL)

```sql
-- Device Registry in SaaS Cloud
CREATE TABLE saas_devices (
    device_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    location_id UUID NOT NULL,
    device_name VARCHAR(128) NOT NULL,
    device_role VARCHAR(64) NOT NULL, -- 'PrimaryPOS', 'SecondaryPOS', 'WaiterTablet', 'KDS'
    fingerprint_hash VARCHAR(64) NOT NULL,
    os_info VARCHAR(128),
    app_version VARCHAR(32),
    status VARCHAR(32) NOT NULL DEFAULT 'ACTIVE', -- 'ACTIVE', 'REVOKED', 'PENDING'
    last_heartbeat_at TIMESTAMPTZ,
    registered_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    revoked_at TIMESTAMPTZ,
    revocation_reason TEXT
);

-- Device Revocation Log
CREATE TABLE saas_device_revocations (
    revocation_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    device_id UUID NOT NULL REFERENCES saas_devices(device_id),
    revoked_by_user_id UUID NOT NULL,
    reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
```

### 7.2 Edge Local Database DDL (SQLite)

```sql
-- Local Device Hardware Identity & Status
CREATE TABLE local_device_identity (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    device_id TEXT NOT NULL,
    tenant_id TEXT NOT NULL,
    location_id TEXT NOT NULL,
    device_name TEXT NOT NULL,
    device_role TEXT NOT NULL,
    fingerprint_hash TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'ACTIVE', -- 'ACTIVE', 'REVOKED'
    activation_date DATETIME NOT NULL
);

-- Local Revocation List
CREATE TABLE local_revoked_devices (
    device_id TEXT PRIMARY KEY,
    revoked_at DATETIME NOT NULL,
    reason TEXT
);
```
