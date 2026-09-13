# Hotel and Hospitality Operations Capability Audit Report

**Document ID:** `docs/verification/instance-11-hotel-operations-audit.md`
**Target Repository:** `YashdeepHotel`
**Target Branch:** `botify`
**Auditor:** Jules (AI Principal Software Architect & Verification Agent)
**Date:** Operational Baseline (2026)
**Audit Type:** Assessment-Only Evidence-Based Capability Audit
**Production Code Changed:** 0 lines (0% modification to production binaries, database schemas, or business logic)

---

## 1. Executive Summary & Verdict

This report presents a rigorous, evidence-based capability audit of all hotel and general hospitality functionality in the `YashdeepHotel` repository. The evaluation spans the legacy desktop codebase (`RSS26/` binaries, decompiled VB.NET forms, and Microsoft Access Jet 4.0 `dinurss.mdb`), the extracted PostgreSQL database schema (`schema_extracted/`), and the target modernization architectural specifications (`SYSTEM_ARCHITECTURE.md`, `DOMAIN_MODEL.md`, `SUBSCRIPTION_ARCHITECTURE.md`, `ENTITLEMENT_MODEL.md`, `docs/SAAS_ARCHITECTURE.md`, and `docs/verification/instance-10-modular-edition-audit.md`).

### Core Audit Verdict:
1. **Name Misnomer vs. Actual Domain Reality**: Despite the commercial trade name *"Hotel Yashdeep"* recorded in the legacy `HotelInfo` configuration table, the legacy monolithic application (`RSS.exe` / `dinurss.mdb`) is exclusively a **Restaurant, Bar, KOT/BOT, and Maharashtra State Excise (FL-III) Compliance System**. It possesses **zero lodging, room management, reservation, guest check-in/check-out, housekeeping, or room folio capabilities**.
2. **Zero Lodging Implementation in Legacy Binaries & DB**: Of the 105 physical tables in `dinurss.mdb` and 1,424 lines of DDL in `postgres_schema.sql`, there are **no tables, columns, or foreign keys** for rooms, room types, room rates, reservations, bookings, guest stay history, folios, or housekeeping. References to "Room" in legacy forms (`FRMENTRY`) refer strictly to dining seating sections (e.g., `FAMILYRATE` for the *Family Room* dining area) or physical storage spaces (e.g., *Godown* liquor warehouse).
3. **Target Modern Specification Status**: In the modern SaaS target architecture specifications, hotel operations are identified as a configurable product capability (Edition 2: *Hotel/Hospitality* and Edition 3: *Hotel plus Bar & Restaurant* in `instance-10-modular-edition-audit.md`). `DOMAIN_MODEL.md` references `PaymentMethod.RoomCredit` for posting dining bills to a guest room folio. However, **no domain aggregates, entities, value objects, API endpoints, Blazor screens, database migrations, or unit tests for lodging/hotel operations exist in the repository code**. Modern C# application code in `src/` remains **0% implemented**.

---

## 2. Hotel Capability Classification Matrix

All 28 hotel and hospitality operational capabilities requested in the audit prompt have been thoroughly inspected across all repository artifacts. Standardized classifications are applied consistently based on actual codebase evidence.

### Standard Classification Legend:
- **Implemented and verified**: Functionality exists in code/database, is fully wired from UI to persistence, and passes execution tests.
- **Partially implemented**: Elements exist in UI or DB, but the end-to-end workflow is incomplete or flawed.
- **Prototype or experimental**: Code or UI draft exists but is disconnected from production services or persistence.
- **Documented but not implemented**: Detailed architectural specification exists in documentation, but 0% code or DB schema exists.
- **Referenced but not found**: Mentioned casually in code comments, enums, or UI labels, but missing underlying entities or services.
- **Missing**: No evidence, documentation, schema, or code exists anywhere in the repository.
- **Blocked by an unresolved decision**: Cannot be implemented due to an open architectural or business decision.
- **Contradictory**: Conflicting definitions exist across different specifications or components.
- **Unable to verify**: Binary or code asset cannot be inspected or tested.

| # | Capability Name | Classification | Legacy Baseline (`RSS26/`) Evidence | Target Modern SaaS Architecture Status | Primary Evidence Location |
|---|---|---|---|---|---|
| 1 | **Hotel Setup** | Partially implemented | `HotelInfo` table stores trade name (`HOTEL YASHDEEP`), address, VAT TIN, FL-III license. No room property parameters. | Documented in `docs/SAAS_ARCHITECTURE.md` as `Location` entity attributes. | `schema_extracted/tables_inventory.csv` (`HotelInfo`) |
| 2 | **Branch Setup** | Documented but not implemented | **Missing in Legacy**. Single-site monolith (`HotelInfo` code=1). | Fully specified as `Location` entity under multi-tenant `Tenant` boundary in `SUBSCRIPTION_ARCHITECTURE.md`. | `SUBSCRIPTION_ARCHITECTURE.md` (Section 2) |
| 3 | **Room Types** | Missing | **None**. Zero tables or forms. | **None**. Omitted from `DOMAIN_MODEL.md`. | `schema_extracted/postgres_schema.sql` |
| 4 | **Rooms** | Missing | **None**. ("Family Room" in `item` refers to dining section). | **None**. Omitted from `DOMAIN_MODEL.md`. | `docs/BUSINESS_LOGIC.md` (Section 1.1) |
| 5 | **Room Status** | Missing | **None**. Zero room state tracking. | **None**. Omitted from `DOMAIN_MODEL.md`. | `schema_extracted/postgres_schema.sql` |
| 6 | **Room Availability** | Missing | **None**. | **None**. | `schema_extracted/postgres_schema.sql` |
| 7 | **Reservations** | Missing | **None**. | **None**. | `schema_extracted/postgres_schema.sql` |
| 8 | **Booking Changes** | Missing | **None**. | **None**. | `schema_extracted/postgres_schema.sql` |
| 9 | **Cancellations** | Missing | **None**. (Order cancellation exists for KOT, not room bookings). | **None**. | `LEGACY_SYSTEM_ANALYSIS.md` (Section 2) |
| 10 | **Guest Profiles** | Partially implemented | `ExPremiteHolder` table stores guest name & permit # for liquor laws. No stay/lodging profile. | Documented as customer entity in `DOMAIN_MODEL.md`, but strictly in restaurant/bar context. | `EXCISE_ARCHITECTURE.md` (Section 1.1) |
| 11 | **Guest History** | Missing | **None**. | **None**. | `schema_extracted/postgres_schema.sql` |
| 12 | **Check-in** | Missing | **None**. ("Check-in" in `ENTITLEMENT_MODEL.md` refers to device license validation). | **None**. | `ENTITLEMENT_MODEL.md` (Section 1) |
| 13 | **Check-out** | Missing | **None**. ("Checkout" in `SECURITY_ARCHITECTURE.md` refers to payment gateway UI). | **None**. | `SECURITY_ARCHITECTURE.md` (Section 1) |
| 14 | **Room Allocation** | Missing | **None**. | **None**. | `schema_extracted/postgres_schema.sql` |
| 15 | **Housekeeping** | Missing | **None**. | **None**. | `schema_extracted/postgres_schema.sql` |
| 16 | **Room-Cleaning Status** | Missing | **None**. | **None**. | `schema_extracted/postgres_schema.sql` |
| 17 | **Maintenance Status** | Missing | **None**. | **None**. | `schema_extracted/postgres_schema.sql` |
| 18 | **Guest Charges** | Referenced but not found | `GrandBill` tracks dining charges. Room charge postings do not exist. | Referenced as `PaymentMethod.RoomCredit` in `DOMAIN_MODEL.md`. | `DOMAIN_MODEL.md` (Context 5, Section 2.5) |
| 19 | **Folios / Stay Records** | Missing | **None**. | **None**. Omitted from `DOMAIN_MODEL.md`. | `DOMAIN_MODEL.md` |
| 20 | **Occupancy** | Missing | **None**. ("Occupancy" in reporting docs refers to dining table turnover). | **None**. | `REPORTING_ARCHITECTURE.md` |
| 21 | **Room Pricing** | Missing | **None**. (`FAMILYRATE`, `ACRATE`, `VIPRATE` in `item` refer to food/liquor section pricing). | **None**. | `docs/BUSINESS_LOGIC.md` (Section 1.2) |
| 22 | **Taxes (Room/Lodging)** | Missing | `Setup.Gst` and `BILLFINAL` track restaurant CGST/SGST (5% food, 18% liquor/VAT). No lodging tax tiers. | Tax structure in `DOMAIN_MODEL.md` targets GST for F&B. | `BILLING_ARCHITECTURE.md` (Section 3) |
| 23 | **Discounts** | Partially implemented | `BILLFINAL.DISCOUNT` handles restaurant bill discounts. No room stay rate discounts. | `Bill` aggregate supports bill-level discounts. | `schema_extracted/postgres_schema.sql` (`BILLFINAL`) |
| 24 | **No-Shows** | Missing | **None**. | **None**. | `schema_extracted/postgres_schema.sql` |
| 25 | **Extensions** | Missing | **None**. | **None**. | `schema_extracted/postgres_schema.sql` |
| 26 | **Room Transfers** | Missing | **None**. (Table transfers exist for dining tables in `FRMENTRY`, not lodging rooms). | **None**. | `LEGACY_SYSTEM_ANALYSIS.md` (Section 3) |
| 27 | **Hotel Reporting** | Documented but not implemented | Legacy reports (`nwitem5.rpt`) cover item sales, excise registers, daily billing. | Spec defines `Edition 2` / `Edition 3` reports in `instance-10-modular-edition-audit.md`. | `REPORTING_ARCHITECTURE.md` |
| 28 | **Room Charge Settlement** | Referenced but not found | **None**. | Enum value `PaymentMethod.RoomCredit` in `DOMAIN_MODEL.md`. No room folio target handler. | `DOMAIN_MODEL.md` (Section 2.5.4) |

---

## 3. Detailed Capability-by-Capability Audit

### 3.1 Hotel Setup & General Property Configuration
- **Status Classification**: `Partially implemented`
- **Repository Evidence**:
  - **UI / Screen / Route**: Legacy WinForms `FrmSetup` / `HotelInfo` configuration dialog.
  - **Database Tables**: `HotelInfo` (`code`, `name`, `address`, `city`, `licno`, `Vattin`, `upi_id`, `AdminMobile`, `Email`).
  - **Entities / DTOs / APIs**: Target `Location` entity defined in `docs/SAAS_ARCHITECTURE.md` (Section 2.3).
- **Missing Functionality**: Property check-in/check-out default times, late check-out grace periods, child policy, extra bed charges, currency settings, lodging tax registration numbers (GSTIN vs Excise License).
- **Dependencies & Blockers**: Requires formal `HotelProperty` entity extending `Location` in modern `DOMAIN_MODEL.md`.
- **Production Risks**: Inability to configure lodging property parameters forces hardcoding or manual manual bill adjustments.
- **Suggested Epic & Priority**: Epic H1 — *Hotel Property & Room Catalog Management* (Priority: High).
- **Required Test Coverage**: Unit tests for property configuration validation; integration tests for tenant/location setup.

### 3.2 Branch Setup & Multi-Location Configuration
- **Status Classification**: `Documented but not implemented`
- **Repository Evidence**:
  - **UI / Screen / Route**: None in code.
  - **Database Tables**: None in `dinurss.mdb`. Target PostgreSQL schema in `docs/SAAS_ARCHITECTURE.md` specifies `Location` table (`LocationId`, `TenantId`, `Code`, `Name`, `Address`).
- **Missing Functionality**: Cross-branch room reservation routing, multi-property guest search, branch-specific room inventories, central administrative controls.
- **Dependencies & Blockers**: Depends on multi-tenant core framework (`TenantContext` and `LocationContext`).
- **Production Risks**: Single-branch legacy architecture prevents hotel chains from operating unified guest loyalty or cross-property bookings.
- **Suggested Epic & Priority**: Epic M1 — *Multi-Location & Organization Management* (Priority: High).
- **Required Test Coverage**: Multi-tenant isolation integration tests; EF Core Global Query Filter tests.

### 3.3 Room Types & Categories
- **Status Classification**: `Missing`
- **Repository Evidence**: Zero tables, columns, or code files exist.
- **Missing Functionality**: Creation and management of room categories (e.g., Deluxe, Suite, Executive, Super Deluxe, Single/Double Occupancy, AC/Non-AC), base occupant capacity, max extra beds, base rack rates, seasonal rate multipliers.
- **Dependencies & Blockers**: Core domain aggregate missing from `DOMAIN_MODEL.md`.
- **Production Risks**: Complete absence of room classification prevents room inventory creation or automated rate calculations.
- **Suggested Epic & Priority**: Epic H1 — *Hotel Property & Room Catalog Management* (Priority: High).
- **Required Test Coverage**: Domain entity unit tests for `RoomType` aggregate invariants.

### 3.4 Rooms & Room Inventory Setup
- **Status Classification**: `Missing`
- **Repository Evidence**: Zero tables or columns exist. (Note: "Family Room" in legacy database `TABLE_NO_GROP` represents dining table group `GROP='Family'`, not a lodging room).
- **Missing Functionality**: Room master setup (Room Number, Floor/Wing, Room Type FK, Interconnecting Room flag, Smoking/Non-Smoking, Key Card Lock ID, Active Status).
- **Dependencies & Blockers**: Blocked by missing `RoomType` aggregate.
- **Production Risks**: Inability to map physical rooms or track room-level assets/status.
- **Suggested Epic & Priority**: Epic H1 — *Hotel Property & Room Catalog Management* (Priority: High).
- **Required Test Coverage**: Integration tests for room inventory creation and floor plan layout validation.

### 3.5 Room Status Management (Real-Time State)
- **Status Classification**: `Missing`
- **Repository Evidence**: Zero code or schema exists.
- **Missing Functionality**: Real-time room status FSM (`Vacant Clean`, `Vacant Dirty`, `Occupied`, `Expected Arrival`, `Expected Departure`, `Out of Order`, `Out of Service`, `Inspected`).
- **Dependencies & Blockers**: Blocked by missing `Room` aggregate.
- **Production Risks**: High risk of assigning dirty or out-of-order rooms to arriving guests.
- **Suggested Epic & Priority**: Epic H2 — *Front Desk & Real-Time Room Operations* (Priority: High).
- **Required Test Coverage**: Finite State Machine transition unit tests for invalid status changes.

### 3.6 Room Availability Engine & Inventory Calendar
- **Status Classification**: `Missing`
- **Repository Evidence**: Zero implementation or architectural documentation exists.
- **Missing Functionality**: Date-range room availability matrix, overbooking limit controls, stop-sell triggers, minimum stay duration rules, channel manager inventory sync.
- **Dependencies & Blockers**: Requires `Room`, `RoomType`, and `Reservation` domain aggregates.
- **Production Risks**: Severe risk of double-booking rooms, especially under offline edge terminal operations.
- **Suggested Epic & Priority**: Epic H3 — *Reservation, Booking & Channel Management Engine* (Priority: Critical).
- **Required Test Coverage**: Concurrency tests for concurrent availability queries; offline availability calculation tests.

### 3.7 Reservations & Booking Engine
- **Status Classification**: `Missing`
- **Repository Evidence**: Zero implementation exists in codebase or database schemas.
- **Missing Functionality**: Reservation creation (Direct Walk-in, Phone, OTA, Corporate), advance payment collection, confirmation voucher generation, group reservations, rate plan binding.
- **Dependencies & Blockers**: Unresolved decision on whether to support external Channel Managers (e.g. Staah, RateGain) via webhooks.
- **Production Risks**: Reliance on manual paper reservation logs leads to unrecorded bookings and revenue leakage.
- **Suggested Epic & Priority**: Epic H3 — *Reservation, Booking & Channel Management Engine* (Priority: Critical).
- **Required Test Coverage**: Unit tests for reservation lifecycle transitions; validation rules for booking dates.

### 3.8 Booking Modifications & Amendments
- **Status Classification**: `Missing`
- **Repository Evidence**: Zero code or schema exists.
- **Missing Functionality**: Stay duration amendments, date changes, room type upgrades/downgrades, rate re-calculation, audit trail logging of modification history.
- **Dependencies & Blockers**: Requires `Reservation` Aggregate Root.
- **Production Risks**: Unaudited rate modifications during stay amendments create high vulnerability to internal cashier fraud.
- **Suggested Epic & Priority**: Epic H3 — *Reservation, Booking & Channel Management Engine* (Priority: Medium).
- **Required Test Coverage**: Integration tests verifying financial adjustments upon booking modification.

### 3.9 Cancellations & Refund Management
- **Status Classification**: `Missing`
- **Repository Evidence**: Zero room cancellation logic exists. (Legacy `KOT` / bill cancellation in `FRMENTRY` applies solely to food/beverage orders).
- **Missing Functionality**: Cancellation policy enforcement (Free cancellation window, penalty fee calculation, advance deposit refund processing, cancellation reason logging).
- **Dependencies & Blockers**: Depends on `Reservation` aggregate and `PaymentGateway` integration.
- **Production Risks**: Uncontrolled advance deposit refunds without dual-approval audit logs.
- **Suggested Epic & Priority**: Epic H3 — *Reservation, Booking & Channel Management Engine* (Priority: Medium).
- **Required Test Coverage**: Unit tests for cancellation policy fee calculations; refund transaction boundary tests.

### 3.10 Guest Profiles & KYC Documentation
- **Status Classification**: `Partially implemented`
- **Repository Evidence**:
  - **Legacy Baseline**: `ExPremiteHolder` table stores guest full name, liquor permit number, and permit expiry date for Maharashtra State Excise compliance.
  - **Target SaaS Specs**: `Customer` entity in `DOMAIN_MODEL.md` (Section 2.5) stores general customer contact info.
- **Missing Functionality**: Comprehensive hotel guest KYC profile (ID Proof Type: Passport / Aadhaar / Voter ID, Document Scans / Photo upload, Nationality, Address, VIP tier, Blacklist flag, Police Verification C-Form data for foreign guests).
- **Dependencies & Blockers**: Expansion of `Customer` entity or creation of dedicated `GuestProfile` aggregate root.
- **Production Risks**: Non-compliance with statutory Indian Police C-Form regulations for foreign guests carries severe legal penalties.
- **Suggested Epic & Priority**: Epic H2 — *Front Desk & Guest Identity Management* (Priority: High).
- **Required Test Coverage**: Validation tests for Indian Aadhaar / Passport document formats; PII encryption tests.

### 3.11 Guest Stay History & Preference Tracking
- **Status Classification**: `Missing`
- **Repository Evidence**: Zero code or schema exists.
- **Missing Functionality**: Historical stay ledger (past visits, preferred room types, special requests, total lifetime spend, incident/complaint records).
- **Dependencies & Blockers**: Requires `GuestProfile` and `Folio` aggregates.
- **Production Risks**: Inability to recognize repeat guests or apply corporate pre-agreed rates.
- **Suggested Epic & Priority**: Epic H2 — *Front Desk & Guest Identity Management* (Priority: Low).
- **Required Test Coverage**: Historical query performance tests across multi-year stay records.

### 3.12 Check-In Workflow & Room Allocation
- **Status Classification**: `Missing`
- **Repository Evidence**: Zero implementation. (Note: Mention of "check-in" in `ENTITLEMENT_MODEL.md` refers strictly to device hardware entitlement checks with cloud authority).
- **Missing Functionality**: Walk-in check-in, reserved check-in, room assignment/allocation, advance payment verification, key card encoder interface (RFID/Magstripe), Guest Registration Card (GRC) generation and digital signature capture.
- **Dependencies & Blockers**: Requires `Reservation`, `Room`, and `Folio` aggregate roots.
- **Production Risks**: Unchecked room allocation causes double-assignment of occupied rooms.
- **Suggested Epic & Priority**: Epic H2 — *Front Desk & Real-Time Room Operations* (Priority: Critical).
- **Required Test Coverage**: Transactional boundary tests ensuring room status updates to `Occupied` atomically with folio creation.

### 3.13 Check-Out Workflow & Settlement
- **Status Classification**: `Missing`
- **Repository Evidence**: Zero room check-out implementation exists. (Note: "Checkout" in `SECURITY_ARCHITECTURE.md` refers strictly to web payment gateway widgets).
- **Missing Functionality**: Folio balance inspection, pending POS charge posting checks, late check-out fee auto-posting, payment settlement (Cash, UPI, Card, Corporate City Ledger), tax invoice generation, key card return check, room status transition to `Vacant Dirty`.
- **Dependencies & Blockers**: Requires `Folio` aggregate and `QuestPDF` invoice generator.
- **Production Risks**: Allowing guest check-out while pending restaurant/room-service charges remain unposted leads to direct financial loss.
- **Suggested Epic & Priority**: Epic H4 — *Guest Folio, Billing & Cross-Outlet Settlement Engine* (Priority: Critical).
- **Required Test Coverage**: Integration tests for check-out blocking when unpaid folio balance > 0.

### 3.14 Room Allocation Engine
- **Status Classification**: `Missing`
- **Repository Evidence**: Zero code or schema exists.
- **Missing Functionality**: Automated or manual room assignment algorithms taking into account guest preferences, floor distribution, room maintenance schedules, and adjacent room requirements for families.
- **Dependencies & Blockers**: Blocked by missing `Room` and `Reservation` aggregates.
- **Production Risks**: Operational bottlenecks during peak arrival windows due to manual room selection.
- **Suggested Epic & Priority**: Epic H2 — *Front Desk & Real-Time Room Operations* (Priority: Medium).
- **Required Test Coverage**: Room allocation constraint validation unit tests.

### 3.15 Housekeeping Operations & Task Management
- **Status Classification**: `Missing`
- **Repository Evidence**: Zero code or schema exists.
- **Missing Functionality**: Housekeeping task creation, daily attendant room assignments, linen/laundry tracking, room inspection workflows, turndown service logs.
- **Dependencies & Blockers**: Requires `Room` aggregate and RBAC Housekeeping Staff role.
- **Production Risks**: Delayed room turnover slowing down arrival check-in times.
- **Suggested Epic & Priority**: Epic H5 — *Housekeeping & Room Maintenance Management* (Priority: Medium).
- **Required Test Coverage**: Task state machine unit tests (`Unassigned` → `In Progress` → `Cleaned` → `Inspected`).

### 3.16 Room Cleaning Status Tracking
- **Status Classification**: `Missing`
- **Repository Evidence**: Zero code or schema exists.
- **Missing Functionality**: Mobile/tablet interface for housekeeping staff to update real-time room cleanliness (`Dirty` → `Cleaning` → `Clean` → `Inspected`), supervisor approval gate.
- **Dependencies & Blockers**: Requires Blazor Mobile/Hybrid UI components for Housekeeping.
- **Production Risks**: Reception staff assigning `Dirty` rooms to arriving guests due to stale paper logs.
- **Suggested Epic & Priority**: Epic H5 — *Housekeeping & Room Maintenance Management* (Priority: Medium).
- **Required Test Coverage**: Real-time SignalR status broadcast integration tests.

### 3.17 Maintenance & Out-of-Order (OOO) Management
- **Status Classification**: `Missing`
- **Repository Evidence**: Zero code or schema exists. (Note: "Maintenance" in `PROJECT_ANALYSIS.md` refers strictly to software maintenance and Azure DevOps setup).
- **Missing Functionality**: Maintenance ticket logging (Plumbing, Electrical, HVAC, Furniture), Out-of-Order (OOO - affects inventory capacity) vs. Out-of-Service (OOS - usable in emergencies) status tagging, resolution verification.
- **Dependencies & Blockers**: Requires `Room` entity and maintenance ticket aggregate.
- **Production Risks**: Assigning rooms with malfunctioning air conditioning or plumbing to guests, resulting in customer complaints and refunds.
- **Suggested Epic & Priority**: Epic H5 — *Housekeeping & Room Maintenance Management* (Priority: Medium).
- **Required Test Coverage**: Availability matrix exclusion unit tests for OOO rooms.

### 3.18 Guest Charges & Incidentals
- **Status Classification**: `Referenced but not found`
- **Repository Evidence**:
  - **Legacy Baseline**: `GrandBill` and `BILLFINAL` record food and liquor charges for dining tables.
  - **Target SaaS Specs**: `DOMAIN_MODEL.md` (Context 5) references `PaymentMethod.RoomCredit` for routing bill amounts to a room folio.
- **Missing Functionality**: Incidental charge posting (Laundry, Telephone, Mini-bar, Paid Parking, Airport Transfer), line-item void permissions, charge limit checks against guest credit caps.
- **Dependencies & Blockers**: Requires `Folio` domain aggregate and cross-outlet posting handlers.
- **Production Risks**: Unchecked incidental postings exceeding guest deposit limits without cashier warnings.
- **Suggested Epic & Priority**: Epic H4 — *Guest Folio, Billing & Cross-Outlet Settlement Engine* (Priority: High).
- **Required Test Coverage**: Credit limit validation unit tests prior to charge posting.

### 3.19 Folios & Guest Stay Financial Records
- **Status Classification**: `Missing`
- **Repository Evidence**: Zero tables or classes exist for Folios. (Note: `BILLFINAL` in legacy Access schema represents dining table bills, not stay folios).
- **Missing Functionality**: Multi-folio support per room (Master Folio for room charges, Incidentals Folio, Corporate City Ledger Folio), debit/credit ledger, room rate auto-posting during Night Audit, payment posting, split-folio capabilities.
- **Dependencies & Blockers**: Unspecified core aggregate in `DOMAIN_MODEL.md`.
- **Production Risks**: Inability to maintain accurate, immutable financial audit trails for guest stay transactions.
- **Suggested Epic & Priority**: Epic H4 — *Guest Folio, Billing & Cross-Outlet Settlement Engine* (Priority: Critical).
- **Required Test Coverage**: Double-entry financial balance verification unit tests (`Total Debits - Total Credits == Balance`).

### 3.20 Occupancy Analytics & Revenue Metrics
- **Status Classification**: `Missing`
- **Repository Evidence**: Zero room occupancy reporting exists. (Note: Mention of "Occupancy" in `REPORTING_ARCHITECTURE.md` refers strictly to restaurant table peak hour turnover analytics).
- **Missing Functionality**: Hotel room occupancy percentage (Occupied Rooms / Total Available Rooms), Average Daily Rate (ADR = Room Revenue / Occupied Rooms), Revenue Per Available Room (RevPAR = Room Revenue / Total Available Rooms), Market Segment breakdown.
- **Dependencies & Blockers**: Requires historical stay and folio dataset.
- **Production Risks**: Inability for hotel management to evaluate property financial performance or yield management opportunities.
- **Suggested Epic & Priority**: Epic H6 — *Hotel Analytics, Yield & Statutory Reporting* (Priority: High).
- **Required Test Coverage**: QuestPDF report rendering tests and mathematical precision verification for RevPAR/ADR calculations.

### 3.21 Room Pricing & Rate Plan Engine
- **Status Classification**: `Missing`
- **Repository Evidence**: Zero room rate tables exist. (Note: `FAMILYRATE`, `ACRATE`, `VIPRATE` in legacy `item` table represent differential pricing tiers for menu food and liquor items served in different dining halls, NOT lodging room rates).
- **Missing Functionality**: Rate plan setup (EP - European Plan / Room Only, CP - Continental Plan / Breakfast included, MAP - Modified American Plan / Half Board, AP - American Plan / Full Board), day-of-week dynamic pricing, seasonal rate matrices, extra guest/child charges, corporate contracted rates.
- **Dependencies & Blockers**: Requires `RatePlan` entity and pricing rules engine.
- **Production Risks**: Static or manual room rate entries during check-in leading to pricing errors and uncaptured revenue.
- **Suggested Epic & Priority**: Epic H1 — *Hotel Property & Room Catalog Management* (Priority: High).
- **Required Test Coverage**: Dynamic rate calculation unit tests across complex date ranges and meal plans.

### 3.22 Lodging Taxes & GST Structure
- **Status Classification**: `Missing`
- **Repository Evidence**:
  - **Legacy Baseline**: `Setup.Gst`, `BILLFINAL.CGST`, `BILLFINAL.SGST` track 5% restaurant GST and State Excise taxes on liquor sales.
  - **Target SaaS Specs**: `BILLING_ARCHITECTURE.md` specifies GST engines for F&B invoices. Zero room lodging GST tiers exist.
- **Missing Functionality**: Indian GST lodging slab tax calculations (e.g. 0% for declared room tariff ≤ ₹1,000, 12% for ₹1,001–₹7,500, 18% for > ₹7,500), SAC code classification (996311 for room accommodation vs 996331 for restaurant service).
- **Dependencies & Blockers**: Requires tax engine expansion for SAC 996311 lodging slabs.
- **Production Risks**: Non-compliance with Indian GST tax council regulations for hotel accommodation, incurring heavy statutory audit penalties.
- **Suggested Epic & Priority**: Epic H4 — *Guest Folio, Billing & Cross-Outlet Settlement Engine* (Priority: Critical).
- **Required Test Coverage**: Unit tests verifying correct GST percentage slab selection based on declared room tariff vs net transaction price.

### 3.23 Discounts & Promotional Rate Allowances
- **Status Classification**: `Partially implemented`
- **Repository Evidence**:
  - **Legacy Baseline**: `BILLFINAL.DISCOUNT` tracks discounts applied to dining table checks.
  - **Target SaaS Specs**: `Bill` aggregate supports bill-level percentage or fixed amount discounts.
- **Missing Functionality**: Room stay specific discounts (Promotional promo codes, corporate manager discretionary allowances, length-of-stay discounts), discount capping rules, manager approval override locks.
- **Dependencies & Blockers**: Requires `Folio` aggregate integration.
- **Production Risks**: Cashiers applying unauthorized room rate discounts without supervisory approval or audit records.
- **Suggested Epic & Priority**: Epic H4 — *Guest Folio, Billing & Cross-Outlet Settlement Engine* (Priority: Medium).
- **Required Test Coverage**: Role-based discount authorization threshold unit tests.

### 3.24 No-Show Management & Fee Posting
- **Status Classification**: `Missing`
- **Repository Evidence**: Zero code or schema exists.
- **Missing Functionality**: Automatic Night Audit detection of un-arrived reservations past midnight cutoff, state transition to `No-Show`, retention/forfeiture of advance deposit, no-show invoice generation.
- **Dependencies & Blockers**: Requires `Reservation`, `Folio`, and Night Audit engine.
- **Production Risks**: Rooms held indefinitely for non-arriving guests, causing lost walk-in sales and uncollected retention fees.
- **Suggested Epic & Priority**: Epic H3 — *Reservation, Booking & Channel Management Engine* (Priority: Medium).
- **Required Test Coverage**: Automated Night Audit no-show trigger unit tests.

### 3.25 Stay Extensions & Late Check-Out
- **Status Classification**: `Missing`
- **Repository Evidence**: Zero code or schema exists.
- **Missing Functionality**: Stay extension availability re-validation, folio date modification, late check-out fee rules (e.g., free up to 2 hours, 50% room rate up to 6 hours, 100% room rate after 6 hours).
- **Dependencies & Blockers**: Requires `Reservation` and `Folio` aggregates.
- **Production Risks**: Unrecorded stay extensions blocking arriving guests reserved for the same room.
- **Suggested Epic & Priority**: Epic H2 — *Front Desk & Real-Time Room Operations* (Priority: Medium).
- **Required Test Coverage**: Extension conflict check unit tests against future reservations.

### 3.26 Room Transfers & Switches
- **Status Classification**: `Missing`
- **Repository Evidence**:
  - **Legacy Baseline**: Table transfer logic (`FrmTableTransfer` / `FRMENTRY`) allows moving dining orders between restaurant tables. No room transfer capability exists.
- **Missing Functionality**: Mid-stay guest room transfer, reason logging (Maintenance issue, guest request, upgrade), pending charge carry-forward to new room folio, key card re-issuance, previous room status update to `Vacant Dirty`.
- **Dependencies & Blockers**: Requires `Folio` and `Room` aggregates.
- **Production Risks**: Splitting guest charges across two unlinked folios during room switches, leading to uncollected charges at check-out.
- **Suggested Epic & Priority**: Epic H2 — *Front Desk & Real-Time Room Operations* (Priority: Medium).
- **Required Test Coverage**: Atomic room transfer transaction integration tests.

### 3.27 Hotel & Hospitality Analytics Reporting
- **Status Classification**: `Documented but not implemented`
- **Repository Evidence**:
  - **Legacy Baseline**: `nwitem5.rpt` and `nwitem5.vb` generate Crystal Reports for itemized food/liquor sales and excise daily registers. Zero hotel lodging reports exist.
  - **Target SaaS Specs**: `REPORTING_ARCHITECTURE.md` defines QuestPDF infrastructure for F&B receipts. `instance-10-modular-edition-audit.md` defines target Edition 2 / Edition 3 reporting requirements.
- **Missing Functionality**: Lodging Manager Flash Report, Daily Police Verification (C-Form) Export, Room Status Summary, Reservation Pace Report, Folio Aging Ledger, Night Audit Balance Sheet.
- **Dependencies & Blockers**: Requires QuestPDF templates backed by hotel domain queries.
- **Production Risks**: Absence of statutory police export files violates local lodging regulations.
- **Suggested Epic & Priority**: Epic H6 — *Hotel Analytics, Yield & Statutory Reporting* (Priority: High).
- **Required Test Coverage**: QuestPDF template rendering unit tests; C-Form export CSV format validation tests.

### 3.28 Cross-Outlet Room Charge Settlement (`RoomCredit`)
- **Status Classification**: `Referenced but not found`
- **Repository Evidence**:
  - **Legacy Baseline**: Zero cross-outlet settlement exists. Bar and restaurant bills in `dinurss.mdb` are strictly settled via Cash or Credit Card.
  - **Target SaaS Specs**: `DOMAIN_MODEL.md` (Context 5, Section 2.5.4) explicitly specifies `PaymentMethod.RoomCredit` in the `Payment` entity. However, no application service handler or API route exists to locate active room folios or post room credits.
- **Missing Functionality**: POS interface to search occupied room numbers and guest names, credit limit check, signature verification, posting POS bill subtotal directly to room `Folio` as a debit line item, transaction rollback if room folio post fails.
- **Dependencies & Blockers**: Unresolved cross-domain dependency between F&B `Order`/`Bill` aggregates and Hotel `Folio` aggregate.
- **Production Risks**: Cashiers selecting `PaymentMethod.RoomCredit` on POS terminal without a backend folio engine, resulting in orphaned dining bills and uncollected revenue.
- **Suggested Epic & Priority**: Epic H4 — *Guest Folio, Billing & Cross-Outlet Settlement Engine* (Priority: Critical).
- **Required Test Coverage**: Cross-domain MediatR command handler integration tests verifying atomic billing + folio posting.

---

## 4. Workflow Maturity Assessment

The table below summarizes the operational maturity of core hospitality workflows across the codebase:

```
┌───────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                   WORKFLOW MATURITY SPECTRUM                                      │
├───────────────────────────┬───────────────────────────┬───────────────────────────────────────────┤
│    WORKFLOW DOMAIN        │      MATURITY LEVEL       │           OPERATIONAL REALITY             │
├───────────────────────────┼───────────────────────────┼───────────────────────────────────────────┤
│ Front Desk & Reception    │ Unimplemented / Missing   │ Zero UI, zero logic, zero database tables. │
│ Housekeeping & Maintenance│ Unimplemented / Missing   │ Zero task tracking or room state logic.  │
│ Lodging Night Audit       │ Unimplemented / Missing   │ F&B Day End exists (`DAYEND`), no room    │
│                           │                           │ auto-posting or room rate roll.           │
│ Guest Folio & Settlement  │ Documented / Unbuilt      │ Spec mentions `RoomCredit`; 0% code.      │
│ F&B & Bar Billing         │ Implemented & Verified    │ Full legacy WinForms + Access Jet F&B POS │
│ State Excise (FL-III)     │ Implemented & Verified    │ Full daily bulk litre & permit registers. │
└───────────────────────────┴───────────────────────────┴───────────────────────────────────────────┘
```

### Key Workflow Deficiencies:
1. **Front Desk Workflow Gap**: No capability exists for registering walk-in guests, looking up advance bookings, generating GRC cards, or issuing room keys.
2. **Lodging Night Audit Gap**: The legacy system features a robust F&B `Day End` process (`DAYEND`, `KOTFINAL_DAYEND`, `GrandBillDetailscopy_Dayend`) that moves daily restaurant transactions into historical archive tables. However, it lacks a **Lodging Night Audit** step to automatically post room tariffs, taxes, and fixed meal packages to active folios at business day roll.
3. **Cross-Outlet Posting Gap**: Restaurant and bar POS terminals cannot verify if a guest is currently checked into a room, nor can they post dining bills directly to a stay folio.

---

## 5. Tenant and Branch Multi-Organization Implications

The modern SaaS target architecture specified in `SUBSCRIPTION_ARCHITECTURE.md` and `docs/SAAS_ARCHITECTURE.md` establishes a hierarchical multi-tenant structure (`Organization` → `Tenant` → `Location` → `Device`).

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Organization (e.g., Yashdeep Group)                 │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────┐
│              Tenant (Legal Tax / Excise License Boundary)               │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │
                     ┌───────────────┴───────────────┐
                     ▼                               ▼
┌─────────────────────────────────────────┐ ┌──────────────────────────────┐
│  Location 1 (Yashdeep Hotel - Bhenda)   │ │ Location 2 (Yashdeep Resort) │
│  (Rooms 101-120, Bar, Restaurant)       │ │ (Rooms 201-250, Pool Bar)    │
└─────────────────────────────────────────┘ └──────────────────────────────┘
```

### Architectural Requirements & Risks for Multi-Tenant Lodging:
1. **Strict Data Isolation**: All room, reservation, guest profile, housekeeping, and folio records MUST contain `TenantId` and `LocationId` foreign keys. EF Core `DbContext` must enforce global query filters (`builder.Entity<Room>().HasQueryFilter(r => r.TenantId == _tenantContext.TenantId)`).
2. **Cross-Branch Guest History Security**: While room inventory and folios MUST be strictly isolated per `LocationId`, guest profiles (`GuestProfile`) may be shared across locations under the same `TenantId` (or `OrganizationId`) to enable chain-wide VIP recognition. However, sharing PII data across distinct tenants is strictly prohibited under data privacy laws.
3. **Data Leakage Risk**: Omitting `LocationId` filtering on room availability queries could expose physical room assignments of one branch to reception terminals of another branch.

---

## 6. Offline & Edge Synchronization Implications

The target architecture mandates an **Offline-First** operational model via local SQLite databases on edge terminals using the Outbox/Inbox synchronization pattern (`OFFLINE_ARCHITECTURE.md`).

### Critical Offline Risks in Hotel Lodging Operations:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        Cloud Synchronization Hub                       │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │
                    Network Partition (Offline Split-Brain)
                                     │
                   ┌─────────────────┴─────────────────┐
                   ▼                                   ▼
┌─────────────────────────────────────┐ ┌─────────────────────────────────────┐
│  Front Desk Terminal A (Offline)    │ │  Front Desk Terminal B (Offline)    │
│  Allocates Room 101 to Guest X      │ │  Allocates Room 101 to Guest Y      │
│  (Enqueues Local SQLite Outbox)     │ │  Enqueues Local SQLite Outbox)      │
└─────────────────────────────────────┘ └─────────────────────────────────────┘
                                     │
                      Network Reconnected (Sync Collision!)
                                     │
                                     ▼
             Room 101 DOUBLE-BOOKED! Unresolvable Physical Conflict
```

1. **Split-Brain Double Booking Risk**: If two Front Desk terminals operate offline during a cloud network partition, both terminals could independently allocate the same vacant room (`Room 101`) to two different walk-in guests. Unlike F&B dining orders (where two tables can order the same menu item simultaneously without conflict), physical lodging room allocation is a **zero-sum constraint**.
2. **Offline Reservation Rule**: Room allocations and new reservations MUST require active online validation with the Cloud Authority unless a terminal holds an exclusive leased lock on a specific room block.
3. **Local Folio Caching**: Guest folios must be cached in local SQLite database on edge terminals to allow room service posting during internet outages. Local Outbox messages for `PaymentMethod.RoomCredit` must execute optimistic credit check validations.

---

## 7. Subsystem Integration Analysis

The matrix below evaluates how hotel lodging operations interface with other primary subsystems in the target architecture:

| Subsystem | Integration Point & Workflow | Current Implementation Status | Vulnerability / Deficiency |
| :--- | :--- | :--- | :--- |
| **Billing & POS** | Posting restaurant/bar bills to room folio (`RoomCredit`). | Referenced in `DOMAIN_MODEL.md`; 0% code. | Cashiers can select `RoomCredit` without an active folio engine, causing orphaned bills. |
| **Payments** | Advance deposit collection, pre-authorizations, check-out settlement. | `Payment` aggregate exists for F&B. | Lacks support for card pre-authorization holds or multi-folio split payments. |
| **Inventory** | Room amenities, mini-bar consumption, linen laundry stock. | F&B Godown/Counter stock exists in `dinurss.mdb`. | No link between room status transitions and mini-bar/amenity stock consumption. |
| **User Permissions & RBAC** | Receptionist, Housekeeper, Duty Manager, Night Auditor roles. | Basic `Login` table with Admin/Operator roles in Access. | Lacks granular permission claims for lodging (e.g., `Folio.Void`, `Rate.Override`). |
| **Device Registration** | Front Desk PCs, Tablet devices for Housekeeping. | Device fingerprinting specified in `DEVICE_MANAGEMENT.md`. | No device category restrictions forcing Front Desk tasks to authorized hardware. |
| **Synchronization** | Outbox sync of reservation changes, check-ins, room status updates. | Outbox pattern defined in `OFFLINE_ARCHITECTURE.md`. | High risk of offline split-brain room allocation collisions. |
| **Reporting** | RevPAR, ADR, Occupancy %, Police C-Form export. | QuestPDF F&B engine specified in `REPORTING_ARCHITECTURE.md`. | Complete absence of lodging analytical query models or QuestPDF templates. |
| **Audit Records** | Room rate overrides, folio line-item voids, room switches. | Basic `ErrorLog_*.txt` text files in legacy `RSS26/`. | No structured audit ledger tracking sensitive room rate or financial overrides. |

---

## 8. Business Rules & Invariant Enforcement Assessment

The table below assesses 13 critical hospitality business rules and invariants:

| # | Business Rule / Invariant | Status | Invariant Enforcement Mechanism | Failure Mode & Risk |
|---|---|---|---|---|
| 1 | **Room Availability Invariant**: A physical room cannot be occupied by more than one active stay folio concurrently. | Unenforced | **None**. Missing `Room` and `Reservation` aggregates. | Physical double-booking of rooms, leading to guest displacement and reputational damage. |
| 2 | **Double-Booking Prevention**: Date-range availability must verify `(CheckIn < ExistingCheckOut) AND (CheckOut > ExistingCheckIn)`. | Unenforced | **None**. | Overbooking room categories past total physical inventory limits. |
| 3 | **Reservation Modification Rule**: Amendments modifying stay dates must re-evaluate room availability for newly requested dates. | Unenforced | **None**. | Overwriting existing confirmed reservations on extended dates. |
| 4 | **Cancellation Policy Rule**: Cancellations within penalty windows must automatically calculate non-refundable fee. | Unenforced | **None**. | Loss of cancellation revenue due to manual cashier oversight. |
| 5 | **No-Show Rule**: Reservations past midnight without check-in auto-transition to `No-Show` during Night Audit. | Unenforced | **None**. | Rooms held indefinitely for non-arriving guests. |
| 6 | **Check-In Validation Invariant**: Check-in requires mandatory guest KYC document capture and advance payment/deposit. | Unenforced | **None**. | Non-compliance with police regulations and uncollected room revenue. |
| 7 | **Check-Out Settlement Invariant**: Room check-out is blocked if `Folio.Balance != 0`. | Unenforced | **None**. | Guests checking out with unpaid room or restaurant charges. |
| 8 | **Room Status Transition Rule**: Check-out automatically sets room status to `Vacant Dirty`. Room cannot be assigned until set to `Clean/Inspected`. | Unenforced | **None**. | Assigning dirty, uncleaned rooms to arriving walk-in guests. |
| 9 | **Housekeeping Status Invariant**: Only supervisors can transition room status from `Clean` to `Inspected`. | Unenforced | **None**. | Uninspected rooms assigned with missing amenities. |
| 10 | **Guest Identity Invariant**: Foreign nationals require mandatory Passport & Visa details for Police C-Form generation. | Unenforced | **None**. | Severe statutory fines and legal penalties under Indian immigration laws. |
| 11 | **Lodging Tax Invariant**: Room tariff GST slab must be calculated dynamically based on declared rate (0%, 12%, 18%). | Unenforced | **None**. | Tax audit penalties for incorrect GST slab application. |
| 12 | **Discount Approval Rule**: Room rate discounts exceeding 10% require supervisory override authentication. | Unenforced | **None**. | Revenue leakage via unauthorized cashier rate reductions. |
| 13 | **Cross-Outlet Reconciliation Invariant**: `RoomCredit` POS postings must atomically debit the target room folio and lock upon checkout. | Unenforced | **None**. | Postings routed to checked-out or invalid room numbers. |

---

## 9. Data-Model, Concurrency, Security & Testing Gaps

### 9.1 Missing Data Model Aggregates
The target domain specification (`DOMAIN_MODEL.md`) contains robust aggregate roots for F&B (`Order`, `Bill`, `MenuItem`, `StockItem`), but is **completely missing lodging aggregate roots**:
- `RoomType` (Aggregate Root)
- `Room` (Aggregate Root)
- `Reservation` (Aggregate Root)
- `GuestProfile` (Aggregate Root)
- `Folio` (Aggregate Root)
- `HousekeepingTask` (Entity)

### 9.2 Concurrency & Transactional Risks
- **Offline Room Allocation Race Condition**: Lacking cloud lock managers, two offline terminals can assign the same room simultaneously.
- **Cross-Outlet Non-Atomic Postings**: If a POS terminal posts a dining bill to a room folio via an HTTP API call without a distributed transaction or Outbox event, a network drop between F&B POS and Hotel API could finalize the restaurant order without creating the corresponding folio debit entry.

### 9.3 Security & PII Protection Gaps
- **Unencrypted Guest KYC Storage**: Legacy schema stores guest names in plain text. Target architecture must enforce AES-256 field-level encryption for sensitive KYC data (Aadhaar numbers, Passport details, ID scans) under Indian DPDP Act compliance.

### 9.4 Comprehensive Testing Gaps
- **Unit Tests**: 0 unit tests exist for hotel domain logic.
- **Integration Tests**: 0 integration tests for room availability or folio balance calculation.
- **UI Verification**: 0 Blazor Hybrid UI components exist for Front Desk or Housekeeping screens.

---

## 10. Recommended Dependency-Ordered Follow-Up Tasks

To establish hotel and hospitality operations as a fully functional, configurable capability in the modernized SaaS platform, the following dependency-ordered development roadmap is recommended:

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│ Epic H1: Hotel Property & Room Catalog Specification                            │
│ (Define RoomType, Room, RatePlan aggregate roots in DOMAIN_MODEL.md)            │
└───────────────────────────────────────┬─────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│ Epic H2: Core Front Desk & Guest Identity Engine                                │
│ (GuestProfile aggregate, KYC document storage, Walk-in Check-in FSM)            │
└───────────────────────────────────────┬─────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│ Epic H3: Reservation & Booking Management Engine                                │
│ (Reservation aggregate, Availability Matrix, Double-Booking Prevention FSM)     │
└───────────────────────────────────────┬─────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│ Epic H4: Guest Folio, Billing & Cross-Outlet Settlement Engine                  │
│ (Folio aggregate, RoomCredit POS integration, Lodging GST SAC 996311 engine)   │
└───────────────────────────────────────┬─────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│ Epic H5: Housekeeping & Room Maintenance Management                             │
│ (HousekeepingTask entity, Room Status real-time SignalR updates)                 │
└───────────────────────────────────────┬─────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│ Epic H6: Hotel Analytics, Yield & Statutory Reporting                            │
│ (RevPAR/ADR analytical queries, QuestPDF C-Form Police Export templates)         │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Dependency-Ordered Task Sequence:

1. **Task H1.1 (Domain Model Expansion)**: Update `DOMAIN_MODEL.md` to formally define `RoomType`, `Room`, `Reservation`, `GuestProfile`, and `Folio` aggregate roots, entities, value objects, and domain events.
2. **Task H1.2 (Database DDL & EF Core Mappings)**: Add PostgreSQL migration DDL in `schema_extracted/postgres_schema.sql` and EF Core DbContext entity configurations for lodging tables with mandatory `TenantId` and `LocationId` query filters.
3. **Task H2.1 (Guest Identity & KYC Engine)**: Implement `GuestProfile` MediatR command handlers, AES-256 field encryption for Passport/Aadhaar PII data, and Indian Police C-Form export builder.
4. **Task H3.1 (Reservation & Availability Engine)**: Implement `Reservation` finite state machine, date-range availability validation service, and double-booking guard clauses.
5. **Task H4.1 (Guest Folio & Cross-Outlet Billing)**: Implement `Folio` aggregate with double-entry ledger logic, lodging GST tax slab calculator, and `PaymentMethod.RoomCredit` POS posting command handler.
6. **Task H5.1 (Housekeeping SignalR Engine)**: Implement real-time room status update pipeline broadcasting `RoomStatusChangedEvent` via SignalR to Front Desk and Housekeeping Blazor UI components.
7. **Task H6.1 (QuestPDF Lodging Reports)**: Implement QuestPDF report document generators for Hotel Manager Daily Flash, Folio Tax Invoices, and Police C-Form exports.

---

## 11. Report Verification & Evidence Index

This audit report has been compiled and verified against physical repository evidence:
- **Legacy Primary Database**: `RSS26/dinurss.mdb` (105 user tables inspected; zero lodging tables present).
- **Extracted PostgreSQL Schema**: `schema_extracted/postgres_schema.sql` (1,424 DDL lines searched; zero room/folio entities present).
- **Target Architectural Specs**: `SYSTEM_ARCHITECTURE.md`, `DOMAIN_MODEL.md`, `SUBSCRIPTION_ARCHITECTURE.md`, `docs/SAAS_ARCHITECTURE.md`, `docs/BUSINESS_LOGIC.md`, `docs/verification/instance-10-modular-edition-audit.md`.
- **Custom Verification Tools**: `/home/jules/self_created_tools/audit_hotel_refs.py`.
