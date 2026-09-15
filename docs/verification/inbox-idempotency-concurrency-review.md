# Cloud Inbox Idempotency and Concurrent Replay Safety Review

## 1. Executive Summary

This document provides an architecture and security audit of the `CloudInboxProcessor` and `InboxMessage` persistence layer in the Yashdeep Hotel Management SaaS platform.

The Cloud Inbox is a fundamental component of the offline-first sync protocol. It acts as the idempotent entry point for event envelopes received from edge POS terminals, ensuring that network retries, duplicate requests, parallel submission races, and payload tampering attempts are handled deterministically without corrupting tenant state or re-executing domain business logic.

---

## 2. Architecture & Persistence Configuration

### 2.1 Entity Model & Composite Key Definition
The `InboxMessage` aggregate root is defined in `Yashdeep.Domain.Sync.InboxMessage` and persisted via `CloudDbContext` (`Yashdeep.Persistence.Cloud.CloudDbContext`).

The database composite primary key is defined as:
```csharp
entity.HasKey(e => new { e.TenantId, e.EventId });
```

This enforces strict database-level uniqueness across multi-tenant boundaries. A duplicate event submitted within the same tenant context violates the unique constraint at the database tier, triggering a `DbUpdateException`.

### 2.2 Entity Properties & Indexing
- **Composite Primary Key**: `(TenantId, EventId)`
- **Tenant Isolation**: Soft tenant query filters are configured via `ITenantContext`, but inbox duplicate checks use `.IgnoreQueryFilters()` to prevent tenant context spoofing or cross-tenant query leaks.
- **Ordering Index**: Index created on `(TenantId, DeviceId, SequenceNumber)` for efficient stream auditing and out-of-order event sequence tracking.
- **Payload Integrity**: Payload hash (`PayloadHash`) computed using SHA-256 (`ComputePayloadHash()`) stored as a 64-character hexadecimal string.

---

## 3. Cloud Inbox Processor Workflow Audit

The processing flow implemented in `CloudInboxProcessor.cs` follows a 3-step idempotent pipeline:

```
[ Incoming Event Envelope ]
           │
           ▼
[ Step 1: Tenant Boundary Check ] ── (TenantId != AuthenticatedTenantId) ──► Return InboxStatus.Rejected
           │
           ▼
[ Step 2: Payload JSON Validation ] ── (Malformed JSON / Deserialization Error) ──► Return InboxStatus.Rejected
           │
           ▼
[ Step 3: Existing Message Check ] ── (Record Found in DB)
           ├───────────────► Hash Mismatch? ──► Set InboxStatus.PayloadMismatch & Return Error
           └───────────────► Status == Processed? ──► Return Cached Response (IsDuplicate = true)
           │
           ▼
[ Step 4: Atomic DB Transaction ]
           ├─ 1. Add InboxRecord (Status = Processing)
           ├─ 2. SaveChangesAsync() ──► Duplicate Key Violation? ──► Catch DbUpdateException & Recover
           ├─ 3. Execute Domain Handler
           ├─ 4. Update InboxRecord (Status = Processed, ResponsePayloadJson)
           └─ 5. Commit Transaction
```

---

## 4. Race Condition & Concurrency Analysis

### 4.1 Race Condition Scenario
When multiple identical requests arrive simultaneously from parallel HTTP requests or concurrent retry queues:
1. **Thread A** and **Thread B** simultaneously check for an existing message in Step 3. Neither finds a record because none is committed yet.
2. Both threads enter the transaction block and call `_dbContext.InboxMessages.AddAsync()`.
3. **Thread A** wins the database write lock in `SaveChangesAsync()`, successfully inserting `InboxRecord` with `Status = Processing`.
4. **Thread B** encounters a primary key duplicate constraint violation (`DbUpdateException` with SQL state `23505` or SQLite `UNIQUE constraint failed`).

### 4.2 Recovery Mechanism & EF Core ChangeTracker Hardening
When **Thread B** catches `DbUpdateException`:
1. **Thread B** rolls back its database transaction.
2. **ChangeTracker Hardening**: Calling `_dbContext.ChangeTracker.Clear()` purges the tracked `InboxMessage` entity that failed to insert, preventing invalid EF Core tracking state in subsequent queries.
3. **Polling Loop with AsNoTracking**: Thread B enters a retry loop (up to 10 attempts with 50ms polling delay) using `.AsNoTracking()` to query the database directly for Thread A's committed result:
```csharp
var reRead = await _dbContext.InboxMessages
    .IgnoreQueryFilters()
    .AsNoTracking()
    .FirstOrDefaultAsync(x => x.TenantId == authenticatedTenantId && x.EventId == envelope.EventId, cancellationToken);
```
4. Once Thread A commits `Status = Processed`, Thread B reads the stored `ResponsePayloadJson`, deserializes it, and returns `SyncProcessingResult<TResponse>.Duplicate(cachedResponse)` without re-executing the domain handler.

---

## 5. Verification Scenarios & Integration Test Suite

The test suite `tests/Yashdeep.SyncEngine.Tests/CloudInboxProcessorIntegrationTests.cs` verifies all 10 core operational edge cases against relational database storage:

| Test Case Scenario | Test Method Name | Expected Behavior | Verification Status |
| :--- | :--- | :--- | :--- |
| **First Submission** | `ProcessEventAsync_FirstSubmission_ExecutesDomainHandlerAndStoresProcessedInbox` | Executes handler once, stores `InboxMessage` with `Status = Processed` and payload hash. | **PASSED** |
| **Duplicate Submission** | `ProcessEventAsync_DuplicateSubmission_DoesNotReExecuteDomainHandler_ReturnsCachedResult` | Skips domain handler, returns stored `ResponsePayloadJson` as `IsDuplicate = true`. | **PASSED** |
| **Parallel Concurrency** | `ProcessEventAsync_TrueParallelConcurrentSubmissions_ExecutesDomainHandlerOnceOnly` | Runs 8 parallel `Task.WhenAll` threads. Exactly 1 executes handler; 7 recover via duplicate key constraint handling and return cached response. | **PASSED** |
| **Payload Tampering** | `ProcessEventAsync_EventIdReuseWithModifiedPayload_RejectsEventAndStoresPayloadMismatch` | Detects `PayloadHash` mismatch for reused `EventId`, sets `InboxStatus.PayloadMismatch`, logs diagnostic JSON, and rejects request. | **PASSED** |
| **Cross-Tenant Isolation** | `ProcessEventAsync_TenantMismatch_RejectsEventAndDoesNotExecuteDomainHandler` | Envelope specifying Tenant A submitted under Tenant B context is rejected with `InboxStatus.Rejected`. | **PASSED** |
| **Multi-Tenant Key Boundary** | `ProcessEventAsync_SameEventUnderDifferentTenant_ProcessedIndependently` | Identical `EventId` submitted under Tenant A and Tenant B process independently without key collision. | **PASSED** |
| **Network Timeout / Retry** | `ProcessEventAsync_ConcurrentDuplicateSubmission_RecoversSafelyWithoutDuplicateDomainExecution` | Simulated client HTTP timeout retry returns cached response seamlessly. | **PASSED** |
| **Server Response Loss** | `ProcessEventAsync_ServerCommitFollowedByLostResponse_ReturnsCachedResultOnRetry` | Server commits transaction but network drops response; client retry retrieves committed response without re-executing logic. | **PASSED** |
| **Transaction Rollback** | `ProcessEventAsync_DomainHandlerFailure_RollsBackTransaction` | Domain handler exception triggers full transaction rollback; no orphaned `InboxMessage` left in database. | **PASSED** |
| **Malformed Payload** | `ProcessEventAsync_MalformedJsonPayload_ReturnsRejectedWithoutExecutingDomainHandler` | Malformed JSON string rejected before domain handler execution. | **PASSED** |
| **Application Restart** | `ProcessEventAsync_ApplicationRestartSimulation_RetrievesCachedResponseFromFreshDbContext` | Fresh `DbContext` instance after simulated application restart correctly reads cached response from persistent storage. | **PASSED** |

---

## 6. Audit Conclusions & Recommendations

1. **Database-Level Uniqueness**: Confirmed present. The EF Core composite key `(TenantId, EventId)` generates an explicit database UNIQUE index, guaranteeing that race conditions are resolved at the database engine level.
2. **Transaction Isolation**: Confirmed sufficient. Database transaction scope wraps `InboxMessage` insertion and domain handler execution atomically. Failure at any point in the domain logic rolls back the inbox record cleanly.
3. **ChangeTracker Safety**: Hardened during this audit. `_dbContext.ChangeTracker.Clear()` and `.AsNoTracking()` ensure that EF Core context state remains clean after handling database duplicate constraint exceptions.
4. **Tenant Isolation**: Strict cross-tenant boundary validation prevents tenant spoofing or cross-tenant event processing.
