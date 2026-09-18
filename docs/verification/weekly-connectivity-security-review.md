# Weekly Connectivity Security Review & Enforcement Verification

**Document Status:** Final Audit & Verification Report
**Repository:** `suyognaikwade/YashdeepHotel`
**Target Branch:** `botify`
**Verification Date:** September 2026

---

## 1. Executive Summary

This security review and audit report documents the verification, hardening, and test coverage of the mandatory weekly connectivity enforcement model for edge POS terminals in the Yashdeep Hotel Management System SaaS platform.

### Core Architecture Rules Enforced:
1. **Mandatory 7-Day Window:** Every registered installation/device MUST successfully check in with the cloud server at least once every 7 calendar days (168 hours) to maintain full offline POS transactional privileges.
2. **Configurable Soft Grace Period:** The grace duration beyond 7 days is strictly configurable via `ConnectivityPolicyOptions.SoftGracePeriod` and is NOT hardcoded to any default duration.
3. **Server-Authoritative Time:** Server time obtained during authenticated online check-in is authoritative for all connectivity state calculations. Edge local system wall-clock time is untrusted.
4. **Clock Tamper Protection:** The client tracks hardware monotonic ticks (`IMonotonicClock`) alongside wall-clock time (`DateTime.UtcNow`). Any backward system clock manipulation or divergence from monotonic uptime triggers immediate state lockout (`ConnectivityState.RestrictedOperation`).
5. **State Precedence & Safety:** Priority hierarchy strictly isolates device status:
   `DeviceSuspension > UpdateRequirement > ClockTamper > Recovery > RestrictedOperation > ConnectivityExpiry > ConnectivityWarning > NormalOfflineOperation`.

---

## 2. Audit of Operational Connectivity State Implementations

| Connectivity State | Operational Behavior | Enforcement Level | Can Perform Transactions? |
| :--- | :--- | :--- | :---: |
| **NormalOfflineOperation** | Operating offline within valid weekly window (0 to 120 hours). | Full POS privileges active. | ✅ Yes |
| **ConnectivityWarning** | Offline for 120 to 168 hours (5–7 days). Non-blocking UI warning banner displayed. | Full POS privileges active. | ✅ Yes |
| **ConnectivityExpiry** | Offline duration exceeds 7 days (168 hours). Configurable soft grace period active. | Warning banner + grace period countdown. | ✅ Yes |
| **RestrictedOperation** | Offline duration exceeds 7 days + soft grace period, or clock tamper detected. | Operational lockout; read-only mode. | ❌ No |
| **SynchronizationFailure** | Online check-in attempt encountered transient network/HTTP errors. | Background retries active; POS operational. | ✅ Yes |
| **UpdateRequirement** | Cloud Web API mandates client software version update. | Operational lockout until update applied. | ❌ No |
| **DeviceSuspension** | Cloud administrator revoked device authorization. | Absolute lockout; authentication invalidated. | ❌ No |
| **Recovery** | Successful online check-in completed after warning, expiry, or sync failure. | Restores active status & resets 7-day window. | ✅ Yes |

---

## 3. Security Hardening & Tamper Resistance Mechanisms

### 3.1 Hardware Monotonic Tick Tracking
- **Abstraction:** `IMonotonicClock` interface backed by `SystemMonotonicClock` (`Stopwatch.GetTimestamp()` / `Environment.TickCount64`).
- **Mechanism:** When a verified check-in occurs (`SynchronizeServerTime`), the check-in record stores `ServerTimeUtc`, `LocalTimeUtc`, and `MonotonicTicks`.
- **Tamper Detection (`ClockTamperDetector`):**
  - **Backward Wall-Clock Rollback:** If `CurrentLocalTimeUtc < LastCheckIn.LocalTimeUtc - MaxAllowedClockSkew`, `IsClockTampered` returns `true`.
  - **Monotonic Divergence:** If monotonic elapsed seconds diverge from wall-clock elapsed seconds beyond allowed skew (e.g. wall clock moved backward relative to monotonic ticks), `IsClockTampered` returns `true`.
  - **Clock Rollback Attempt to Extend Window:** Even if a user attempts to extend the 7-day window by moving the local system clock backward from Day 8 to Day 3, the monotonic tick count (which accrued 8 days of ticks) reveals the discrepancy and forces immediate transition to `RestrictedOperation`.

### 3.2 Server-Authoritative Estimation
- **Abstraction:** `IServerTimeProvider` calculates `GetAuthoritativeTimeUtc()`.
- **Estimation Algorithm:**
  `AuthoritativeTimeUtc = LastVerifiedServerTimeUtc + (CurrentMonotonicTicks - LastCheckInMonotonicTicks) / TickFrequency`
- **Result:** The edge client estimates true UTC time using hardware ticks since the last verified server check-in, rendering system wall-clock modification completely ineffective for bypassing entitlement expiration.

---

## 4. Verification Test Matrix

The test suite in `tests/Yashdeep.Connectivity.Tests/ConnectivityStateEvaluatorTests.cs` deterministically validates all boundary conditions and operational states:

| Test Method | Test Scenario & Boundary Condition | Verified Expected State | Status |
| :--- | :--- | :--- | :---: |
| `Test_1_SuccessfulOnlineCheckIn_ReturnsRecoveryAndUpdatesServerTime` | Online check-in with verified server timestamp. | `ConnectivityState.Recovery` | ✅ PASS |
| `Test_2_OfflineOperationWithinSevenDays_ReturnsNormalOfflineOperation` | Offline at Day 3 (72 hours offline). | `ConnectivityState.NormalOfflineOperation` | ✅ PASS |
| `Test_3_WarningState_TriggeredBetweenWarningThresholdAndSevenDays` | Offline at Day 6 (144 hours offline). | `ConnectivityState.ConnectivityWarning` | ✅ PASS |
| `Test_4_Boundary_ImmediatelyBeforeSevenDays_ReturnsConnectivityWarning` | Offline at 6 days, 23 hours, 59 minutes (167h 59m). | `ConnectivityState.ConnectivityWarning` | ✅ PASS |
| `Test_4b_Boundary_ExactlyAtSevenDays_ReturnsConnectivityExpiry` | Offline at exactly 7.0 days (168.0 hours). | `ConnectivityState.ConnectivityExpiry` | ✅ PASS |
| `Test_4c_Boundary_ImmediatelyAfterSevenDays_ReturnsConnectivityExpiry` | Offline at 7 days, 00 hours, 01 minute (168h 01m). | `ConnectivityState.ConnectivityExpiry` | ✅ PASS |
| `Test_4d_GraceBoundary_ImmediatelyBeforeSoftGraceExpiry_ReturnsConnectivityExpiry` | Offline at 7d + 3d grace - 1 minute. | `ConnectivityState.ConnectivityExpiry` | ✅ PASS |
| `Test_4e_GraceBoundary_ImmediatelyAfterSoftGraceExpiry_ReturnsRestrictedOperation` | Offline at 7d + 3d grace + 1 minute. | `ConnectivityState.RestrictedOperation` | ✅ PASS |
| `Test_5_ClockMovedBackward_TriggersRestrictedOperationAndClockTamperFlag` | Local system clock manually rolled back from Day 5 to Day 2. | `ConnectivityState.RestrictedOperation` | ✅ PASS |
| `Test_5b_ClientClockTamperAttemptToExtendWindow_DetectedAndLocked` | Offline for 8 days; user rolls clock back to Day 3. Monotonic tick check catches manipulation. | `ConnectivityState.RestrictedOperation` | ✅ PASS |
| `Test_5c_LargeClockForwardJump_EvaluatedCorrectly` | Clock jumped +30 days while hardware ticks show 1 hour. Wall-clock > max window forces lockout. | `ConnectivityState.RestrictedOperation` | ✅ PASS |
| `Test_5d_DeviceNeverConnected_HandlesNullCheckInSafely` | Newly installed terminal with zero prior check-in record. | `ConnectivityState.NormalOfflineOperation` | ✅ PASS |
| `Test_6_ServerTimeUpdate_UpdatesCheckInStoreAndAuthoritativeTime` | Server time sync updates persistent check-in record. | Verified `ServerTimeUtc` updated | ✅ PASS |
| `Test_7_RecoveryAfterReconnect_RestoresActiveStateFromExpiryOrFailure` | Successful reconnect after Day 8 expiry state. | `ConnectivityState.Recovery` | ✅ PASS |
| `Test_7b_OnlineSyncFailure_ReturnsSyncFailureAndPermitsLocalTxn` | Online but sync endpoint returned HTTP 503 error. | `ConnectivityState.SynchronizationFailure` | ✅ PASS |
| `Test_8_DeviceSuspension_OverridesAllOtherConnectivityStates` | Cloud administrator suspended device while online. | `ConnectivityState.DeviceSuspension` | ✅ PASS |
| `Test_9_UpdateRequiredState_OverridesNormalOfflineOperation` | Mandatory software update mandated by Cloud API. | `ConnectivityState.UpdateRequirement` | ✅ PASS |

---

## 5. Summary of Code & Configuration Modifications

1. **`src/Domain/Yashdeep.Domain/Connectivity/ConnectivityModels.cs`:**
   - Added optional `ServerTimeUtc` to `ConnectivityEvaluationContext` to pass verified online server time during check-in evaluations.
2. **`src/Application/Yashdeep.Application/Connectivity/TimeAndCheckInInterfaces.cs`:**
   - Updated `IServerTimeProvider.SynchronizeServerTime` overload to support explicit local time association.
3. **`src/Infrastructure/Yashdeep.Infrastructure/Connectivity/TimeAndCheckInImplementations.cs`:**
   - Hardened `ClockTamperDetector` to handle monotonic clock tick resets/reboots safely.
   - Updated `ServerTimeProvider.SynchronizeServerTime` to record authoritative server time and corresponding local time atomically.
4. **`src/Infrastructure/Yashdeep.Infrastructure/Connectivity/ConnectivityStateEvaluator.cs`:**
   - Updated online check-in handling to pass verified server time to `SynchronizeServerTime`.
   - Updated fallback `LastVerifiedServerTimeUtc` tracking across all evaluation results.
5. **`tests/Yashdeep.Connectivity.Tests/Yashdeep.Connectivity.Tests.csproj`:**
   - Updated target framework to `net10.0` to match `Yashdeep.Infrastructure` and resolve restore/build conflicts.
6. **`tests/Yashdeep.Connectivity.Tests/ConnectivityStateEvaluatorTests.cs`:**
   - Expanded test suite from 9 to 17 comprehensive deterministic unit/integration tests covering all boundary and tamper resistance scenarios.

---
**End of Review Report.**
