# Device Provisioning, Revocation & Offline Management Specification

## 1. Overview & Device Topology

This document specifies the device management architecture, hardware fingerprinting, activation workflows, remote revocation, and offline plan change behavior for the **Yashdeep SaaS Platform**.

### Operating Environment Topology
POS terminals operate across heterogeneous hardware types within a hotel/restaurant branch:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Location Device Topology                        │
├───────────────────────────────┬────────────────────────────────────────┤
│ Device Type                   │ Primary Operational Function           │
├───────────────────────────────┼────────────────────────────────────────┤
│ Windows Desktop POS Terminal  │ Main Billing Cashier, Day-End Audit    │
│ Android / Touch Mobile POS    │ Waiter Table Order Taking (KOT / BOT)  │
│ Kitchen Display System (KDS)  │ Kitchen Order Ticket Display & Routing  │
│ Bar Counter Station           │ Loose Peg Dispensing & Bar BOT Print   │
└───────────────────────────────┴────────────────────────────────────────┘
```

---

## 2. Hardware Fingerprinting & Identity Binding

To enforce per-device licensing limits and prevent unauthorized cloning of local SQLite databases onto unlicensed hardware, every POS terminal generates a deterministic **Hardware Fingerprint**:

### 2.1 Hardware Fingerprint Composition Algorithm
The fingerprint is calculated by hashing immutable hardware properties:

```
                  ┌─────────────────────────────────────┐
                  │ Motherboard BIOS Serial / UUID     │
                  ├─────────────────────────────────────┤
                  │ CPU Processor Serial / ID           │
                  ├─────────────────────────────────────┤
                  │ Network Interface Primary MAC Addr  │
                  ├─────────────────────────────────────┤
                  │ OS Storage Volume Serial (C:\ UUID) │
                  └──────────────────┬──────────────────┘
                                     │
                                     ▼
                  [ Concatenate Strings with Delimiter '|' ]
                                     │
                                     ▼
                  [ SHA-256 Cryptographic Hash Engine ]
                                     │
                                     ▼
                   "HW-WIN-984F-221A-BB89-C03E41A7012"
```

### 2.2 Security & Device Identity Claims
- **Device Identity Binding**: Device registration pairs `HardwareFingerprint` + `DeviceId` + `TenantId` on the Cloud Authority.
- **Hardware Drift Threshold**: If minor components change (e.g. NIC driver or USB dongle), system computes fuzzy match score. If > 75% hardware attributes match, fingerprint automatically updates upon cloud sync. If < 75% match, device is flagged as **Cloned/Unrecognized** and requires admin re-authorization.

---

## 3. Device Provisioning & Registration Protocol

```
┌────────────────────────────────────────────────────────────────────────┐
│                  Device Provisioning & Pairing Workflow                │
├────────────────────────────────────────────────────────────────────────┤
│ 1. Admin opens Cloud Management Portal -> Clicks "Add POS Device"     │
│ 2. Cloud API generates short-lived 6-digit OTP (e.g., "782-910")       │
│ 3. Operator installs Blazor Hybrid App on POS Hardware                │
│ 4. Operator enters Tenant ID & 6-digit OTP on POS Setup Screen        │
│ 5. POS transmits: OTP + HardwareFingerprint + DeviceName + OS Platform│
│ 6. Cloud API validates OTP & checks tenant `max_devices_per_location` │
│ 7. Cloud registers Device, binds Fingerprint, issues Device JWT +     │
│    Cryptographically Signed Entitlement Token                          │
│ 8. POS securely stores credentials in encrypted local storage/SQLite  │
└────────────────────────────────────────────────────────────────────────┘
```

### 3.1 Device Registration Flow Diagram

```
[ Edge POS Terminal ]                                      [ Cloud API Gateway ]
        │                                                            │
        │── 1. POST /api/v1/devices/pair ───────────────────────────►│
        │   Payload: { tenantId, otp, hwFingerprint, deviceName }   │
        │                                                            │── Check OTP validity
        │                                                            │── Check plan device limits
        │                                                            │── Register device in DB
        │◄── 2. HTTP 200 OK ─────────────────────────────────────────│
        │   Response: { deviceId, deviceToken, signedEntitlementToken }│
        │                                                            │
  [ Store Local SQLite ]                                             │
```

---

## 4. Device Allocation & Capacity Limit Enforcement

### 4.1 Plan Limit Check Rules
During pairing, the Cloud API checks active device counts against the Tenant's subscription limits:

$$\text{ActiveRegisteredDevices} + 1 \le \text{SubscriptionLimit}(\text{max\_devices\_per\_location}) \times \text{ActiveLocations}$$

### 4.2 Handling Allocation Overages
- If allocation limit is reached:
  - Pairing fails with error: `DEVICE_LIMIT_EXCEEDED` (HTTP 403 Forbidden).
  - Admin receives option in Cloud Portal to either:
    1. **Deactivate an existing registered device** to free up a slot.
    2. **Upgrade Subscription Plan** to a higher tier allowing more devices.

---

## 5. Device Revocation & Remote Deactivation

Device revocation can occur due to device loss/theft, subscription downgrade, or admin action.

```
                                 [ Revocation Triggered ]
                                 (Lost Tablet / Downgrade)
                                            │
                                            ▼
                             [ Cloud API Revokes Device ]
                             (Updates DB & Revocation List)
                                            │
                    ┌───────────────────────┴───────────────────────┐
                    ▼                                               ▼
          [ Terminal Connected (Online) ]                [ Terminal Offline ]
                    │                                               │
                    ▼                                               ▼
         [ Instant WebSocket Push ]                   [ Terminal Re-connects Later ]
         [ Dispatches Revoke Event ]                   [ Blocked at HTTP Sync API ]
                    │                                               │
                    ▼                                               ▼
         [ Lock Local POS Screen ]                    [ Local Token Signature Expired ]
         [ Purge SQLite Entitlement ]                 [ Lock Local POS Screen ]
```

### 5.1 Real-Time Online Revocation (SignalR Push)
1. Admin clicks "Revoke Device" in Cloud Portal.
2. Cloud API broadcasts `DeviceRevoked` event over SignalR WebSocket connection to target `DeviceId`.
3. POS application catches event:
   - Purges local cached signed entitlement token.
   - Sets local operational state to **Revoked / Read-Only**.
   - Clears local user sessions and displays modal: *"This device registration has been revoked by your administrator."*

### 5.2 Offline Revocation & Token Revocation List (TRL)
If a device is stolen or lost while offline:
1. Cloud API records `DeviceId` and `HardwareFingerprint` on the **Global Token Revocation List (TRL)**.
2. During offline operation, the stolen device continues operating ONLY until its local `offline_grace_exp` timestamp expires (maximum 3 to 30 days depending on plan).
3. The moment the device attempts any internet connectivity:
   - Cloud Sync endpoint inspects request headers.
   - Detects revoked `DeviceId` on TRL.
   - Returns HTTP 401 Unauthorized with header `X-Device-Revoked: true`.
   - POS terminal processes response and forcibly locks local POS capabilities.

---

## 6. Plan Changes & Device Behavior While Offline

### 6.1 Plan Changes Made While Edge Terminal is Offline
If an Organization admin changes the subscription plan in the Cloud Portal while one or more POS terminals are offline:

| Plan Change Event | Offline Device Behavior | Re-Connection & Reconciliation Behavior |
| :--- | :--- | :--- |
| **Upgrade** (e.g. Starter → Pro) | Device continues running with old token limits until internet is restored. | Upon reconnect, POS syncs outbox, receives new signed token, and instantly unlocks upgraded features (e.g. Excise FL-III tab appears). |
| **Downgrade** (e.g. Pro → Starter) | Device continues running with cached Pro token until `exp` / `offline_grace_exp` expires. | Upon reconnect, POS receives downgraded token. If current device exceeds new limit, POS is designated as **Excess Device** and locked until de-registered. |
| **Cancellation / Suspension** | Device runs in Grace Period until cached token `offline_grace_exp` is reached. | Upon reconnect, Cloud API rejects sync requests with HTTP 402. Terminal locks to Read-Only mode. |

### 6.2 Offline Device Downgrade Resolution Logic

If a tenant downgrades from 10 devices to 2 devices while 8 devices are offline:

```
                          [ POS Terminal Reconnects ]
                                       │
                                       ▼
                     [ Send Cloud Sync Request + Token ]
                                       │
                                       ▼
                   [ Cloud Validates Active Device Index ]
                                       │
               ┌───────────────────────┴───────────────────────┐
               ▼                                               ▼
   [ Device Index <= New Limit ]                   [ Device Index > New Limit ]
               │                                               │
               ▼                                               ▼
    [ Issue Fresh Token ]                       [ Return HTTP 403 Forbidden ]
    [ Maintain Active POS ]                     [ Error: DEVICE_SLOT_EXCEEDED ]
                                                               │
                                                               ▼
                                                [ Local POS Locks Screen ]
                                                [ Display Selection Modal ]
```

---

## 7. Verification and File Integrity

This document forms the core device management specification.

- Target File Path: `DEVICE_MANAGEMENT.md`
- Markdown Format Verified: Yes
- Aligned with Architecture Specs (`docs/ARCHITECTURE.md`) and Rules (`AGENTS.md`).
