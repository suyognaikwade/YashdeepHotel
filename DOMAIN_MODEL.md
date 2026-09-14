# Yashdeep Hotel Management System — Canonical Domain Model

## Executive Summary & Design Principles

This document specifies the canonical domain model for the modern cloud-synchronized multi-tenant SaaS platform of the **Yashdeep Hotel Management System**.

Designed for Indian hospitality businesses with integrated dining, takeaway (parcel), multi-tier bar dispensing, and Maharashtra State Excise (FL-III) regulatory compliance, this domain model replaces the legacy monolithic Microsoft Access (`dinurss.mdb`) schema. It adheres strictly to **Domain-Driven Design (DDD)** principles and supports an offline-first architecture powered by edge POS terminals (SQLite + EF Core) synchronized with a central cloud platform (PostgreSQL 16 + ASP.NET Core 9 Web API).

### Key Architectural Principles
1. **Multi-Tenancy & Isolation**: Every domain entity (with the exception of global Subscription Plans and System Entitlements) is owned by a `TenantId`.
2. **Offline-First Resilience**: Transactions created at local POS terminals are stored in local SQLite databases with unique UUID primary keys (`Guid`) and synchronized to the cloud via an Outbox Pattern.
3. **Explicit Aggregate Boundaries**: Aggregates enforce consistency invariants. Entitlements and business rules prevent invalid cross-aggregate modifications.
4. **State Machine Lifecycles**: Domain concepts such as `Order`, `Bill`, `KOT`, `Bottle`, and `DayEnd` follow explicit, deterministic lifecycle states.
5. **Auditing & Traceability**: Immutable audit logs and financial ledgers record all structural changes, cancellations, discounts, and date locks.

---

## 1. Domain Bounded Contexts & Aggregate Summary

The domain is partitioned into 7 primary **Bounded Contexts**:

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                                 CANONICAL DOMAIN MODEL                                  │
├───────────────────┬───────────────────┬───────────────────┬─────────────────────────────┤
│ Tenant & SaaS     │ Identity & RBAC   │ Dining Floor &    │ Menu & Pricing              │
│ Administration    │ Context           │ Layout Context    │ Catalog Context             │
│ - Tenant (AR)     │ - User (AR)       │ - Location (AR)   │ - Menu (AR)                 │
│ - Plan (AR)       │ - Role (AR)       │ - Section (AR)    │ - Category (VO)             │
│ - Subscription    │ - Permission (VO) │ - Table (Entity)  │ - MenuItem (Entity)         │
│ - Entitlement     │ - Device (Entity) │                   │ - SectionPrice (VO)         │
├───────────────────┼───────────────────┼───────────────────┼─────────────────────────────┤
│ Order & Billing   │ Stock & Inventory │ Excise Compliance │ System Audit & Sync         │
│ Context           │ Context           │ Context           │ Context                     │
│ - Order (AR)      │ - InventoryItem(AR│ - ExciseRegister  │ - DayEnd (AR)               │
│ - OrderItem (Ent) │ - StockLocation(AR│   (AR)            │ - BusinessDate (VO)         │
│ - KOT / BOT (Ent) │ - StockMovement   │ - PermitHolder    │ - AuditEvent (AR)           │
│ - Bill (AR)       │ - Purchase (AR)   │   (Entity)        │ - SyncOperation (AR)        │
│ - Payment (Entity)│ - Transfer (AR)   │ - ExciseTxn (Ent) │ - OutboxMessage (AR)        │
│                   │ - Adjustment (AR) │                   │                             │
│                   │ - Bottle (Entity) │                   │                             │
│                   │ - LooseStock (Ent)│                   │                             │
└───────────────────┴───────────────────┴───────────────────┴─────────────────────────────┘
*(AR = Aggregate Root, Entity = Domain Entity, VO = Value Object)*
```

---

## 2. Core Domain Entity & Aggregate Specifications

### 2.1 Context 1: Tenant, Subscription & SaaS Platform Management

#### 2.1.1 Tenant Aggregate Root
* **Description**: Represents an independent business customer operating one or more hotel/restaurant locations.
* **Fields**:
  * `TenantId` (Guid, PK)
  * `LegalName` (string)
  * `TradeName` (string, e.g., "HOTEL YASHDEEP")
  * `GSTIN` (string, e.g., "27900111779v")
  * `ExciseLicenseNumber` (string, e.g., "FL III-2151444022D8ADF7")
  * `ContactEmail` (string)
  * `ContactPhone` (string)
  * `Status` (TenantStatus Enum: `Active`, `Suspended`, `Terminated`)
  * `CreatedAtUtc` (DateTime)
  * `UpdatedAtUtc` (DateTime)
* **Ownership**: Root of all tenant-bound data.
* **Lifecycle**: `Pending` -> `Active` <-> `Suspended` -> `Terminated`.

#### 2.1.2 Plan Aggregate Root & Entitlement Value Object
* **Description**: Global SaaS subscription plans and specific capability permissions.
* **Plan Fields**: `PlanId` (Guid, PK), `Code` (string), `Name` (string), `MaxLocations` (int), `MaxDevicesPerLocation` (int), `IsExciseModuleEnabled` (bool), `MonthlyPrice` (decimal).
* **Entitlement Fields (Value Object)**: `FeatureKey` (string, e.g., `Excise.FL3Register`, `Offline.SQLiteSync`), `LimitValue` (string/int).

#### 2.1.3 Subscription Aggregate Root
* **Description**: Links a Tenant to a SaaS Plan.
* **Fields**: `SubscriptionId` (Guid, PK), `TenantId` (Guid, FK), `PlanId` (Guid, FK), `StartDate` (DateTime), `EndDate` (DateTime), `BillingCycle` (Enum: `Monthly`, `Annual`), `Status` (Enum: `Trialing`, `Active`, `PastDue`, `Cancelled`).

#### 2.1.4 Device Entity
* **Description**: A physical hardware terminal (POS Touchscreen PC, Android Tablet, Waiter Handheld) authorized to access a location.
* **Fields**: `DeviceId` (Guid, PK), `TenantId` (Guid, FK), `LocationId` (Guid, FK), `DeviceCode` (string), `HardwareFingerprint` (string / Motherboard CPU ID), `DeviceName` (string), `IsActive` (bool), `LastSyncTimestamp` (DateTime?).

---

### 2.2 Context 2: Identity & Access Control (RBAC)

#### 2.2.1 User Aggregate Root
* **Description**: Represents staff members (Managers, Cashiers, Waiters/Captains, Kitchen Staff).
* **Fields**: `UserId` (Guid, PK), `TenantId` (Guid, FK), `Username` (string), `PasswordHash` (string), `FullName` (string), `PinCode` (string, for fast numeric POS waiter login), `IsActive` (bool), `Roles` (Collection of UserRole references).

#### 2.2.2 Role Aggregate Root & Permission Value Object
* **Description**: Security roles and granular permissions.
* **Role Fields**: `RoleId` (Guid, PK), `TenantId` (Guid, FK), `RoleName` (string, e.g., `Admin`, `Manager`, `Cashier`, `Captain`).
* **Permission Fields**: `PermissionId` (Guid, PK), `Scope` (string, e.g., `Bill.Void`, `Discount.Apply`, `DayEnd.Execute`, `DateLock.Bypass`).

---

### 2.3 Context 3: Location, Floor & Section Management

#### 2.3.1 Location Aggregate Root
* **Description**: Physical hotel branch or establishment.
* **Fields**: `LocationId` (Guid, PK), `TenantId` (Guid, FK), `Name` (string), `Address` (Address VO), `TaxConfiguration` (TaxConfig VO: CGST %, SGST %, VAT %).

#### 2.3.2 Section Aggregate Root
* **Description**: Dining zones with specific pricing structures and service charges.
* **Fields**: `SectionId` (Guid, PK), `TenantId` (Guid, FK), `LocationId` (Guid, FK), `Code` (string: `FAMILY`, `AC`, `HALL`, `RESTAURANT`, `GARDEN`, `PARCEL`), `Name` (string), `PricingTier` (Enum: `BaseRate`, `FamilyRate`, `VipRate`, `AcRate`, `WholesaleRate`), `ServiceChargePercentage` (decimal).

#### 2.3.3 Table Entity
* **Description**: Physical seating unit within a section.
* **Fields**: `TableId` (Guid, PK), `TenantId` (Guid, FK), `SectionId` (Guid, FK), `TableNumber` (string, e.g., "T-01", "A-02"), `SeatingCapacity` (int), `Status` (TableStatus Enum: `Vacant`, `Occupied`, `Billed`, `Reserved`, `Merged`, `Blocked`), `ActiveOrderId` (Guid?, FK referencing current open Order).

---

### 2.4 Context 4: Menu Catalog & Pricing

#### 2.4.1 Menu Aggregate Root
* **Description**: Complete catalog of food, beverage, and liquor items available for sale.
* **Fields**: `MenuId` (Guid, PK), `TenantId` (Guid, FK), `LocationId` (Guid, FK), `Version` (int), `IsActive` (bool).

#### 2.4.2 MenuItem Entity & SectionPrice Value Objects
* **Description**: Individual dish or beverage product code.
* **Fields**:
  * `MenuItemId` (Guid, PK)
  * `TenantId` (Guid, FK)
  * `ShortCode` (string, e.g., "101" for fast POS keyboard entry)
  * `EnglishName` (string, e.g., "Chicken Tikka")
  * `MarathiName` (string, e.g., "चिकन टिक्का" for Kitchen KOT slip printing)
  * `Department` (Enum: `Kitchen`, `Bar`, `Beverage`)
  * `Category` (string, e.g., "IMFL", "CountryLiquor", "Tandoor", "MainCourse")
  * `BrandName` (string?, for liquor items)
  * `UnitVolumeMl` (int?, e.g., 30, 60, 90, 180, 375, 750, 1000)
  * `BottlesPerCase` (int?, standard case packing size)
  * `BasePrice` (decimal)
  * `SectionPrices` (Dictionary of `SectionId` -> `Price` overrides)
  * `IsExciseTracked` (bool)
  * `IsActive` (bool)

---

### 2.5 Context 5: Order, KOT/BOT & Billing (Core Operational Transaction Context)

#### 2.5.1 Order Aggregate Root
* **Description**: Represents a customer dining transaction from seating to bill payment or cancellation.
* **Fields**:
  * `OrderId` (Guid, PK)
  * `TenantId` (Guid, FK)
  * `LocationId` (Guid, FK)
  * `TableId` (Guid, FK)
  * `BusinessDate` (DateOnly)
  * `OrderNumber` (string, daily sequential order number)
  * `OrderType` (Enum: `DineIn`, `TakeawayParcel`, `Delivery`)
  * `Status` (OrderStatus Enum: `Open`, `Billed`, `Completed`, `Cancelled`, `Merged`)
  * `CaptainUserId` (Guid, FK)
  * `SeatedAt` (DateTime)
  * `ClosedAt` (DateTime?)
  * `OrderItems` (List of `OrderItem` Entities)
  * `Kots` (List of `KOT` Entities)

#### 2.5.2 OrderItem Entity
* **Description**: A line item ordered by guests.
* **Fields**:
  * `OrderItemId` (Guid, PK)
  * `OrderId` (Guid, FK)
  * `MenuItemId` (Guid, FK)
  * `ItemName` (string)
  * `MarathiName` (string)
  * `Department` (Enum: `Kitchen`, `Bar`, `Beverage`)
  * `Quantity` (decimal)
  * `UnitVolumeMl` (int?)
  * `UnitPrice` (decimal)
  * `SubTotal` (decimal = `Quantity * UnitPrice`)
  * `Status` (OrderItemStatus Enum: `PendingKOT`, `SentToKitchen`, `Served`, `Cancelled`)
  * `CancelledReason` (string?)
  * `CancelledByUserId` (Guid?, FK)

#### 2.5.3 KOT (Kitchen Order Ticket) & BOT (Bar Order Ticket) Entities
* **Description**: Physical routing tickets generated for kitchen cooks or bar tenders.
* **Fields**:
  * `KotId` (Guid, PK)
  * `OrderId` (Guid, FK)
  * `KotNumber` (string, e.g., "KOT-20260330-0042")
  * `TicketType` (Enum: `KOT_Kitchen`, `BOT_Bar`)
  * `TargetPrinterName` (string)
  * `PrintedAt` (DateTime)
  * `Status` (Enum: `Printed`, `Reprinted`, `Voided`)
  * `LineItems` (Snapshot of ordered items sent in this ticket batch)

#### 2.5.4 Bill Aggregate Root
* **Description**: Financial customer guest check invoice generated from an Order.
* **Fields**:
  * `BillId` (Guid, PK)
  * `TenantId` (Guid, FK)
  * `LocationId` (Guid, FK)
  * `OrderId` (Guid, FK)
  * `BusinessDate` (DateOnly)
  * `BillNumber` (long, global sequence number)
  * `BillDailyNumber` (int, daily sequence counter reset at Day End)
  * `TableId` (Guid, FK)
  * `WaiterUserId` (Guid, FK)
  * `CashierUserId` (Guid, FK)
  * `SubTotalAmount` (decimal)
  * `FoodSubTotal` (decimal)
  * `LiquorSubTotal` (decimal)
  * `DiscountAmount` (decimal)
  * `DiscountPercentage` (decimal)
  * `ServiceChargeAmount` (decimal)
  * `CGSTAmount` (decimal)
  * `SGSTAmount` (decimal)
  * `VATAmount` (decimal)
  * `RoundOffAmount` (decimal)
  * `GrandTotal` (decimal)
  * `PaymentStatus` (PaymentStatus Enum: `Unpaid`, `PartiallyPaid`, `Paid`, `SettledToCredit`)
  * `IsVoided` (bool)
  * `VoidedReason` (string?)
  * `CreatedAt` (DateTime)
  * `Payments` (List of `Payment` Entities)

#### 2.5.5 Payment Entity
* **Description**: Financial settlement transaction applied to a Bill.
* **Fields**:
  * `PaymentId` (Guid, PK)
  * `BillId` (Guid, FK)
  * `TenantId` (Guid, FK)
  * `PaymentMethod` (PaymentMethod Enum: `Cash`, `UPI_QR`, `Card`, `RoomCredit`, `CustomerLedger`)
  * `Amount` (decimal)
  * `TransactionReference` (string?, UPI Transaction Reference / Card Approval Code)
  * `CustomerLedgerAccountId` (Guid?, FK for credit customers)
  * `PaidAt` (DateTime)

---

### 2.6 Context 6: Multi-Tier Stock, Inventory & Excise Management

#### 2.6.1 StockLocation Aggregate Root
* **Description**: Physical areas within a location where inventory is stored or dispensed.
* **Fields**: `StockLocationId` (Guid, PK), `TenantId` (Guid, FK), `LocationId` (Guid, FK), `Name` (string), `Type` (StockLocationType Enum: `GodownWarehouse`, `CounterBar`, `KitchenStore`).

#### 2.6.2 InventoryItem Aggregate Root
* **Description**: Master stock tracking item linked to Menu Items or Raw Ingredients.
* **Fields**: `InventoryItemId` (Guid, PK), `TenantId` (Guid, FK), `SKU` (string), `Name` (string), `Category` (string), `IsLiquor` (bool), `PackSizeMl` (int), `MinimumReorderLevel` (decimal).

#### 2.6.3 Bottle & LooseStock Entities (Bar Dispensary Tier)
* **Description**: Tracks liquor across sealed bottles and opened bottles (loose ML).
* **Bottle Fields**: `BottleId` (Guid, PK), `InventoryItemId` (Guid, FK), `StockLocationId` (Guid, FK), `Status` (BottleStatus Enum: `SealedInGodown`, `TransferredToCounter`, `OpenedForDispensing`, `EmptyFinished`, `BrokenSpilled`).
* **LooseStock Fields**: `LooseStockId` (Guid, PK), `StockLocationId` (Guid, FK), `InventoryItemId` (Guid, FK), `CurrentlyOpenBottleId` (Guid, FK), `RemainingVolumeMl` (decimal), `TotalMlDispensedToday` (decimal).

#### 2.6.4 Purchase Aggregate Root
* **Description**: Inward stock purchase invoice received from licensed distributors/vendors.
* **Fields**: `PurchaseId` (Guid, PK), `TenantId` (Guid, FK), `VendorAccountId` (Guid, FK), `InvoiceNumber` (string), `TransportPermitNumber` (string, mandatory for liquor inward), `InvoiceDate` (DateOnly), `TotalAmount` (decimal), `LineItems` (List of PurchaseItems).

#### 2.6.5 Transfer & Adjustment Aggregate Roots
* **Description**: Inter-location/store movements and inventory reconciliation adjustments.
* **Transfer Fields**: `TransferId` (Guid, PK), `SourceLocationId` (Guid, FK), `DestinationLocationId` (Guid, FK), `TransferredAt` (DateTime), `Items` (Collection).
* **Adjustment Fields**: `AdjustmentId` (Guid, PK), `StockLocationId` (Guid, FK), `Reason` (Enum: `Breakage`, `Spillage`, `AuditDiscrepancy`, `ExciseWastage`), `VolumeAdjustedMl` (decimal), `ApprovedByUserId` (Guid, FK).

#### 2.6.6 ExciseTransaction Entity & PermitHolder Entity (Maharashtra FL-III Context)
* **Description**: Mandatory legal registers for Maharashtra State Excise compliance.
* **PermitHolder Fields**: `PermitHolderId` (Guid, PK), `TenantId` (Guid, FK), `PermitNumber` (string), `HolderName` (string), `ExpiryDate` (DateOnly), `Address` (string).
* **ExciseTransaction Fields**: `ExciseTxnId` (Guid, PK), `TenantId` (Guid, FK), `BusinessDate` (DateOnly), `PermitHolderId` (Guid?, FK), `BrandName` (string), `Category` (Enum: `IMFL`, `CountryLiquor`, `Wine`, `Beer`), `VolumeDispensedMl` (decimal), `BulkLiters` (decimal = `VolumeDispensedMl / 1000`), `RegisterType` (Enum: `Register1_DailySales`, `FormFLR3`).

---

### 2.7 Context 7: Day End Settlement, System Audit & Offline Sync

#### 2.7.1 Business Date Value Object & DayEnd Aggregate Root
* **Description**: Manages hospitality business date boundaries (which extend past midnight) and daily financial closures.
* **DayEnd Fields**:
  * `DayEndId` (Guid, PK)
  * `TenantId` (Guid, FK)
  * `LocationId` (Guid, FK)
  * `BusinessDate` (DateOnly)
  * `ClosedAtTimestamp` (DateTime)
  * `ExecutedByUserId` (Guid, FK)
  * `TotalBillsCount` (int)
  * `TotalGrossSales` (decimal)
  * `TotalDiscount` (decimal)
  * `TotalTax` (decimal)
  * `TotalCashReceived` (decimal)
  * `TotalUpiReceived` (decimal)
  * `TotalCardReceived` (decimal)
  * `TotalCreditSales` (decimal)
  * `Status` (DayEndStatus Enum: `InProgress`, `Completed`, `Failed`)
  * `AutomatedEmailSent` (bool)

#### 2.7.2 AuditEvent Aggregate Root
* **Description**: Immutable append-only record of security and operational events.
* **Fields**: `AuditEventId` (Guid, PK), `TenantId` (Guid, FK), `UserId` (Guid?, FK), `DeviceId` (Guid?, FK), `EventType` (string, e.g., `Bill.Voided`, `KOT.Cancelled`, `Price.Overridden`, `DateLock.MasterBypassed`), `PayloadJson` (string), `IpAddress` (string), `TimestampUtc` (DateTime).

#### 2.7.3 SyncOperation Aggregate Root & OutboxMessage Entity
* **Description**: Handles offline SQLite edge node mutation tracking and cloud synchronization.
* **Fields**:
  * `OutboxMessageId` (Guid, PK)
  * `TenantId` (Guid, FK)
  * `DeviceId` (Guid, FK)
  * `AggregateType` (string, e.g., "Order", "Bill", "StockMovement")
  * `AggregateId` (Guid)
  * `Operation` (Enum: `Create`, `Update`, `Delete`)
  * `PayloadJson` (string)
  * `CreatedAt` (DateTime)
  * `ProcessedAt` (DateTime?)
  * `SyncStatus` (Enum: `Pending`, `Uploaded`, `ConflictResolved`, `Failed`)
  * `RetryCount` (int)
  * `ErrorMessage` (string?)

---

## 3. Aggregate Boundaries & Ownership Model

### 3.1 Ownership Hierarchy

```
Tenant (Root SaaS Boundary)
 ├── Subscription
 ├── User & Role (RBAC)
 └── Location (Physical Establishment)
      ├── Device (POS Terminals)
      ├── Section -> Table
      ├── Menu -> MenuItem -> SectionPrice
      ├── StockLocation (Godown / Counter Bar) -> InventoryItem / Bottle / LooseStock
      ├── Order -> OrderItem -> KOT / BOT
      ├── Bill -> Payment
      ├── ExciseRegister -> PermitHolder / ExciseTransaction
      └── DayEnd (Business Date Closing Record)
```

### 3.2 Key DDD Rules for Aggregate Boundaries
1. **Reference Across Aggregates by Primary Key**: Entities in one aggregate (e.g., `Order`) reference entities in another aggregate (e.g., `MenuItem` or `Table`) strictly by their `Guid` identifiers, never via direct object reference navigation properties.
2. **Transactional Consistency Within Boundaries**: Any mutation inside an Aggregate (e.g., adding an `OrderItem` to an `Order` or adding a `Payment` to a `Bill`) must be committed atomically in a single transaction.
3. **Eventual Consistency Between Aggregates**: Cross-aggregate workflows (e.g., an `Order` completing and triggering an `InventoryItem` stock deduction and an `ExciseTransaction` entry) occur via Domain Events (`OrderCompletedEvent`) handled asynchronously or within application services.

---

## 4. Lifecycles & State Machine Transitions

### 4.1 Order Lifecycle

```
       [ Seated / Created ]
                │
                ▼
            ┌────────┐
            │  Open  │ ◄─────── (Add items / KOTs)
            └───┬────┘
                │
         (Print Guest Check)
                │
                ▼
           ┌──────────┐
           │  Billed  │
           └────┬─────┘
                │
        (Payment Received)
                │
                ▼
          ┌───────────┐
          │ Completed │ (Terminal State)
          └───────────┘
```
* **Cancellation Flow**: An `Open` or `Billed` order can transition to `Cancelled` only if authorized by a Manager role with an entry written to `AuditEvent` and `CancelKot`.

### 4.2 Bill & Payment Lifecycle

```
[ Order Billed ] ──► PaymentStatus: Unpaid
                           │
                 (Apply Cash/UPI/Card Payment)
                           │
                           ▼
          ┌──────────────────────────────────┐
          │ AmountPaid == Bill.GrandTotal ?  │
          └────────────────┬─────────────────┘
                           │
             ┌─────────────┴─────────────┐
          (Yes)                         (No)
             │                           │
             ▼                           ▼
  [ PaymentStatus: Paid ]   [ PaymentStatus: PartiallyPaid / Credit ]
```

### 4.3 Liquor Bottle & ML Dispensing Lifecycle

```
[ Inward Purchase ] ──► BottleStatus: SealedInGodown
                                 │
                     (Transfer to Counter Bar)
                                 │
                                 ▼
                    BottleStatus: TransferredToCounter
                                 │
                         (Open Bottle for Pegs)
                                 │
                                 ▼
                    BottleStatus: OpenedForDispensing
                    (LooseStock.RemainingVolumeMl = 750ml)
                                 │
                       (Dispense 30/60/90/180ml Pegs)
                                 │
                                 ▼
                    (RemainingVolumeMl reaches 0ml)
                                 │
                                 ▼
                    BottleStatus: EmptyFinished
                    (Auto-opens next sealed bottle from Counter)
```

### 4.4 Business Date & Day End Lifecycle

```
[ Active Business Date (e.g. 2026-03-30) ]
                    │
            (Initiate Day End)
                    │
                    ▼
[ Check Open Tables / Unbilled Orders ]
                    │
          ┌─────────┴─────────┐
       (Open Orders Found)  (No Open Orders)
          │                   │
          ▼                   ▼
    [ ABORT DAY END ]   [ Lock Date & Execute Day End ]
                              │
                              ├── 1. Snapshot Daily Totals
                              ├── 2. Rollover Stock Closing -> Tomorrow Opening
                              ├── 3. Reset BillDailyNumber = 1, KotNumber = 1
                              ├── 4. Advance BusinessDate -> 2026-03-31
                              └── 5. Dispatch Automated Sales Email
```

---

## 5. Domain Invariants & Business Rules

1. **Multi-Tenant Isolation Invariant**: No database query or mutation at edge or cloud may read or modify data belonging to a different `TenantId`.
2. **Table Seating Invariant**: A table in state `Occupied` can have only ONE active `Order` in state `Open`.
3. **KOT Printing Invariant**: A `KOT` or `BOT` cannot be generated for zero-quantity or invalid menu items. Once printed, line items can only be modified via explicit `CancelKot` records with supervisor approval.
4. **Bilingual Menu Invariant**: Every `MenuItem` assigned to the `Kitchen` department MUST have both `EnglishName` and `MarathiName` populated for kitchen slip clarity.
5. **Differential Pricing Invariant**: When an item is added to an `Order`, the unit rate applied MUST match the `PricingTier` configured for the table's `Section` (e.g., `ACRate` for AC Section, `FamilyRate` for Family Hall).
6. **Bill Calculation Invariant**:
   $$\text{GrandTotal} = \text{SubTotal} - \text{Discount} + \text{ServiceCharge} + \text{CGST} + \text{SGST} + \text{VAT} + \text{RoundOff}$$
   The calculated `GrandTotal` must exactly equal the sum of associated `Payment` amounts before a Bill transitions to `Paid`.
7. **Liquor Stock Non-Negativity Invariant**: Loose stock dispensing cannot reduce `LooseStock.RemainingVolumeMl` below 0ml without automatically triggering an auto-uncork of 1 sealed bottle from `Counter` stock. If `Counter` sealed bottle stock is 0, dispensing is blocked.
8. **Day End Block Invariant**: Day End execution MUST be rejected if there are any active tables with `OrderStatus == Open` or `PaymentStatus == Unpaid`.
9. **Historical Date Lock Invariant**: Transactions belonging to a closed `BusinessDate` cannot be edited or voided unless a Manager authorization bypass is logged in `AuditEvent`.

---

## 6. Transactional Boundaries & ACID Guarantees

The following operational commands **MUST** execute within single, strict database ACID transactions (whether in local edge SQLite or cloud PostgreSQL):

| Operational Transaction | Entities Involved | Transactional Requirements |
| :--- | :--- | :--- |
| **1. Create KOT / Add Order Items** | `Order`, `OrderItem`, `KOT`, `Table` | Atomically append items, set table status to `Occupied`, increment KOT sequence, and write KOT ticket. |
| **2. Cancel KOT Item** | `OrderItem`, `CancelKot`, `AuditEvent` | Atomically mark item cancelled, log cancellation reason/waiter, and update order subtotal. |
| **3. Finalize Bill** | `Order`, `Bill`, `Table` | Atomically freeze order items, compute taxes/discounts, generate sequential `BillNumber`, and update table state to `Billed`. |
| **4. Process Bill Payment** | `Bill`, `Payment`, `CustomerLedgerAccount` | Atomically insert payment entry, update bill `PaymentStatus`, adjust customer credit balance (if credit), and free table to `Vacant` if paid in full. |
| **5. Dispense Liquor Peg (BOT)** | `OrderItem`, `LooseStock`, `Bottle`, `ExciseTransaction` | Atomically deduct ML volume, trigger sealed bottle decrement if ML hits 0, and write excise statement entry. |
| **6. Execute Day End Settlement** | `DayEnd`, `BusinessDate`, `CNTPACK_LIVE`, `GodownStock` | Atomically verify zero open tables, calculate daily totals, rollover stock balances, increment business date, and reset daily sequence counters. |
| **7. Process Outbox Sync Batch** | `OutboxMessage`, Sync Engine Entities | Atomically mark uploaded mutations as `ProcessedAt` on edge node upon receipt of cloud ACK. |

---

## 7. Mapping Legacy Access Schema to Modern Domain Model

| Legacy Access Table (`dinurss.mdb`) | Modern Domain Model Aggregate / Entity | Notes & Architectural Improvements |
| :--- | :--- | :--- |
| `BILLFINAL`, `BILLFINAL_Dayend` | `Bill` Aggregate Root | Unified single table with temporal `BusinessDate` indexing; eliminates legacy archive tables. |
| `finalbill`, `finalbillcopy` | `OrderItem` Entity & `Bill` Snapshot | Strongly typed line items with direct relationship to `Bill`. |
| `KOTFINAL`, `KOTDETAIL`, `CancelKot` | `KOT` Entity & `OrderItem` status | Integrated into `Order` Aggregate; cancellation audit retained in `CancelKot`. |
| `item`, `ItemDept`, `BRANDML` | `MenuItem` & `Menu` Aggregate Root | Support for Devanagari (`MarathiName`), ML volumes, and multi-tier section pricing. |
| `TABLE_NO_GROP` | `Section` Aggregate Root | Explicit pricing tier binding (`AC`, `Family`, `VIP`). |
| `GodownStock`, `CNTPACK_LIVE`, `CNTLOOSE_LIVE` | `StockLocation`, `Bottle`, `LooseStock` | Clean 3-tier warehouse/counter/loose ML stock model with auto-uncorking invariants. |
| `ExPremiteHolder`, `ExciseMonthlyStat` | `PermitHolder`, `ExciseTransaction` | Full Maharashtra FL-III regulatory compliance tracking. |
| `DAYEND`, `dateLckMaster` | `DayEnd` Aggregate & `BusinessDate` VO | Business date lock engine with time-stamped audit events. |
| `Login`, `Setup`, `PRINTERSETUP` | `User`, `Role`, `Location`, `Device` | Enterprise Identity, RBAC, multi-tenancy, and hardware profile configuration. |
