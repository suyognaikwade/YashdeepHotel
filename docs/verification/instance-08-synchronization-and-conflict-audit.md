# Instance 08: Synchronization Architecture and Conflict Audit

**System Role:** Lead SaaS Architect & Systems Integration Auditor
**Target Platform:** Yashdeep Hotel Management System Modernization (.NET 9 Blazor Hybrid / SQLite Edge POS / PostgreSQL 16 Multi-Tenant Cloud)
**Audit Date:** March 2025
**Document Status:** Complete Audit Report

---

## 1. Executive Summary & Verdict

This report provides an exhaustive technical audit of the synchronization architecture, outbox/inbox protocol, conflict handling, network resilience, and data integrity design for the modernized **Yashdeep Hotel Management System**.

### 1.1 Core Implementation Verdict

| Dimension | Audit Verdict | Key Finding |
| :--- | :--- | :--- |
| **Code Implementation** | ❌ **0% Implemented (Missing)** | No C# / .NET 9 synchronization services, endpoints, outbox workers, or EF Core DbContext entities currently exist in the codebase. Legacy VB.NET code (`RSS26/`) operates purely on local Access `.mdb` file shares with zero synchronization capability. |
| **Architectural Specification** | ⚠️ **Partially Documented (Conceptual)** | Synchronization patterns (Outbox schema, SQLite WAL, device identity, basic HTTP push) are specified in `OFFLINE_ARCHITECTURE.md` and `SYSTEM_ARCHITECTURE.md`, but critical conflict resolution, dead-letter queueing, delta pull checkpoints, and network interruption policies are undocumented or incomplete. |
| **Multi-Device Offline Safety** | 🚨 **UNSAFE / HIGH RISK** | Operating multiple edge POS terminals offline under the current specification WILL result in lost transactions, split-brain dining table states, Excise FL-III inventory discrepancies, and sequence collisions upon cloud synchronization. |

---

## 2. Synchronization Component Inspection (23 Areas)

The system specification and repository artifacts were audited across 23 mandatory synchronization components:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        SYNCHRONIZATION COMPONENT AUDIT MATRIX                          │
├───────────────────────┬──────────────────────┬─────────────────────────────────────────┤
│ Component             │ Status               │ Primary Reference                       │
├───────────────────────┼──────────────────────┼─────────────────────────────────────────┤
│ 1. Outbox             │ Documented (Partial) │ OFFLINE_ARCHITECTURE.md §6.1            │
│ 2. Inbox              │ Documented (Partial) │ OFFLINE_ARCHITECTURE.md §6.2            │
│ 3. Sync Services      │ Conceptual Only      │ SYSTEM_ARCHITECTURE.md §3.10            │
│ 4. Sync Endpoints     │ Conceptual Only      │ SYSTEM_ARCHITECTURE.md §4.2             │
│ 5. Batch Processing   │ Documented (Partial) │ OFFLINE_ARCHITECTURE.md §6.1            │
│ 6. Retry Policies     │ Missing              │ OFFLINE_ARCHITECTURE.md §6.1 (Field)    │
│ 7. Idempotency Keys   │ Documented (Partial) │ ARCHITECTURE_REVIEW.md §15.3            │
│ 8. Deduplication      │ Documented (Partial) │ OFFLINE_ARCHITECTURE.md §6.2            │
│ 9. Conflict Detection │ Missing              │ ARCHITECTURE_REVIEW.md (Brief note)     │
│ 10. Conflict Resolut. │ Missing              │ PROJECT_ANALYSIS.md §7                  │
│ 11. Ordering          │ Documented (Partial) │ OFFLINE_ARCHITECTURE.md §6.1            │
│ 12. Timestamps        │ Documented (Partial) │ DOMAIN_MODEL.md §2.7.3                  │
│ 13. Device Identity   │ Fully Documented     │ DEVICE_MANAGEMENT.md §2 & §3            │
│ 14. Sync Checkpoints  │ Missing              │ OFFLINE_ARCHITECTURE.md §11.3           │
│ 15. Partial Sync      │ Missing              │ None                                    │
│ 16. Network Loss      │ Conceptual Only      │ OFFLINE_ARCHITECTURE.md §2              │
│ 17. Duplicate Submit  │ Documented (Partial) │ OFFLINE_ARCHITECTURE.md §6.2            │
│ 18. Server ACKs       │ Conceptual Only      │ DOMAIN_MODEL.md §458                    │
│ 19. Failure Recovery  │ Documented (Partial) │ OFFLINE_ARCHITECTURE.md §11             │
│ 20. Dead-Letter Queue │ Missing              │ None                                    │
│ 21. Audit Records     │ Documented (Partial) │ DOMAIN_MODEL.md §2.7.3                  │
│ 22. SignalR Notific. │ Documented (Partial) │ ARCHITECTURE_REVIEW.md §53              │
│ 23. REST APIs         │ Documented (Partial) │ ARCHITECTURE_REVIEW.md §8.3             │
└───────────────────────┴──────────────────────┴─────────────────────────────────────────┘
```

### 2.1 Component Breakdown & Detailed Findings

1. **Outbox (`OutboxMessages`)**:
   - *Status:* Documented (Partial / Conceptual)
   - *Details:* Table schema defined in SQLite (`Id`, `DeviceId`, `SequenceNumber`, `EventType`, `PayloadJson`, `CreatedAtUtc`, `RetryCount`, `SyncStatus`, `LastError`). Written transactionally alongside entity mutations.
   - *Gaps:* Missing sequence gap handling (if message 10 fails, do 11-15 wait?), payload encryption format specification, and memory-efficient streaming for binary/blob payloads.

2. **Inbox (`InboxMessages`)**:
   - *Status:* Documented (Partial / Conceptual)
   - *Details:* Terminal and Cloud idempotency check via `InboxMessages` (`Id`, `SourceDeviceId`, `EventType`, `ProcessedAtUtc`). Checked using `IF EXISTS` before event application.
   - *Gaps:* Cloud server-side Inbox schema is omitted from `docs/DATABASE_SCHEMA.md` and `schema_extracted/postgres_schema.sql`. Missing Inbox retention/pruning strategy on cloud PostgreSQL.

3. **Sync Services**:
   - *Status:* Conceptual Only
   - *Details:* `IHostedService` background worker running a timer loop (every 15–30 seconds) outlined in `SYSTEM_ARCHITECTURE.md` and `DECOMPILATION_GUIDE.md`.
   - *Gaps:* No C# implementation. Lacks thread synchronization between local DB writes and sync dispatch, connection pool management under high POS load, and cancellation token propagation.

4. **Sync Endpoints**:
   - *Status:* Conceptual Only
   - *Details:* Named endpoints `POST /api/v1/sync/push` (or `/api/v1/sync/batch`) and delta sync endpoint mentioned in specifications.
   - *Gaps:* No OpenAPI/Swagger specifications, missing controller actions, request payload DTO models, or HTTP error response contracts.

5. **Batch Processing**:
   - *Status:* Documented (Partial)
   - *Details:* Max batch size set to 50 items ordered by `SequenceNumber ASC`.
   - *Gaps:* No specification for handling mixed-outcome batches (e.g., items 1–10 valid, item 11 invalid, items 12–50 valid). Server either accepts all or fails all, leading to batch blocking.

6. **Retry Policies**:
   - *Status:* Missing
   - *Details:* `RetryCount` column present in `OutboxMessages`.
   - *Gaps:* Missing exponential backoff formula, maximum retry threshold (e.g., 5 retries before dead-lettering), and jitter calculations to prevent server thundering herds.

7. **Idempotency Keys**:
   - *Status:* Documented (Partial)
   - *Details:* Outbox record `Id` (Guid) serves as primary idempotency key for network dispatches.
   - *Gaps:* Lacks payload content hash matching to detect key re-use with mutated payloads (payload tampering during retries).

8. **Deduplication**:
   - *Status:* Documented (Partial)
   - *Details:* Primary key lookup in `InboxMessages` table prior to execution.
   - *Gaps:* Distributed deduplication across multi-region server nodes is unaddressed. No cache layer (Redis) specified for high-throughput idempotency lookup.

9. **Conflict Detection**:
   - *Status:* Missing
   - *Details:* Acknowledged as necessary in `ARCHITECTURE_REVIEW.md` and `PROJECT_ANALYSIS.md`.
   - *Gaps:* Zero technical mechanisms specified for detecting concurrent offline edits (e.g., no Row Versioning, no `ConcurrencyToken`, no Vector Clocks, no Last-Modified header checks).

10. **Conflict Resolution**:
    - *Status:* Missing
    - *Details:* Mentioned as "Phase 2 design" in `PROJECT_ANALYSIS.md`.
    - *Gaps:* No domain-specific resolution rules defined. The system does not specify how to handle split payments, concurrent order modifications on the same table, or stock deduction races across multiple terminals.

11. **Ordering**:
    - *Status:* Documented (Partial)
    - *Details:* Per-device `SequenceNumber` (`BIGINT`) monotonically increased. Pushed `FIFO`.
    - *Gaps:* Cross-device total ordering is completely undefined. Out-of-order execution across multi-terminal setups (e.g., Terminal A sends Payment before Terminal B sends Order Creation) will fail server foreign key constraints.

12. **Timestamps**:
    - *Status:* Documented (Partial)
    - *Details:* `CreatedAtUtc` recorded at mutation time on edge node.
    - *Gaps:* No protection against local system clock manipulation, battery drainage clock resets, or timezone skew between POS edge nodes and server.

13. **Device Identity**:
    - *Status:* Fully Documented
    - *Details:* Excellent specification in `DEVICE_MANAGEMENT.md` (Motherboard UUID + MAC + secure GUID, Ed25519/RS256 JWT device tokens, hardware drift threshold >75%).
    - *Gaps:* C# implementation of hardware fingerprinting and TPM integration is missing.

14. **Sync Checkpoints**:
    - *Status:* Missing
    - *Details:* Mentioned in delta sync concept.
    - *Gaps:* No high-water mark timestamp, `LogSequenceNumber` (LSN), or opaque cursor token defined for incremental cloud-to-terminal delta downloads.

15. **Partial Synchronization**:
    - *Status:* Missing
    - *Details:* None.
    - *Gaps:* No mechanism to pause/resume partial batch streams during connection drops, nor entity dependency tree resolution during partial payload application.

16. **Network Interruption Handling**:
    - *Status:* Conceptual Only
    - *Details:* Offline lifecycle diagram (`OFFLINE_ARCHITECTURE.md` §2) shows transition from Online to Offline state and queuing.
    - *Gaps:* HTTP client timeout settings, socket disconnect handling during active POST body transmission, and response body truncation handling are omitted.

17. **Duplicate Submissions**:
    - *Status:* Documented (Partial)
    - *Details:* Handled via Inbox ID lookup.
    - *Gaps:* Retried requests where the server successfully committed the transaction but the network dropped before the HTTP 200 response reached the client are not explicitly trace-audited.

18. **Server Acknowledgements (ACKs)**:
    - *Status:* Conceptual Only
    - *Details:* Server returns processed sequence numbers; client updates `SyncStatus = Synced`.
    - *Gaps:* ACK payload schema (JSON response body) is missing. No NACK (Negative Acknowledgement) error structure defined.

19. **Failure Recovery**:
    - *Status:* Documented (Partial)
    - *Details:* SQLite WAL mode, boot-time integrity check (`PRAGMA quick_check`), shadow database backups (`OFFLINE_ARCHITECTURE.md` §11).
    - *Gaps:* Recovery routines for stuck outboxes, corrupt payload JSON strings, or persistent cloud 500 internal errors are missing.

20. **Dead-Letter Handling (DLQ)**:
    - *Status:* Missing
    - *Details:* None.
    - *Gaps:* If an outbox message permanently fails validation (e.g., negative stock constraint on server), it will retry indefinitely or crash the sync loop without a DLQ table or admin quarantine UI.

21. **Audit Records**:
    - *Status:* Documented (Partial)
    - *Details:* `SyncOperation` aggregate specified in `DOMAIN_MODEL.md` §2.7.3.
    - *Gaps:* Server-side sync audit log table (`sync_audit_logs`) omitted from database schema specifications.

22. **SignalR Notifications**:
    - *Status:* Documented (Partial)
    - *Details:* SignalR WebSockets for cloud-to-device alerts (e.g., immediate license unlock, table status push).
    - *Gaps:* Missing reconnection backoff policy, message loss handling during WebSocket disconnection, and fallback to HTTP polling.

23. **REST APIs**:
    - *Status:* Documented (Partial)
    - *Details:* Established as HTTPS REST with JSON payloads over TLS 1.3 (`POST /api/v1/sync/batch`).
    - *Gaps:* Rate limiting headers, compression specs (Gzip/Brotli handling), and request size ceiling (e.g., max 5 MB per payload) are unmapped.

---

## 3. Multi-Device Offline Synchronizability Assessment

### 3.1 Can Multiple Offline Devices Safely Synchronize?

**Verdict: NO.** Operating multiple edge devices offline in a bar/restaurant environment using the current architectural specification poses **severe operational and data safety risks**.

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│                      MULTI-DEVICE OFFLINE SCENARIO FAILURE                       │
├──────────────────────────────────────────────────────────────────────────────────┤
│                                                                                  │
│   Terminal A (Offline)                         Terminal B (Offline)              │
│   ┌───────────────────────────┐                ┌───────────────────────────┐     │
│   │ Table 4: Add 2 Beers      │                │ Table 4: Transfer to T8   │     │
│   │ Bill #1001 Generated      │                │ Bill #1001 Generated      │     │
│   │ Outbox Seq: 101           │                │ Outbox Seq: 205           │     │
│   └─────────────┬─────────────┘                └─────────────┬─────────────┘     │
│                 │                                            │                   │
│                 └─────────────────────┬──────────────────────┘                   │
│                                       │ Network Restored                         │
│                                       ▼                                          │
│                    ┌──────────────────────────────────────┐                      │
│                    │    Cloud API Sync Coordinator        │                      │
│                    └──────────────────┬───────────────────┘                      │
│                                       │                                          │
│   ┌───────────────────────────────────┴──────────────────────────────────────┐   │
│   │                        UNRESOLVED CONFLICTS                              │   │
│   │ ❌ Primary Key Collision: Duplicate Bill #1001 issued to different tables!│   │
│   │ ❌ Out-of-Order Execution: Items added to T4 after T4 was transferred!    │   │
│   │ ❌ Stock Double Decrement: Loose ML inventory decremented twice offline.  │   │
│   └──────────────────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 Key Architectural Defects for Multi-Device Operations

1. **Lack of Vector Clocks / Distributed Logical Clocks**:
   - Outbox messages use only single-device monotonically increasing sequence numbers (`SequenceNumber`) and UTC wall-clock timestamps (`CreatedAtUtc`).
   - The system cannot establish causal relationships between events on different offline terminals (e.g., did Terminal A close Table 4 *before* or *after* Terminal B added a new KOT?).

2. **Split-Brain Table Management**:
   - Tables in dining sections (e.g., AC Hall, Garden) can be modified concurrently by multiple waiters on different mobile/tablet devices.
   - Without a single local primary master node or real-time consensus lock, offline terminals produce conflicting order items, table transfers, and table settlements.

3. **Bill Global Sequence Block Exhaustion**:
   - `ARCHITECTURE_REVIEW.md` mentions allocating bill sequence blocks (e.g., 10000–10999) to offline terminals to prevent duplicate bill numbers.
   - However, if a terminal exhausts its allocated block while offline, it must either stop billing or risk issuing duplicate GST invoice numbers, violating Indian tax regulations.

4. **Excise FL-III Stock Invariants**:
   - Maharashtra State Excise law requires continuous, exact volume tracking for liquor (bottles and loose ML pegs).
   - Deducting stock on two offline terminals simultaneously can push actual physical inventory below zero on the server ledger, corrupting the daily FL-III register (`Register 6`).

---

## 4. Comprehensive Risk Identification (11 Areas)

The following matrix identifies concrete risks, failure scenarios, architectural root causes, and recommended mitigations:

| # | Risk Area | Description & Trigger Scenario | Impact | Root Cause | Required Mitigation |
| :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | **Duplicate Transactions** | Server processes Outbox batch, commits DB, but HTTP 200 ACK drops due to network blip. Client retries batch. | **HIGH** | Lack of explicit payload hash deduplication on Cloud API. | Enforce Server Inbox check on `(SourceDeviceId, OutboxMessageId)` and return cached ACK without re-executing domain handlers. |
| 2 | **Lost Transactions** | Edge terminal SQLite database encounters disk corruption or gets wiped before Outbox is pushed to cloud. | **CRITICAL** | Storage of un-synced operational data strictly on single local drive without shadow replication. | Maintain local WAL shadow backups + immediate push retry loop upon connection detection. |
| 3 | **Out-of-Order Operations** | Terminal B pushes `BillSettled` before Terminal A pushes `KotCreated` due to thread execution races or multi-device push ordering. | **HIGH** | Absence of cross-aggregate causal ordering in Outbox queue. | Implement server-side dependency staging queue (hold `BillSettled` until `KotCreated` is received). |
| 4 | **Conflicting Edits** | Two offline terminals modify the same Order or Table status concurrently. | **HIGH** | No optimistic concurrency control (`ConcurrencyToken`) or CRDTs for offline entities. | Implement Last-Write-Wins (LWW) with explicit UI conflict review queue for financial/order entities. |
| 5 | **Partial Batches** | Outbox batch of 50 items drops connection on item 25. Server commits 25, client retries all 50. | **MEDIUM** | Monolithic batch HTTP POST without itemized transaction boundaries. | Return itemized ACK list (`[ { id: 1, status: 'ACK' }, ... ]`) allowing client to mark individual items as synced. |
| 6 | **Retry Storms** | Cloud recovers from outage. 50 POS terminals fire immediate infinite retry loops simultaneously. | **HIGH** | Lack of randomized exponential backoff and jitter in sync background worker. | Implement standard Exponential Backoff + Full Jitter (`Thread.Sleep(Min(MaxBackoff, Base * 2^attempt) + RandomJitter)`). |
| 7 | **Clock Differences** | POS terminal battery dies, system clock resets to `1970-01-01` or drifts by 15 minutes. | **HIGH** | Outbox relies on client `DateTime.UtcNow` for ordering and security token validation. | Enforce server-side timestamp assignment upon ingest + server clock skew delta calculation on edge client. |
| 8 | **Tenant Leakage** | Compromised or misconfigured POS terminal passes wrong `TenantId` in Outbox payload JSON. | **CRITICAL** | Outbox payload deserialization trusting client-provided `TenantId`. | Force Cloud API Gateway to overwrite and enforce `TenantId` extracted strictly from validated JWT claims. |
| 9 | **Branch Leakage** | Terminal at Branch A sends stock transfer mutation referencing Branch B's local storage IDs. | **HIGH** | Lack of location/branch authorization scopes in Outbox handler. | Server-side validation verifying `DeviceId` is registered to `BranchId` in `LocationDevices` master table. |
| 10 | **Data Corruption** | Malformed JSON payload or broken serialization in local SQLite `OutboxMessages.PayloadJson`. | **HIGH** | Absence of schema validation prior to Outbox enqueueing. | Strongly typed JSON schema validation before local commit + quarantining unparseable payloads. |
| 11 | **Unrecoverable Failures** | "Poison Pill" message at front of Outbox fails server domain rules (e.g., negative price). Blocks all subsequent syncs. | **CRITICAL** | Missing Dead-Letter Queue (DLQ) and max retry threshold. | Move messages to `DeadLetterMessages` table after 5 failed retries and alert cloud admin dashboard. |

---

## 5. Recommended Synchronization Lifecycle

To eliminate architectural gaps and ensure multi-device safety, the following **9-Phase End-to-End Synchronization Lifecycle** is authoritatively recommended:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                      RECOMMENDED 9-PHASE SYNC LIFECYCLE                                │
└────────────────────────────────────────────────────────────────────────────────────────┘

  [ PHASE 1: LOCAL MUTATION & OUTBOX ENQUEUE ]
  - Edge POS executes local transaction in SQLite (Order/Bill/Stock).
  - Atomically appends event payload to `OutboxMessages` with Guid `MessageId`,
    monotonically increasing `SequenceNumber`, and `TenantId`.

  [ PHASE 2: OUTBOX LEASING & BATCH SELECTION ]
  - Edge `SyncBackgroundService` queries top 50 `Pending` messages (`ORDER BY SequenceNumber ASC`).
  - Updates `SyncStatus = InFlight` with a 2-minute lease timestamp (`LeaseExpiresUtc`) to prevent concurrent dispatcher threads.

  [ PHASE 3: SECURE TRANSPORT & PUSH ]
  - Transmits payload over TLS 1.3 to `POST /api/v1/sync/push`.
  - Includes Bearer JWT (`tenant_id`, `branch_id`, `device_id`) and `X-Payload-Checksum` (SHA256).

  [ PHASE 4: CLOUD GATEWAY INGEST & AUTH ]
  - API Gateway validates JWT signature, device active status, and tenant entitlement.
  - Enforces `TenantId` and `BranchId` from JWT claim onto request context (prevents tenant leakage).

  [ PHASE 5: CLOUD INBOX IDEMPOTENCY CHECK ]
  - Cloud checks `InboxMessages` table for `(SourceDeviceId, OutboxMessageId)`.
  - If exists: Bypasses domain processing and immediately returns cached ACK payload (handles duplicate submissions).

  [ PHASE 6: CONFLICT DETECTION & RESOLUTION ]
  - Inspects entity version / timestamp.
  - Applies Domain Conflict Resolution Matrix:
    - Master Data (Prices, Menu): Server Wins.
    - Orders / Bills: Last-Write-Wins (LWW) with Audit Trail, OR Staging Queue if out-of-order.
    - Inventory Stock: Delta Aggregation (Add/Subtract relative deltas, avoid absolute overrides).

  [ PHASE 7: CLOUD ATOMIC COMMIT & ACK GENERATION ]
  - Executes PostgreSQL transaction (mutates domain aggregates + writes `InboxMessages` + writes `sync_audit_logs`).
  - Generates itemized ACK response DTO: `{ ProcessedIds: [...], FailedIds: [{ Id, Reason }] }`.

  [ PHASE 8: CLIENT ACK PROCESSING & PURGING ]
  - Edge POS receives HTTP 200 with itemized ACK.
  - Marks acknowledged IDs as `SyncStatus = Synced` and clears lease.
  - Moves failed IDs with non-transient errors (e.g., domain violation) to `LocalDeadLetterQueue`.

  [ PHASE 9: DELTA PULL CHECKPOINT SYNC ]
  - Edge POS calls `GET /api/v1/sync/delta?last_checkpoint_token=XYZ`.
  - Downloads server-side master data updates (menu price changes, new users) and applies locally inside `InboxMessages` deduplication check.
```

---

## 6. Prioritized List of Missing Tests

To prepare for implementation and ensure full verification, the following test suite must be developed:

### 6.1 Priority 1: Critical Safety & Security Tests
1. **Tenant Isolation Sync Test:** Verify that an Outbox message with a tampered `TenantId` payload is rejected by Cloud API and cannot mutate another tenant's PostgreSQL database.
2. **Multi-Device Offline Table Conflict Test:** Simulate two offline edge terminals modifying Table 4 simultaneously. Verify that sync resolves cleanly without crashing or corrupting bill data.
3. **Lost ACK Idempotency Test:** Simulate a dropped HTTP 200 ACK response. Verify that retrying the identical Outbox payload does not duplicate bills, payments, or KOT items on the server.
4. **Poison Pill & Dead-Letter Quarantine Test:** Verify that an unparseable or domain-invalid Outbox message is quarantined to DLQ after 5 retries without blocking valid subsequent outbox messages.

### 6.2 Priority 2: Reliability & Resilience Tests
5. **Network Interruption Mid-Batch Test:** Drop network connection midway through transmitting a 50-item batch. Verify itemized ACK processing and partial batch resumption upon reconnect.
6. **Clock Drift & System Time Skew Test:** Set edge POS system clock back by 3 hours. Verify that Outbox messages are assigned server ingest timestamps and synced in correct causal order.
7. **Retry Storm Exponential Backoff Test:** Simulate cloud 503 Service Unavailable. Verify that 10 mock POS terminals apply exponential backoff + jitter without flooding the server.
8. **Hardware Identity Revocation Test:** Revoke a device certificate on the cloud. Verify that subsequent `POST /api/v1/sync/push` requests return HTTP 401/403 and edge terminal enters read-only grace mode.

### 6.3 Priority 3: Data Integrity & Performance Tests
9. **High-Volume Backlog Outbox Catch-Up Test:** Queue 5,000 Outbox messages offline. Verify memory stability and performance during sequential batch processing upon reconnection.
10. **Database Power Loss Mid-Sync Recovery Test:** Terminate POS application process forcefully during active SQLite Outbox transaction. Verify SQLite WAL auto-recovery on reboot.
11. **Delta Checkpoint Continuance Test:** Verify continuous incremental download of server master data using opaque checkpoint tokens across multiple sync cycles.
