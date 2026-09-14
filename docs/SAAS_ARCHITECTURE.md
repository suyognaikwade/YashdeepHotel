# Multi-Tenant SaaS Architecture & Domain Model

## 1. Architectural Overview & System Vision

This document details the multi-tenant SaaS architecture for the **Yashdeep Hotel Management System Modernization Platform**. The modernized platform transitions the legacy single-site, file-shared Microsoft Access application (`RSS.exe` / `dinurss.mdb`) into an offline-first, cloud-synchronized, multi-tenant enterprise SaaS platform built on .NET 9, Blazor Hybrid, SQLite POS edge databases, and PostgreSQL 16 cloud database.

---

## 2. Multi-Tenant Domain Hierarchy & Core Entities

```
┌────────────────────────────────────────────────────────────────────────┐
│                              Platform (SaaS)                           │
└────────────────────────────────────────────────────────────────────────┘
                                    │ 1
                                    ▼ *
┌────────────────────────────────────────────────────────────────────────┐
│                                Tenant                                  │
│  (e.g., "Yashdeep Hospitality Group", Tax ID, Primary Contact, Status) │
└────────────────────────────────────────────────────────────────────────┘
         │ 1                         │ 1                      │ 1
         ▼ *                         ▼ 1                      ▼ *
┌────────────────┐          ┌──────────────────┐     ┌──────────────────┐
│   Locations    │          │   Subscription   │     │   Feature Flags  │
│(Nanded, Bhenda)│          │ (Active, Expired)│     │  & Config Overr. │
└────────────────┘          └──────────────────┘     └──────────────────┘
    │ 1        │ 1                   │ 1
    ▼ *        ▼ *                   ▼ 1
┌────────┐  ┌────────┐      ┌──────────────────┐
│ Users  │  │ Devices│      │   Plan (Tier)    │
│(Staff) │  │ (POS)  │      │(Basic/Pro/Enterprise)
└────────┘  └────────┘      └──────────────────┘
    │ 1                          │ 1
    ▼ *                          ▼ *
┌────────┐                  ┌──────────────────┐
│ Roles  │                  │   Entitlements   │
│& Perm. │                  │(Max Locs/Devs/FL3)
└────────┘                  └──────────────────┘
```

### 2.1 Tenant
The **Tenant** is the root organizational boundary. It represents a business entity (e.g., a hotel group, restaurant owner, or franchisee) subscribing to the platform.
- **Attributes**: `TenantId` (UUID), `Name`, `LegalName`, `TaxIdentifier` (GSTIN/VAT), `PrimaryContactEmail`, `PrimaryContactPhone`, `Status` (`Trialing`, `Active`, `Suspended`, `SoftDeleted`, `HardDeleted`), `CreatedAt`, `UpdatedAt`.
- **Scope**: All operational data (menu items, orders, inventory, transactions, accounting ledgers, state excise logs) belongs strictly to a single `TenantId`.

### 2.2 Tenant Lifecycle
A tenant transitions through well-defined lifecycle states:
1. **Provisioning**: Tenant record created, schema/partition validated, default roles and admin account seeded, default configuration initialized.
2. **Trialing**: Limited period access with full or feature-capped trial tier.
3. **Active**: Fully operational under an active subscription.
4. **Grace Period**: Temporary grace status upon payment failure; warning headers issued.
5. **Suspended**: Ingress API writes blocked; POS offline sync suspended; read-only access for tenant admins via portal.
6. **Soft-Deleted**: Marked for decommission (`Status = SoftDeleted`, `DeletedAt` set). Data hidden from active queries; retained per retention policy.
7. **Hard-Deleted (Purged)**: Permanent erasure of tenant data across database, object storage, and backup retention policies.

### 2.3 Locations (Branches)
A **Location** represents a physical establishment operated by the tenant (e.g., "Yashdeep Hotel - Bhenda Branch", "Yashdeep Resort - Nanded Branch").
- **Attributes**: `LocationId` (UUID), `TenantId` (UUID), `Code`, `Name`, `Address`, `FSSAI_LicenseNumber`, `ExciseLicenseNumber` (e.g., `FL III-2151444022D8ADF7`), `TimeZone`, `IsActive`.
- **Scope**: Dining sections (Family Room, AC Hall, Garden), tables, inventory godowns, counters, and POS devices are bound to specific `LocationId`s under a `TenantId`.

### 2.4 Users
A **User** represents a staff member or administrator operating within a tenant context.
- **Attributes**: `UserId` (UUID), `TenantId` (UUID), `Email`, `PhoneNumber`, `PasswordHash`, `FullName`, `PinCodeHash` (for quick POS login), `IsActive`, `AssignedLocationIds` (List<UUID>).
- **Multi-Location Scope**: Staff can be restricted to a single location or assigned across multiple locations within the same tenant.

### 2.5 Roles & Permissions
Access control follows fine-grained Role-Based and Permission-Based Access Control (RBAC/PBAC):
- **Roles**: System roles (`PlatformAdmin`, `TenantAdmin`, `BranchManager`, `Cashier`, `Captain/Waiter`, `BarManager`, `KitchenStaff`) and Custom Tenant Roles.
- **Permissions**: Atomic permissions such as `pos.order.create`, `pos.bill.discount`, `excise.report.generate`, `inventory.godown.transfer`, `tenant.config.edit`.

### 2.6 Devices
A **Device** is a physical hardware endpoint (Windows POS terminal, Android tablet, handheld waiter terminal) registered to a location.
- **Attributes**: `DeviceId` (UUID), `TenantId` (UUID), `LocationId` (UUID), `DeviceName`, `HardwareSerialNumber`, `DeviceType` (`MainPOS`, `WaiterHandheld`, `KDS_KitchenDisplay`, `BarCounterPOS`), `ApiKeyHash`, `LastSyncedAt`, `IsActive`.
- **Offline Context**: Each POS device maintains a local SQLite database that stores data isolated to its `TenantId` and assigned `LocationId`.

### 2.7 Subscription, Plan & Entitlements
- **Plan**: Defines the service tier (`Starter`, `Professional`, `Enterprise`). Coded with quota limits (e.g., Max Locations, Max Devices, Max Users, Enable FL-III Excise Module).
- **Subscription**: The active agreement linking a `Tenant` to a `Plan`. Attributes: `SubscriptionId`, `TenantId`, `PlanId`, `BillingCycle` (`Monthly`/`Annual`), `CurrentPeriodStart`, `CurrentPeriodEnd`, `Status` (`Active`, `PastDue`, `Canceled`).
- **Entitlements**: Dynamic runtime checks computed from `Plan` limits + custom add-ons. Examples:
  - `MaxLocations` (e.g., 3 branches)
  - `MaxDevicesPerLocation` (e.g., 5 POS terminals)
  - `HasExciseCompliance` (Boolean)
  - `HasBilingualKOT` (Boolean)
  - `HasMultiGodown` (Boolean)

### 2.8 Feature Flags & Tenant-Specific Configuration
- **Feature Flags**: Dynamic feature toggles enabled globally or overridden per tenant/location (e.g., `EnableDynamicUPIQR`, `EnableKDSWebSocket`, `EnableAutoDayEndEmail`).
- **Tenant Configuration**: Structured JSON key-value overrides for operational defaults:
  - Default tax structures (CGST/SGST vs VAT)
  - Section rate multipliers (Family Room, AC Hall, VIP, Garden rates)
  - Bill header/footer custom texts and logo URLs
  - Thermal receipt printing formats (80mm / 58mm)

---

## 3. Tenant Lifecycle Management

```
┌──────────────┐     Payment Fail      ┌──────────────┐    Grace Expired   ┌──────────────┐
│  Provision   ├──────────────────────►│  Trialing /  ├───────────────────►│  Suspended   │
│              │                       │    Active    │                    │  (Read Only) │
└──────┬───────┘                       └──────┬───────┘                    └──────┬───────┘
       │                                      │                                   │
       │ Admin Action                         │ Cancel                            │ Retention Expired
       ▼                                      ▼                                   ▼
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                                     Soft Deleted                                        │
│                        (Data isolated & retained for 90 days)                           │
└────────────────────────────────────────────────────────┬────────────────────────────────┘
                                                         │
                                                         │ Purge Job
                                                         ▼
                                                ┌──────────────────┐
                                                │   Hard Deleted   │
                                                │ (Permanently Erased)
                                                └──────────────────┘
```

### 3.1 Provisioning Workflow
1. **Tenant Registration**: Ingestion of company details, initial admin account, and selected Plan.
2. **Database Provisioning**:
   - Seed `Tenant`, `Location`, default `Roles`, `Permissions`, and initial `User` in PostgreSQL cloud DB.
   - Seed default menu categories, section rate schemas, and tax setups.
3. **Device Pairing Key Generation**: Generate cryptographically signed activation tokens for edge POS device registration.

### 3.2 Suspension Procedure
1. When a subscription transitions to `Suspended` (or admin manual suspension):
   - Tenant state in Cloud DB updated to `Status = Suspended`.
   - API Ingress Gateway blocks write mutations (`POST`, `PUT`, `DELETE`) with HTTP `403 Forbidden` (`TenantSuspendedException`).
   - Edge POS devices receive sync suspension flags during background poll and switch to offline-read / local-only mode with banner alerts.
   - Tenant Admins retain portal login access restricted to subscription billing updates.

### 3.3 Deletion & Data Retention Policy
1. **Soft Deletion**:
   - Admin triggers tenant deletion -> `Status` changed to `SoftDeleted`, `DeletedAt = NOW()`.
   - All API keys, JWT session tokens, and device sync tokens instantly invalidated.
   - All tenant rows remain in database, filtered out by default global query filters (`WHERE Status != SoftDeleted`).
2. **Data Retention Window**:
   - Soft-deleted tenants enter a **90-day compliance retention window** (statutory requirement for Indian GST and State Excise records).
3. **Hard Deletion (Purge Pipeline)**:
   - Automated daily background job checks for soft-deleted tenants exceeding 90 days.
   - Purge engine executes transactional cascade deletion of tenant records across all tables (`TenantId = @tenantId`).
   - Object storage assets (receipt PDFs, logos) permanently deleted from S3-compatible cloud storage.
   - Audit event logged in platform-level system audit archive (`TenantPurgedEvent`).

---

## 4. Tenant Isolation Architecture

### 4.1 Evaluation of Isolation Strategies

| Strategy | Security Boundary | Maintenance Complexity | Cost / Resource Overhead | Suitability for POS Platform |
| :--- | :--- | :--- | :--- | :--- |
| **Database-Per-Tenant** | Hard physical DB isolation | Extremely High (1000s of DB migrations) | High (Idle DB resource wastage) | Rejected (High operational complexity for 100s of SMB restaurants) |
| **Schema-Per-Tenant** | PostgreSQL Schema separation | High (Schema management and connection pooling overhead) | Medium | Rejected |
| **Shared DB + App Filter** | Software level (`TenantId` in EF Core) | Low | Low / High Scale | High (Good developer ergonomics, but risky if filter bypassed) |
| **Shared DB + PostgreSQL RLS** | **Kernel/Database Engine Level** | Medium (Centralized SQL policies) | Low / High Scale | **SELECTED (Authoritative Architecture)** |

### 4.2 Authoritative Security Boundary: Dual-Layer Defense

To achieve defense-in-depth, the platform enforces **Dual-Layer Tenant Isolation**:
1. **Primary Defense (App Level)**: EF Core Global Query Filters automatically inject `WHERE "TenantId" = @CurrentTenantId` into every LINQ query.
2. **Authoritative Hard Defense (DB Engine Level)**: **PostgreSQL Row-Level Security (RLS)** policies enforce tenant data boundary at the database kernel level. Even if a developer writes raw SQL or forgets EF Core filters, PostgreSQL rejects cross-tenant reads or writes.

---

## 5. Security Implementation Specifications

### 5.1 API Request Tenant Context Determination

Every incoming HTTP / gRPC request is evaluated by the ASP.NET Core `TenantContextMiddleware`:

```
Incoming Request
      │
      ▼
┌────────────────────────────────────────────────────────┐
│               TenantContextMiddleware                  │
├────────────────────────────────────────────────────────┤
│ 1. Extract Bearer JWT Claim (`tenant_id`)              │
│ 2. If POS Device: Extract Header (`X-Device-Api-Key`)  │
│ 3. If Portal Admin: Extract Host Subdomain             │
│    (e.g. `yashdeep.poscloud.in` -> lookup `yashdeep`)   │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│            TenantContext Validation                    │
│ ├── Verify Tenant Exists & Status == Active           │
│ ├── Verify User/Device belongs to Resolved TenantId    │
│ └── Inject ITenantContext (Scoped DI Dependency)       │
└────────────────────────────────────────────────────────┘
```

#### ASP.NET Core Tenant Context Middleware Code Snippet

```csharp
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContextSetter tenantContextSetter, ITenantRepository tenantRepo)
    {
        Guid? tenantId = null;

        // Strategy 1: JWT Claim (User Request)
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var claim = context.User.FindFirst("tenant_id")?.Value;
            if (Guid.TryParse(claim, out var parsedClaim))
            {
                tenantId = parsedClaim;
            }
        }

        // Strategy 2: Device API Key Header (POS Sync Request)
        if (!tenantId.HasValue && context.Request.Headers.TryGetValue("X-Device-Api-Key", out var apiKey))
        {
            tenantId = await tenantRepo.GetTenantIdByDeviceKeyAsync(apiKey.ToString());
        }

        // Strategy 3: Subdomain Resolution (Tenant Admin Portal)
        if (!tenantId.HasValue)
        {
            var host = context.Request.Host.Host; // e.g. "yashdeep.hotelpos.com"
            var parts = host.Split('.');
            if (parts.Length > 2)
            {
                tenantId = await tenantRepo.GetTenantIdBySubdomainAsync(parts[0]);
            }
        }

        if (tenantId.HasValue)
        {
            var tenant = await tenantRepo.GetByIdAsync(tenantId.Value);
            if (tenant == null || tenant.Status == TenantStatus.SoftDeleted)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid or inactive tenant." });
                return;
            }

            if (tenant.Status == TenantStatus.Suspended && HttpMethods.IsPost(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Tenant account is suspended. Writes are disabled." });
                return;
            }

            tenantContextSetter.SetTenantContext(tenant.TenantId, tenant.Status);
        }

        await _next(context);
    }
}
```

### 5.2 Application-Level Isolation: EF Core Global Query Filters & Interceptor

```csharp
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

public class ApplicationDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<TableOrder> TableOrders => Set<TableOrder>();
    public DbSet<BillFinal> Bills => Set<BillFinal>();
    public DbSet<GodownStock> GodownStocks => Set<GodownStock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Dynamically apply TenantId global query filter to all ITenantEntity types
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var propertyMethodInfo = typeof(EF).GetMethod("Property")!.MakeGenericMethod(typeof(Guid));
                var tenantIdProperty = Expression.Call(propertyMethodInfo, parameter, Expression.Constant("TenantId"));
                var compareExpression = Expression.Equal(
                    tenantIdProperty,
                    Expression.Property(Expression.Constant(this), nameof(CurrentTenantId))
                );

                var lambda = Expression.Lambda(compareExpression, parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }

    public Guid CurrentTenantId => _tenantContext.TenantId;

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Enforce TenantId on newly inserted tenant entities
        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.TenantId == Guid.Empty)
                {
                    entry.Entity.TenantId = _tenantContext.TenantId;
                }
                else if (entry.Entity.TenantId != _tenantContext.TenantId)
                {
                    throw new InvalidOperationException("Cross-tenant entity insertion attempt blocked.");
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
```

### 5.3 Database Engine Level Isolation: PostgreSQL Row-Level Security (RLS)

To enforce RLS, every database connection opened by EF Core sets a session variable `app.current_tenant_id`.

#### EF Core DbConnection Interceptor Snippet

```csharp
public class NpgsqlTenantInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenantContext;

    public NpgsqlTenantInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.HasTenant)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SET LOCAL app.current_tenant_id = '{_tenantContext.TenantId}';";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}
```

#### PostgreSQL RLS DDL Setup SQL Script

```sql
-- 1. Create Application Database User Role
CREATE ROLE app_user WITH LOGIN PASSWORD 'SecureAppPassword123!';

-- 2. Enable Row-Level Security on All Tenant Tables
ALTER TABLE "TableOrders" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "BillFinal" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "GodownStock" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "KOTDetail" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "ExciseMonthlyStat" ENABLE ROW LEVEL SECURITY;

-- 3. Create RLS Policies for Tenant Data Isolation
CREATE POLICY tenant_isolation_policy ON "TableOrders"
    FOR ALL
    TO app_user
    USING ("TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
    WITH CHECK ("TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);

CREATE POLICY tenant_isolation_policy ON "BillFinal"
    FOR ALL
    TO app_user
    USING ("TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
    WITH CHECK ("TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);

CREATE POLICY tenant_isolation_policy ON "GodownStock"
    FOR ALL
    TO app_user
    USING ("TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
    WITH CHECK ("TenantId" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);

-- 4. Admin Bypass Policy (For System Level Analytics Jobs Only)
CREATE ROLE platform_admin_user WITH LOGIN SUPERUSER PASSWORD 'AdminSecurePassword123!';
-- Superusers bypass RLS policies by default in PostgreSQL.
```

---

## 6. Multi-Location Behavior & Offline Edge Isolation

### 6.1 Multi-Location Data Visibility
- **Branch-Level Scope**: POS devices and staff users assigned to Location A (`LocationId_A`) query data filtered by `TenantId` AND `LocationId_A`.
- **Tenant-Level Scope**: Tenant administrators, accountants, and inventory managers possess cross-location visibility (`LocationId IS NULL` or explicit list of assigned location IDs) to view aggregated revenue, perform stock transfers between branches, and consolidate State Excise reports.

### 6.2 Offline Edge Device Isolation (SQLite)
Every local POS terminal runs an embedded SQLite database (`local_pos.db3`).
- **Edge Data Caching**: The local SQLite database stores data strictly filtered to its assigned `TenantId` and `LocationId`.
- **Sync Boundary Verification**:
  When the edge Outbox Sync client sends local mutations to Cloud API (`/api/v1/sync/push`), the Cloud API verifies that:
  1. The authenticated device API key matches the payload `TenantId` and `LocationId`.
  2. All mutated entity `TenantId` properties match the authenticated device context.
- **Cross-Tenant Access Prevention on Edge**: A stolen or compromised edge device contains zero data belonging to other tenants or other locations.

---

## 7. Operational & Migration Framework

### 7.1 Tenant Migration Pipeline (Legacy Access -> Multi-Tenant Cloud)
When onboarding legacy single-site customers operating `dinurss.mdb`:
1. **Extraction**: `extract_schema.sh` / `extract_schema.ps1` extracts legacy Jet 4.0 tables.
2. **Transform**: The migration CLI app maps legacy records to modern domain models:
   - Assigns a newly generated `TenantId` and default `LocationId` to every record.
   - Converts legacy passwords (`333`/`admin`) to salted PBKDF2/Bcrypt hashes.
   - Maps legacy item prices (`FAMILYRATE`, `ACRATE`, `VIPRATE`) into modern `SectionPrices`.
   - Normalizes multi-tier liquor stock records (`GodownStock`, `CNTPACK_LIVE`, `CNTLOOSE_LIVE`).
3. **Load**: Transactional bulk upload into PostgreSQL under RLS context with validation checks.

### 7.2 Summary Architecture Verification Checklist

| Architectural Requirement | Verification Mechanism |
| :--- | :--- |
| **Tenant Determination** | `TenantContextMiddleware` extracts JWT claim `tenant_id`, `X-Device-Api-Key`, or subdomain. |
| **App-Level Isolation** | EF Core `HasQueryFilter` + `SaveChangesAsync` `TenantId` validation guard. |
| **DB-Level Isolation** | PostgreSQL Row-Level Security (`ENABLE ROW LEVEL SECURITY`) with `app.current_tenant_id`. |
| **Suspension Enforcement** | Write requests blocked with HTTP 403; POS edge sync suspended. |
| **Data Retention** | 90-day soft deletion grace window prior to automated purge worker. |
| **Multi-Location** | Granular `LocationId` context filters combined with tenant-wide admin override roles. |
| **Offline Isolation** | SQLite edge database containing only local location & tenant datasets. |
