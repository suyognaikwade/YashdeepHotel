# Outbox Atomicity and Durability Review

**Document Version:** 1.0
**Repository:** `suyognaikwade/YashdeepHotel`
**Target Branch:** `botify`
**Date:** September 2026
**Status:** Complete / Verified

---

## Executive Summary

This audit report evaluates the local Outbox pattern implementation within the modern Yashdeep Hotel Management SaaS platform, verifying its adherence to `docs/IMPLEMENTATION_CONTRACT.md`, `OFFLINE_ARCHITECTURE.md`, and Clean Architecture design guidelines.

The durable local outbox guarantees **At-Least-Once event delivery** and **atomic transactional consistency** across edge POS terminals. All business mutations (e.g., local order placement, bill generation, inventory stock deductions) and outbox event records occur within a single, explicit local database transaction boundary managed by EF Core SQLite / SQLCipher.

---

## Audit Findings & Persistence Architecture Review

### 1. POS Implementation & Persistence Provider Audit
* **Production Persistence Stack:**
  * **Database Engine:** SQLite 3 with SQLCipher 256-bit AES encryption (`PRAGMA key`, `PRAGMA journal_mode=WAL`).
  * **DbContext:** `LocalPosDbContext` (in `Yashdeep.Persistence.Local`) manages local domain entities (`LocalOrder`) and outbox messages (`OutboxMessage`).
  * **Unit of Work & Repository:** `LocalUnitOfWork` provides explicit transaction management (`BeginTransactionAsync`, `CommitAsync`, `RollbackAsync`), while `OutboxRepository` implements `IOutboxRepository`.
* **POS Vertical Slice Inspection:**
  * The initial vertical slice test suite (`PosVerticalSliceTests`) utilizes an in-memory DbContext (`LocalPosMemoryDbContext`) for lightweight unit verification of POS workflow logic.
  * Production edge deployments use `LocalPosDbContext` connected to encrypted SQLite storage, ensuring outbox messages and local transactions are written to physical disk with Write-Ahead Logging (WAL) enabled.

### 2. Transaction Boundary Verification
* Business entity additions/modifications and outbox message creations share the same `LocalPosDbContext` instance and `IDbDbContextTransaction`.
* Calling `unitOfWork.SaveChangesAsync()` followed by `transaction.CommitAsync()` guarantees atomic persistence.
* If an unhandled application exception occurs prior to commit or `transaction.RollbackAsync()` is invoked, both business entity changes and outbox event records are discarded atomically.

---

## Verification of Test Scenarios

All 15 required failure, atomicity, and metadata resilience scenarios were verified via automated unit and integration test suites in `tests/Yashdeep.Outbox.Tests`:

| Scenario | Test Method | Outcome / Verification Mechanism |
| :--- | :--- | :--- |
| **1. Commit Success** | `CommitTransaction_PersistsBothBusinessMutationAndOutboxEventAtomically` | Verified both `LocalOrder` and `OutboxMessage` persist atomically on `transaction.CommitAsync()`. |
| **2. Transaction Rollback** | `RollbackTransaction_DoesNotPersistOutboxEventOrBusinessMutation` | Verified zero persistence of order or outbox message when `transaction.RollbackAsync()` is executed. |
| **3. Application Failure During Transaction** | `ApplicationFailure_MidwayThroughTransaction_RollsBackAllMutationsAndOutboxMessages` | Verified that an unhandled exception thrown before commit aborts transaction, resulting in zero persisted records. |
| **4. Process Restart with Unsent Messages** | `ProcessRestartWithUnsentMessages_PersistsToDiskAndRecoversPendingOutboxMessages` | Verified SQLite file-based persistence allows a new process instance/DbContext to recover unsent pending messages. |
| **5. Duplicate Event IDs** | `EventIdentityUniqueness_PreventsDuplicateEventIds` | Verified database primary key constraint (`EventId`) throws `DbUpdateException` on duplicate event insertion. |
| **6. Event Payload Integrity Hash** | `PayloadIntegrity_FailsWhenPayloadSilentlyTampered` | Verified SHA-256 checksum validation via `VerifyPayloadIntegrity()` detects silent payload modifications. |
| **7. Event Ordering** | `DeterministicSequenceNumbering_GeneratesSequentialSequencePerDevice` | Verified monotonic sequence numbering (`SequenceNumber`) per device ID via `GetNextSequenceNumberAsync()`. |
| **8. Event Version** | `EventVersionValidation_ConstructsWithValidVersion_AndRejectsInvalidVersion` | Verified positive `EventVersion` requirement and rejection of non-positive versions (`<= 0`). |
| **9. Tenant Identity** | `ContextVerification_StoresAndFiltersTenantBranchAndDeviceContext` | Verified `TenantId` metadata storage and filtering isolation across multi-tenant contexts. |
| **10. Branch Identity** | `ContextVerification_StoresAndFiltersTenantBranchAndDeviceContext` | Verified `BranchId` contextual metadata persistence and queries. |
| **11. Device Identity** | `ContextVerification_StoresAndFiltersTenantBranchAndDeviceContext` | Verified `DeviceId` filtering and per-device unique sequence index (`HasIndex(e => new { e.DeviceId, e.SequenceNumber }).IsUnique()`). |
| **12. Retry Metadata** | `OutboxStateTransitions_HandlesRetryAndDeadLetterTransitionsSafely` | Verified incremental tracking of `RetryCount`, `LastAttemptedUtc`, `NextRetryUtc`, and exponential backoff calculations. |
| **13. Dead-Letter State** | `OutboxStateTransitions_HandlesRetryAndDeadLetterTransitionsSafely` | Verified automatic transition to `OutboxStatus.DeadLetter` when `RetryCount >= maxRetries`. |
| **14. Pre-ACK Cloud Sync Safety** | `PreAckSafety_EventsRemainUnsyncedUntilExplicitCloudAck_AndDeadLetterCannotMarkSynced` | Verified outbox events remain `Pending` / `InFlight` until cloud Web API acknowledges receipt; invalid state transitions are blocked. |
| **15. Recoverability After Restart** | `ProcessRestartWithUnsentMessages_PersistsToDiskAndRecoversPendingOutboxMessages` | Verified unsent outbox messages survive cold application restarts and remain recoverable via `GetPendingBatchAsync()`. |

---

## Recommendations & Compliance

1. **Production SQLite Initialization:** All POS client startup paths must initialize SQLite via `LocalDatabaseInitializer` to ensure SQLCipher key derivation and `PRAGMA journal_mode=WAL` are active before executing outbox transactions.
2. **Target Framework Standardization:** Solution target frameworks have been unified to `.NET 10` across shared, domain, application, persistence, and test projects, resolving restore/build compatibility gaps.
3. **Continuous Test Gate:** Execute `DOTNET_ROLL_FORWARD=Major dotnet test YashdeepHotelMS.sln` during pre-commit and CI/CD runs to maintain 100% pass rate on outbox atomicity and durability tests.
