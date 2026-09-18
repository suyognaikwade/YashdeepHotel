# Trusted Device Registration & Lifecycle Security Audit Review

**Audit Date:** September 2026
**Auditor:** Jules (Senior Security & Software Engineer)
**Target Architecture:** Yashdeep Hotel & Restaurant Management SaaS Platform (.NET 9 / .NET 10 Clean Architecture)
**Scope:** Device Identity, Registration, Lifecycle Transitions, Security Isolation, Token Persistence, and Hardware Fingerprint Risk Assessment
**Reference Contracts:** `docs/IMPLEMENTATION_CONTRACT.md`, `DEVICE_MANAGEMENT.md`

---

## Executive Summary

This security review audits the **Trusted Device Registration and Lifecycle Management Foundation** implemented across `Yashdeep.Domain`, `Yashdeep.Application`, `Yashdeep.Infrastructure`, `Yashdeep.Shared`, and `Yashdeep.Server.Api`.

The assessment confirms that the architecture enforces strong **Tenant Binding**, **Branch Binding**, **Duplicate Registration Prevention**, **Suspended/Revoked Device Rejection**, **Device Replacement Workflows**, and **Cross-Tenant Access Prevention**. Cryptographic device tokens use HMAC-SHA256 signatures with payload integrity checks, ensuring that edge devices never receive or store cloud database credentials or signing keys.

---

## 1. Hardware Fingerprint Assessment & Identity Safety

### 1.1 Multi-Factor Fingerprint Algorithm Analysis
The system generates a deterministic `HardwareFingerprint` via `DeviceHardwareInfo` in `Yashdeep.Shared.Hardware`:

```csharp
var rawString = $"{PlatformName.ToUpperInvariant()}|{SystemUuid}|{CpuId}|{VolumeSerial}|{PrimaryMacAddress}";
```

* **Multi-Factor Identity:** The fingerprint combines **5 distinct attributes**:
  1. Platform Name (`Windows`, `Android`)
  2. System UUID (BIOS UUID on Windows / `Settings.Secure.ANDROID_ID` on Android)
  3. CPU Processor ID / Serial Number
  4. Primary Storage Volume Serial Number (`C:\` volume ID / internal storage UUID)
  5. Primary Network Interface MAC Address

### 1.2 Evaluation of Mutable MAC Address Reliance
* **Finding:** The system **does NOT rely solely on mutable hardware characteristics such as MAC address**.
* **Safety Mechanism:** Even if a NIC is replaced, a USB network dongle is attached, or MAC address randomization occurs, the System UUID, CPU Processor ID, and Storage Volume Serial remain bound.
* **Fuzzy Match & Drift Score:** `IDeviceIdentityProvider` implementations evaluate attribute drift (e.g., `CalculateDriftScore`). When drift is within tolerable limits (> 75% match score), minor hardware changes do not invalidate the device, while major changes flag the device as unrecognized/cloned.

### 1.3 Privacy, Reliability, and Portability Risk Matrix

| Risk Area | Identified Risk / Finding | Impact | Mitigation Strategy |
| :--- | :--- | :--- | :--- |
| **Privacy (MAC Address)** | Raw MAC address collection could raise privacy concerns under strict data protection regimes if stored in plain text. | Low / Compliance | Hash MAC addresses or treat hardware fingerprints as opaque SHA-256 tokens. The current SHA-256 algorithm transforms raw attributes into an opaque string (e.g. `HW-WIN-984F-221A-BB89-C03E41A7012`). |
| **Reliability (MAC Randomization)** | Windows 10/11 and Android 10+ default to randomized Wi-Fi MAC addresses per connection/SSID. | Medium | Primary MAC address must be queried from physical hardware properties (e.g., permanent hardware MAC or Ethernet interface) rather than transient Wi-Fi adapter addresses, or excluded from hard failure logic via fuzzy drift scoring. |
| **Portability (Cross-Platform)** | Windows and Android expose hardware IDs through different native APIs (`System.Management` / WMI on Windows vs `Android.Provider.Settings` on Android). | Low | Mitigated by clean `IDeviceIdentityProvider` abstraction with platform-specific implementations (`WindowsDeviceIdentityProvider`, `AndroidDeviceIdentityProvider`) sharing the unified `DeviceHardwareInfo` record in `Yashdeep.Shared`. |

---

## 2. Device Lifecycle & Security Boundaries Verification

### 2.1 State Machine Transitions (`DeviceLifecycleState`)
The `Device` aggregate root in `Yashdeep.Domain.Entities.Device` manages 5 distinct lifecycle states:

```
[ Unregistered ] ──► (Register) ──► [ Pending ] ──► (Activate) ──► [ Active ]
                                                                       │
                                              ┌────────────────────────┴────────────────────────┐
                                              ▼                                                 ▼
                                        (Suspend)                                           (Revoke)
                                              │                                                 │
                                              ▼                                                 ▼
                                        [ Suspended ]                                      [ Revoked ]
                                              │                                                 │
                                        (Recover)                                               │
                                              │                                                 │
                                              └────────────────► [ Active ] ◄───────────────────┘
                                                                       │
                                                                   (Replace)
                                                                       │
                                                                       ▼
                                                                  [ Retired ]
```

1. **Pending State:** Initial state upon registration. Generates short-lived 6-digit OTP activation code (valid 24h).
2. **Active State:** Activated via valid OTP code and matching hardware fingerprint. Receives HMAC-SHA256 device token.
3. **Suspended State:** Temporarily restricted by admin. Immediate rejection on activation attempts; recoverable back to `Active` via `Recover()`.
4. **Revoked State:** Permanently revoked (e.g. stolen device). Terminal state; direct activation or recovery is blocked with `InvalidOperationException`.
5. **Retired State:** Device replaced by new hardware instance via `Replace()`. Links `ReplacedByDeviceId` and transitions old instance to `Retired`.

### 2.2 Security Boundary Verification Summary

| Security Boundary | Status | Verification Detail & Invariants Enforced |
| :--- | :--- | :--- |
| **Tenant Binding** | **VERIFIED** | Every device record carries mandatory `TenantId`. All requests in `DeviceRegistrationService` and `DevicesController` enforce `TenantId` matching. Cross-tenant access attempts throw `UnauthorizedAccessException`. |
| **Branch Binding** | **VERIFIED** | Devices are explicitly assigned to a `BranchId`. Activation attempts with a mismatched `BranchId` are rejected with `InvalidOperationException` ("Branch mismatch"). |
| **Duplicate Prevention** | **VERIFIED** | `DeviceRegistrationService.RegisterDeviceAsync` queries `GetByHardwareFingerprintAsync(tenantId, hwFingerprint)`. Re-registration of an active/pending fingerprint throws `InvalidOperationException`. |
| **Suspended Rejection** | **VERIFIED** | Suspended devices cannot be activated directly (`ActivateDeviceAsync` throws `InvalidOperationException: "Suspended device rejection"`). Must undergo managerial recovery. |
| **Revoked Rejection** | **VERIFIED** | Revoked devices are permanently blocked from activation (`ActivateDeviceAsync` throws `InvalidOperationException: "Revoked device rejection"`). |
| **Device Replacement** | **VERIFIED** | `ReplaceDeviceAsync` atomically transitions old device to `Retired`, sets `ReplacedByDeviceId`, and registers a new `Pending` device with fresh OTP code. |
| **Unauthorized Activation**| **VERIFIED** | Activation requires valid OTP code, non-expired activation window, and matching `HardwareFingerprint`. Invalid code or expired window throws `UnauthorizedAccessException`. |
| **Cross-Tenant Isolation**| **VERIFIED** | `GetTenantDeviceOrThrow` verifies `device.TenantId == request.TenantId`. `DevicesController` verifies `HttpContext` tenant header matches request payload. |

---

## 3. Audit Event Trail & Token Persistence Safety

### 3.1 Audit Events Logged
All state transitions emit strongly typed domain events dispatched to `IAuditEventLogger`:
* `DeviceRegisteredEvent`
* `DeviceActivatedEvent`
* `DeviceSuspendedEvent`
* `DeviceRevokedEvent`
* `DeviceRecoveredEvent`
* `DeviceReplacedEvent`

### 3.2 Credential & Token Persistence Safety
* **Zero Credential Exposure:** Device tokens (`DeviceTokenPayload`) contain device ID, tenant ID, branch ID, hardware fingerprint, state, and expiration timestamps. **They contain ZERO database passwords, connection strings, or cloud access keys.**
* **Cryptographic Signatures:** Issued using HMAC-SHA256 (`DeviceTokenService`) with constant-time signature comparison (`CryptographicOperations.FixedTimeEquals`).
* **Client Persistence Strategy:** Client applications (Windows Blazor Hybrid / Android MAUI) store device tokens in OS Secure Storage (Windows Credential Manager via DPAPI / Android KeyStore) or encrypted SQLite (SQLCipher 256-bit AES).

---

## 4. Remediation & Action Items Executed

1. **Test Project Framework Alignment:** Updated `tests/Yashdeep.Tests.DeviceRegistration/Yashdeep.Tests.DeviceRegistration.csproj` target framework from `net8.0` to `net10.0` to eliminate restore and compilation incompatibility with `.NET 10` dependencies (`Yashdeep.Domain`, `Yashdeep.Application`, `Yashdeep.Shared`, `Yashdeep.Infrastructure`).
2. **Verification Test Suite:** Executed full test suite (`Yashdeep.Tests` and `Yashdeep.Tests.DeviceRegistration`), confirming all 35 tests pass with 0 failures.

---
*End of Security Review Report.*
