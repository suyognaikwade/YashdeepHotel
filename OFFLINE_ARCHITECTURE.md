# Offline Architecture & Resilience Specification
**System Role:** Agent 7 — Offline Architecture Specialist
**Target Platform:** .NET 9 Blazor Hybrid (MAUI Desktop/Mobile) & Edge POS Terminals
**Document Status:** Approved Architecture Standard

---

## 1. Executive Summary & Architectural Guiding Principles

The Yashdeep Hotel Management System POS operates in edge environments (restaurant floor, bar counters, billing desks) subject to intermittent connectivity, power fluctuations, and local network partitions. To guarantee zero operational downtime for critical restaurant and bar operations (table service, KOT/BOT creation, billing, printing, stock adjustments), the system follows an **Offline-First, Cloud-Synchronized Architecture**.

### 1.1 Core Guiding Principles
1. **Uninterrupted Local Operations:** Every core POS transaction (order taking, kitchen routing, billing, thermal receipt printing, stock decrement) executes entirely against local storage without any blocking remote HTTP calls.
2. **Eventual Cloud Consistency:** Local state mutations are written to a transactional local **Outbox** and asynchronously pushed to the cloud once connectivity is established.
3. **Strict Data Minimization:** The local terminal database is an **Operational Working Set**, not a replica of the cloud database. Long-term historical archives, full multi-year ledgers, and tenant-wide multi-branch analytics are never stored on edge terminals.
4. **Zero Legacy Access Schema Retention:** Legacy Microsoft Access (`dinurss.mdb`) monolithic table structures, `*_Dayend` duplicated tables, and unparameterized dynamic fields are strictly excluded. Modern domain-driven entities with SQLite and EF Core 9 are used exclusively.
5. **Security at Rest & Motion:** Local operational data is stored in an encrypted SQLite database using **SQLCipher (AES-256)**, protected by hardware-bound device keys.
6. **Robust Crash & Corruption Recovery:** The system utilizes Write-Ahead Logging (WAL) mode, transactional atomic commits, automated shadow backups, and corrupt-database recovery routines to recover gracefully from sudden power outages.

---

## 2. Offline Lifecycle Diagram

The following state machine and lifecycle diagram illustrates the operational states of an edge POS device, transition triggers, local outbox queue processing, inbox handling, and offline grace mechanisms.

```
                  ┌──────────────────────────────────────────┐
                  │            Device Boot / Setup           │
                  └────────────────────┬─────────────────────┘
                                       │ Hardware Key & Device ID Check
                                       ▼
                  ┌──────────────────────────────────────────┐
                  │       Initialize Encrypted SQLite        │
                  │   (SQLCipher, PRAGMA journal_mode=WAL)   │
                  └────────────────────┬─────────────────────┘
                                       │ Verify Offline Entitlement Cache
                                       ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│                             POS OPERATIONAL STATES                               │
│                                                                                  │
│   ┌───────────────────────────┐  Network Loss   ┌───────────────────────────┐    │
│   │        ONLINE STATE       ├────────────────►│       OFFLINE STATE       │    │
│   │  - Local DB writes        │                 │  - Local DB writes        │    │
│   │  - Outbox flush active    │◄────────────────┤  - Outbox queued          │    │
│   │  - Realtime SignalR ping  │ Connectivity    │  - Grace timer running    │    │
│   └─────────────┬─────────────┘ Restored        └─────────────┬─────────────┘    │
│                 │                                             │                  │
│                 │ Execute Local POS Transaction               │ Execute Local    │
│                 │ (KOT/BOT, Bill, Stock)                      │ Transaction      │
│                 ▼                                             ▼                  │
│   ┌─────────────────────────────────────────────────────────────────────────┐    │
│   │                     LOCAL ATOMIC TRANSACTION BOUNDARY                   │    │
│   │   1. Insert/Update Local Entity (e.g., Order, Bill, Stock)               │    │
│   │   2. Append Outbox Message (EventPayload, SequenceId, DeviceId)         │    │
│   │   3. Commit SQLite Transaction                                          │    │
│   └────────────────────────────────────┬────────────────────────────────────┘    │
│                                        │                                         │
└────────────────────────────────────────┼─────────────────────────────────────────┘
                                         │
                                         ▼
                     ┌───────────────────────────────────────┐
                     │     Direct Local ESC/POS Printing     │
                     │  (Kitchen KOT / Bar BOT / Guest Bill) │
                     └───────────────────┬───────────────────┘
                                         │
                                         ▼
                     ┌───────────────────────────────────────┐
                     │      Background Sync Engine Loop      │
                     └───────────────────┬───────────────────┘
                                         │
                      Connectivity Check │ Connected & Auth Valid
                   ┌─────────────────────┴─────────────────────┐
                   │                                           │
                   ▼                                           ▼
      [ Keep Messages Queued ]                     [ Process Outbox Queue ]
      - Verify Grace Period                        1. Read Batch (FIFO)
      - Enforce Offline Cap                        2. POST /api/v1/sync/outbox
      - Log Connectivity Loss                      3. Cloud processes Inbox
                                                   4. Server ACK / Delta Down
                                                   5. Mark Outbox Sent
```

---

## 3. Local Storage & Encrypted Database

### 3.1 Local Storage Engine
- **Database Engine:** Embedded **SQLite** via `Microsoft.EntityFrameworkCore.Sqlite` (.NET 9).
- **Pragmas & Performance Configuration:**
  - `PRAGMA journal_mode = WAL;` (Write-Ahead Logging enables concurrent read operations during write transactions).
  - `PRAGMA synchronous = NORMAL;` (Provides durability against application crashes while maximizing write throughput).
  - `PRAGMA foreign_keys = ON;` (Enforces relational integrity on local entities).
  - `PRAGMA busy_timeout = 5000;` (Prevents database locked exceptions during rapid KOT/BOT writes).
  - `PRAGMA temp_store = MEMORY;` (Accelerates temporary query sorting).

### 3.2 Database Encryption (SQLCipher / AES-256)
- **Encryption Library:** `SQLitePCLRaw.bundle_sqlcipher` / `Microsoft.Data.Sqlite` with SQLCipher.
- **Key Generation & Storage:**
  - Key derivation: PBKDF2 with 256,000 iterations.
  - The master database key is derived from a combination of:
    1. Hardware Device UUID (e.g., Windows TPM / Android KeyStore).
    2. Enclave-protected local installation secret.
- **Key Access:** Key is injected into the EF Core DbContext connection string dynamically at application boot after local PIN / Operator authentication.
  `Data Source=pos_local.db;Password=<Derived-AES256-Key>;`

---

## 4. Local Repositories, Data Access & Caching Strategy

### 4.1 Local Repository Pattern
Data access on the terminal strictly uses EF Core 9 with a dedicated local repository layer (`ILocalRepository<T>`):
- **Local DbContext (`LocalPosDbContext`):** Mapped specifically to local operational entities.
- **Query Tracking:** `AsNoTracking()` is used for read-only POS catalog queries (menu lookup, table selection) to minimize memory allocations.
- **Write Tracking:** Explicit unit-of-work boundaries wrap business operations and outbox entries in a single local transaction.

### 4.2 Multi-Tier Local Cache
To ensure instant UI rendering during high-speed billing:
1. **L1 In-Memory LFU Cache (`IMemoryCache`):**
   - Stores active table status map, operator active session, menu item lookup dictionary, tax rates, and thermal printer routing configuration.
   - Cache invalidation occurs via explicit local domain events or inbound cloud delta updates.
2. **L2 Disk Cache (Encrypted SQLite Tables):**
   - Serves as persistent backing store for L1 cache across POS application restarts.
   - Pre-loaded with operational master data (Active Menu Items, Differential Section Rates, Permit Holder records).

---

## 5. Offline Business Operations & Local Transaction Boundaries

### 5.1 Permitted Offline Business Operations
The following core domain operations run 100% offline without remote server validation:

| Operation | Local Workflows & Action | Local Artifact Produced |
| :--- | :--- | :--- |
| **Table Order & KOT Creation** | Create table seating order, add menu items, assign waiter, calculate section rates. | Local `Order`, `OrderItem`, ESC/POS KOT thermal slip. |
| **Bar BOT & Peg Dispensing** | Add liquor item (30ml, 60ml, bottle), calculate loose volume decrement, auto-open new bottle. | Local `Order`, `CounterStock` volume update, Bar BOT slip. |
| **Bill Generation & Split Tax** | Calculate subtotal, apply section discounts, compute CGST/SGST/VAT, generate UPI QR code. | Local `Bill`, ESC/POS Guest Receipt. |
| **Payment Collection** | Record cash, card, UPI (offline QR), or customer ledger credit payment. | Local `PaymentRecord`, `Bill` status set to Paid. |
| **Stock Movement** | Counter stock decrement upon sale; Godown-to-Counter transfer recording. | Local `StockAdjustment` entry. |
| **Excise Daily Log** | Record customer permit number (`ExPremiteHolder`) and dispense volume for FL-III compliance. | Local `ExciseLogEntry`. |
| **Day End Audit (Offline)** | Perform local closing audit, tally cash drawer, shift active orders to daily snapshot. | Local `DayEndSummary`. |

### 5.2 Local Transaction Boundary Pattern
Every state change on the terminal must strictly adhere to the **Atomic Local Outbox Pattern**:

```csharp
public async Task<Guid> CreateKotAsync(CreateKotCommand cmd)
{
    using var transaction = await _dbContext.Database.BeginTransactionAsync();
    try
    {
        // 1. Mutate Local Domain Entities
        var order = await _dbContext.Orders.FindAsync(cmd.OrderId);
        var kot = order.AddKot(cmd.Items, cmd.WaiterId);

        // 2. Decrement Local Counter Stock
        _stockEngine.DeductLocalStock(cmd.Items);

        // 3. Construct Outbox Message
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            DeviceId = _deviceProvider.DeviceId,
            EventType = "Order.KotCreated",
            PayloadJson = JsonSerializer.Serialize(new KotCreatedEvent(kot)),
            CreatedAtUtc = DateTime.UtcNow,
            SyncStatus = SyncStatus.Pending
        };

        _dbContext.OutboxMessages.Add(outboxMessage);

        // 4. Commit Local SQLite Transaction Atomically
        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        // 5. Trigger Immediate Local ESC/POS Thermal Printing (Non-blocking)
        _thermalPrinter.PrintKot(kot);

        return kot.Id;
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

---

## 6. Sync Protocol: Outbox & Inbox Patterns

### 6.1 Outbox Pattern (Terminal → Cloud)
- **Table Schema (`OutboxMessages`):**
  - `Id` (Guid, Primary Key)
  - `DeviceId` (Guid, Indexed)
  - `SequenceNumber` (Monotonically increasing `BIGINT` per device)
  - `EventType` (NVARCHAR(128))
  - `PayloadJson` (TEXT, Encrypted JSON payload)
  - `CreatedAtUtc` (DATETIME)
  - `RetryCount` (INT, Default 0)
  - `SyncStatus` (Enum: `Pending`=0, `InFlight`=1, `Synced`=2, `Failed`=3)
  - `LastError` (TEXT, Nullable)

- **Outbox Sync Loop:**
  - A background `IHostedService` runs every 15 seconds (or triggers immediately upon network re-establishment).
  - Fetches pending messages ordered by `SequenceNumber ASC` in batches of 50.
  - Sends batch to Cloud Endpoint: `POST /api/v1/sync/push`.
  - Cloud returns list of successfully processed `SequenceNumber` values.
  - Terminal updates `SyncStatus = Synced` or removes acknowledged messages older than retention threshold.

### 6.2 Inbox Pattern & Idempotency (Cloud & Inter-Terminal)
- **Table Schema (`InboxMessages`):**
  - `Id` (Guid, Primary Key - Cloud Message Id or Outbox Id)
  - `SourceDeviceId` (Guid)
  - `EventType` (NVARCHAR(128))
  - `ProcessedAtUtc` (DATETIME)
- **Idempotency Execution:**
  - Before processing any inbound synchronization message (e.g., price updates, master data changes, or cross-terminal table releases), the terminal checks:
    `IF EXISTS (SELECT 1 FROM InboxMessages WHERE Id = @MessageId) RETURN ALREADY_PROCESSED;`
  - Prevents duplicate application of synchronized events during network retry loops.

---

## 7. Device Identity, Entitlements & Subscription Grace

### 7.1 Hardware-Bound Device Identity
Each POS terminal generates a unique cryptographic identity at installation:
- **Device Identifiers:** Hardware fingerprints combining motherboard UUID, MAC address, and secure storage container GUID.
- **Client Certificate / JWT Token:**
  - Device registers with the cloud gateway during online onboarding.
  - Receives an X.509 Device Certificate or long-lived RS256 Device JWT signed by Cloud Authority.
  - Token embeds claims: `tenant_id`, `branch_id`, `device_id`, `device_role` (`PrimaryPOS`, `OrderTablet`, `BarTerminal`).

### 7.2 Offline Entitlement Cache & Subscription Grace Period
To prevent denial of service during network outages while enforcing SaaS licensing:
- **Offline Entitlement Cache:**
  - Signed JSON payload stored locally in encrypted key-value table `OfflineEntitlements`.
  - Contains: `TenantId`, `LicenseTier`, `AllowedModules` (POS, Bar, Excise, Reports), `ValidUntilUtc`, `MaxOfflineDays` (e.g., 14 days), `DigitalSignature`.
  - Validated offline using the Cloud Authority's public key baked into the app binary.
- **Subscription Grace Policy:**
  - **Days 0–14 (Normal Offline Operation):** Full feature set active. Visual indicator displays "Offline Mode (Days Remaining: X)".
  - **Days 15–21 (Grace Period Warning):** Prominent operational warning on POS header. Operations continue normally.
  - **Day > 21 (Hard Grace Expiry):** New billing and order creation locked. Read-only historical lookup and offline data export enabled until cloud re-authentication occurs.

---

## 8. Local Configuration Management

Local terminal settings are partitioned into **Synced Master Configuration** and **Terminal-Local Hardware Configuration**:

| Configuration Type | Storage Location | Examples | Sync Behavior |
| :--- | :--- | :--- | :--- |
| **Synced Master Config** | Local SQLite (`AppConfig` table) | Hotel Trade Name, VAT TIN, Excise License No, Tax Rates, Section Names. | Downloaded from cloud; cached locally; read-only on terminal. |
| **Terminal Local Config** | Encrypted JSON (`localsettings.json`) | ESC/POS Printer IP/COM ports, Cash Drawer trigger codes, Display scale, Local station ID. | Stored locally per device; excluded from cloud sync. |

---

## 9. Local Data Allowed List vs. Prohibited List

To preserve security, performance, and disk usage, explicit boundaries dictate what data can exist on the terminal.

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│                             LOCAL DATA ALLOWED LIST                              │
├──────────────────────────────────────────────────────────────────────────────────┤
│ 1. Active Working Master Data:                                                   │
│    - Active Menu Items, Categories, Section-wise Rates (salerate, ACRate, etc.). │
│    - Active Dining Tables & Seating Layout Map.                                  │
│    - Active Staff Accounts (hashed PINs for local login/auth).                   │
│    - Current Excise Permit Holders list (ExPremiteHolder active subset).          │
│ 2. Operational Data (Rolling 30-day Window):                                     │
│    - Active and Recent Orders (KOTs/BOTs), Bills, Payments.                      │
│    - Counter Stock & Loose Bottle Levels (CNTPACK_LIVE, CNTLOOSE_LIVE equivalent).│
│    - Current Day Excise Log & Daily Bulk Litre Summary.                          │
│ 3. Sync Infrastructural Data:                                                    │
│    - Local Outbox Queue & Local Inbox Deduplication Log.                         │
│    - Offline Entitlement Certificate & Local Settings.                           │
└──────────────────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────────────────┐
│                            LOCAL DATA PROHIBITED LIST                            │
├──────────────────────────────────────────────────────────────────────────────────┤
│ ❌ Complete Cloud Database Replicas or Multi-Tenant Data.                         │
│ ❌ Legacy Microsoft Access Schema Tables (e.g. *_Dayend duplicated tables).      │
│ ❌ Multi-Year Historical Sales Archives (Older than retention limit, e.g. 30 days)│
│ ❌ Full Multi-Branch Accounting Ledgers, P&L Balance Sheets, Capital Ledgers.    │
│ ❌ Plain-text User Credentials, Passwords, or Master Cloud API Keys.             │
│ ❌ Raw Credit Card / Bank Account details (PCI-DSS violation).                   │
└──────────────────────────────────────────────────────────────────────────────────┘
```

---

## 10. Data Retention & Data Minimization Rules

1. **Rolling Operational Window:** Edge terminals retain operational transactions (Orders, Bills, KOTs) for a **maximum of 30 days** post-synchronization.
2. **Automated Local Pruning Worker:**
   - Runs nightly during local Day-End or application idle hours.
   - Query rule:
     `DELETE FROM LocalBills WHERE CreatedAtUtc < DATE('now', '-30 days') AND SyncStatus = 'Synced';`
   - Keeps local SQLite database size compact (typically < 100 MB).
3. **Outbox Pruning:** Outbox messages with `SyncStatus = Synced` are purged after **7 days**.

---

## 11. Upgrade Behavior, Crash Recovery & Corruption Recovery

### 11.1 Schema Upgrade Behavior
- **EF Core Migrations:** Executed on boot via `dbContext.Database.Migrate()`.
- **Pre-Migration Safety Check:**
  1. Terminal checks if Outbox has unsynced messages (`SyncStatus = Pending`).
  2. If unsynced messages exist and migration contains destructive changes, app prompts/forces an outbox flush to cloud before executing migration.
  3. Creates an automatic pre-upgrade SQLite database copy (`pos_local_pre_vX.db`).

### 11.2 Crash Recovery (Power Failure / Sudden Shutdown)
- **WAL Durability:** SQLite Write-Ahead Logging automatically rolls back uncommitted dirty pages upon reopening the database.
- **Outbox Recovery:** Messages marked `InFlight` during an unexpected crash are reset to `Pending` on application boot and re-processed safely.

### 11.3 Database Corruption Recovery Routine
In the event of hardware disk corruption or forced power interrupt resulting in `SQLiteException: database disk image is malformed`:

```
                           [ Detect Corruption On Boot ]
                                         │
                                         ▼
                   ┌───────────────────────────────────────────┐
                   │    Execute PRAGMA quick_check / integrity │
                   └─────────────────────┬─────────────────────┘
                                         │ Corruption Confirmed
                                         ▼
                   ┌───────────────────────────────────────────┐
                   │   Quarantine Malformed DB to .corrupt_bak │
                   └─────────────────────┬─────────────────────┘
                                         │
                                         ▼
                   ┌───────────────────────────────────────────┐
                   │  Restore Latest Automated Local Backup    │
                   │       (pos_local_shadow_backup.db)        │
                   └─────────────────────┬─────────────────────┘
                                         │
                                         ▼
                   ┌───────────────────────────────────────────┐
                   │    Re-apply Unsynced Outbox Entries from   │
                   │     Shadow Transaction Log if available   │
                   └─────────────────────┬─────────────────────┘
                                         │
                                         ▼
                   ┌───────────────────────────────────────────┐
                   │ Request Delta Re-Sync from Cloud Gateway  │
                   │    (Download Active Tables/Menu/Stock)    │
                   └───────────────────────────────────────────┘
```

---

## 12. Backup & Recovery of Local Operational Data

1. **Automated Local Shadow Backup:**
   - Uses SQLite Online Backup API (`sqlite3_backup_init`) while the application is running.
   - Runs daily at Day-End closing.
   - Stores 3 rolling backup files locally (`pos_local_backup_1.db`, `pos_local_backup_2.db`, `pos_local_backup_3.db`).
2. **Encrypted Export for Technical Support:**
   - Emergency UI option allows operators to export an AES-256 encrypted operational diagnostic bundle to a USB drive for disaster recovery support.

---

## 13. Architectural Compliance Verification Matrix

| Requirement | Architectural Specification Section | Compliance Status |
| :--- | :--- | :--- |
| Local Storage | Section 3.1 (SQLite + WAL mode) | Verified |
| Encrypted Database | Section 3.2 (SQLCipher / AES-256) | Verified |
| Local Repositories & Cache | Section 4 (EF Core 9, L1/L2 Cache) | Verified |
| Offline Business Operations | Section 5.1 (KOT, BOT, Bill, Excise, Stock) | Verified |
| Outbox & Inbox Patterns | Section 6 (Outbox Push / Inbox Deduplication) | Verified |
| Local Transaction Boundaries | Section 5.2 (Atomic Entity + Outbox Write) | Verified |
| Device Identity | Section 7.1 (Hardware UUID + Device JWT) | Verified |
| Entitlements & Grace Period | Section 7.2 (Signed License Cache + 21-day Grace) | Verified |
| Data Allowed / Prohibited Lists | Section 9 (30-day working set, No full cloud DB) | Verified |
| No Legacy Access Schema | Section 1.1 & Section 9 (Modern clean entities) | Verified |
| Crash & Corruption Recovery | Section 11 (WAL, Shadow Restore, Integrity Check) | Verified |
| Offline Lifecycle Diagram | Section 2 (Complete State Machine Diagram) | Verified |
