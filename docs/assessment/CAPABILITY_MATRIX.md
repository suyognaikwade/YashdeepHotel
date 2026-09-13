# Product Capability Assessment Matrix

**Repository**: `https://github.com/suyognaikwade/YashdeepHotel`
**Target Branch**: `botify`
**Classification Baseline**: Standardized 9-State Classification Scheme

---

## Standardized Classifications

1. **Implemented and verified**: Code exists, is functional, and is verified by passing automated tests or evidence.
2. **Partially implemented**: Code or schema exists but is incomplete or lacks key functionality/tests.
3. **Prototype or experimental**: Code exists as a temporary spikes, mock, or unvalidated prototype.
4. **Documented but not implemented**: Detailed specifications exist in documentation, but zero source code exists in the repository.
5. **Referenced but not found**: Mentioned in code, configuration, or documentation but missing from repository.
6. **Missing**: Required capability is completely absent from code, schema, and documentation.
7. **Blocked by an unresolved decision**: Cannot be implemented until product owner resolves a pending decision.
8. **Contradictory**: Conflicting requirements or implementations exist across documentation or code.
9. **Unable to verify**: Evidence is incomplete, obfuscated, or inaccessible.

---

## Complete Capability Assessment Table

| Capability / Requirement | Current Classification | Repository Evidence & File Reference | Missing Functionality & Gaps | Key Risks & Dependencies | Suggested Epic | Priority | Test Coverage |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **1. Multi-Tenant Platform Isolation** | **Partially implemented** | `schema_extracted/postgres_schema.sql` (lines 15-40), `docs/SAAS_ARCHITECTURE.md` | EF Core Global Query Filters, tenant resolution middleware, JWT tenant claim context. | Data leakage risk between tenants without ASP.NET middleware. | EPIC-01 (Tenant Foundations) | P0 (Critical) | Untested |
| **2. Multi-Branch Hierarchy & Scope** | **Documented but not implemented** | `docs/SAAS_ARCHITECTURE.md`, `SYSTEM_ARCHITECTURE.md` | `BranchId` / `LocationId` scope enforcement in C# application services. | Cross-branch data bleeding. | EPIC-01 (Tenant Foundations) | P0 (Critical) | Untested |
| **3. Offline-First Local Engine** | **Documented but not implemented** | `OFFLINE_ARCHITECTURE.md`, `SYSTEM_ARCHITECTURE.md` | Local SQLite DbContext, SQLCipher encryption layer, edge transaction processing. | System operational failure during network outage. | EPIC-02 (Offline Engine) | P0 (Critical) | Untested |
| **4. Weekly Mandatory Connectivity Enforcement** | **Documented but not implemented** | `docs/assessment/IMPLEMENTATION_BASELINE_AND_GAP_ASSESSMENT.md` | 7-day timer, clock tampering detection, grace period state machine, restricted read-only lock. | License compliance failure or unexpected lockout. | EPIC-03 (Entitlements) | P0 (Critical) | Untested |
| **5. Outbox/Inbox Synchronization** | **Documented but not implemented** | `SYSTEM_ARCHITECTURE.md`, `OFFLINE_ARCHITECTURE.md` | Local Outbox queue worker, Cloud Inbox idempotent batch endpoint, conflict handler. | Data desynchronization, lost KOTs or bills. | EPIC-04 (Sync Engine) | P0 (Critical) | Untested |
| **6. Ed25519 Subscription Entitlements** | **Documented but not implemented** | `ENTITLEMENT_MODEL.md`, `SUBSCRIPTION_ARCHITECTURE.md` | Cloud Ed25519 signing service, edge client public key validation, entitlement payload reader. | Unauthorized feature access or expired usage. | EPIC-03 (Entitlements) | P0 (Critical) | Untested |
| **7. Device Registration & Trust** | **Documented but not implemented** | `DEVICE_MANAGEMENT.md`, `SECURITY_ARCHITECTURE.md` | Hardware fingerprint generator (CPU/MB/MAC), registration handshake API, revocation listener. | Spoofed devices accessing tenant DB. | EPIC-05 (Device Security) | P1 (High) | Untested |
| **8. Modular Edition Setup & Capabilities** | **Documented but not implemented** | `SUBSCRIPTION_ARCHITECTURE.md`, `SYSTEM_ARCHITECTURE.md` | Capability flags (Bar/Rest vs Hotel), first-run setup wizard, module enablement guards. | UI showing inactive features. | EPIC-03 (Entitlements) | P1 (High) | Untested |
| **9. Section Seating & Rate Matrix** | **Documented but not implemented** | `LEGACY_SYSTEM_ANALYSIS.md`, `BUSINESS_LOGIC.md` | Section entity, table status tracker (Vacant, Occupied, Billed), section-wise rate resolution. | Incorrect pricing applied to bills. | EPIC-06 (Restaurant POS) | P0 (Critical) | Untested |
| **10. Bilingual KOT & BOT Routing** | **Documented but not implemented** | `BUSINESS_LOGIC.md`, `PRINTING_ARCHITECTURE.md` | Order entry handler, Devanagari/Marathi translation lookup, kitchen/bar printer router. | Incorrect order sent to kitchen. | EPIC-06 (Restaurant POS) | P0 (Critical) | Untested |
| **11. Billing, Split Taxes & Dynamic UPI** | **Documented but not implemented** | `BILLING_ARCHITECTURE.md`, `BUSINESS_LOGIC.md` | Tax calculation engine (CGST/SGST + VAT), dynamic UPI QR string generator, bill settlement. | Tax compliance violations. | EPIC-07 (Billing & Taxes) | P0 (Critical) | Untested |
| **12. Multi-Tier Liquor Stock Tracking** | **Documented but not implemented** | `INVENTORY_ARCHITECTURE.md`, `LEGACY_SYSTEM_ANALYSIS.md` | Godown -> Counter -> Loose ML auto-conversion engine, peg deduction logic (30/60/90ml). | Stock variance and Excise audit failure. | EPIC-08 (Liquor Inventory) | P0 (Critical) | Untested |
| **13. Maharashtra FL-III Excise Compliance** | **Documented but not implemented** | `EXCISE_ARCHITECTURE.md`, `BUSINESS_LOGIC.md` | Permit holder register, FrmDailyBulkLitre summary generator, Excise Monthly Register 1. | Statutory fine or license suspension. | EPIC-09 (Excise Compliance) | P0 (Critical) | Untested |
| **14. Thermal ESC/POS Printing** | **Documented but not implemented** | `PRINTING_ARCHITECTURE.md`, `BILLING_ARCHITECTURE.md` | ESC/POS byte generator, USB/LAN/Bluetooth print driver, paper-out fallback handler. | Failed bill printing during peak hours. | EPIC-10 (Printing Engine) | P0 (Critical) | Untested |
| **15. Day End Settlement & Rollover** | **Documented but not implemented** | `LEGACY_SYSTEM_ANALYSIS.md`, `BUSINESS_LOGIC.md` | Table clearance check, business date advancing, closing stock calculation, financial snapshot. | Unclosed daily accounts or incorrect opening stock. | EPIC-11 (Day End & Audit) | P0 (Critical) | Untested |
| **16. Hotel Room & Reservation Engine** | **Documented but not implemented** | `DOMAIN_MODEL.md`, `SYSTEM_ARCHITECTURE.md` | Room type, room status grid, reservation calendar, check-in / check-out, guest ledger. | Double booking, lost guest billing. | EPIC-12 (Hotel Management) | P2 (Medium) | Untested |
| **17. Guest Management & Housekeeping** | **Documented but not implemented** | `DOMAIN_MODEL.md` | Guest profile, ID proof attachment, housekeeping task assignment, room cleaning status. | Operational delays in room turnover. | EPIC-12 (Hotel Management) | P2 (Medium) | Untested |
| **18. Access MDB Data Migration Pipeline** | **Documented but not implemented** | `MIGRATION_ARCHITECTURE.md`, `schema_extracted/` | Jet 4.0 OleDb reader, data transformation ETL, reconciliation engine, dry-run mode. | Corrupted historical data during onboarding. | EPIC-13 (Data Migration) | P1 (High) | Untested |
| **19. Automatic Client Updater** | **Documented but not implemented** | `SYSTEM_ARCHITECTURE.md`, `SECURITY_ARCHITECTURE.md` | Velopack driver, signature verification, background download, atomic install & rollback. | Failed client update breaking API compatibility. | EPIC-14 (Client Updater) | P1 (High) | Untested |
| **20. QuestPDF Financial Reporting** | **Documented but not implemented** | `REPORTING_ARCHITECTURE.md`, `BILLING_ARCHITECTURE.md` | QuestPDF templates for Itemwise Sales, Tax Register, Ledger, Excise Returns, PDF exporter. | Missing financial insights. | EPIC-15 (Reporting) | P1 (High) | Untested |
| **21. Security Monitoring & Audit Logging** | **Documented but not implemented** | `SECURITY_ARCHITECTURE.md`, `SYSTEM_ARCHITECTURE.md` | Structured audit log emitter, security event listener, local tamper detection log. | Unchecked administrative abuse. | EPIC-05 (Device Security) | P1 (High) | Untested |
| **22. Legacy Access Binary Codebase** | **Implemented and verified** | `RSS26/RSS.exe`, `dinurss.mdb`, `nwitem5.vb` | Deprecated WinForms application, legacy OleDb connection string, Crystal Reports templates. | Jet locking (`0x80004005`), single-point failure. | Deprecated / Reference | N/A | Untested |

---

## Major Implementation Gaps Summary

1. **Complete Absence of C# Codebase**: While schema DDLs and exhaustive architecture specifications are complete, there is currently **no executable .NET 9 solution or project** in `src/`.
2. **Missing Outbox & Synchronization Engine**: The entire offline synchronization mechanism relies on C# Outbox worker classes that need to be constructed.
3. **Missing Desktop / Mobile UI**: No Blazor components, Razor pages, or MAUI view handlers are implemented yet.
4. **Missing Test Suite**: Zero unit, integration, sync, or UI tests are present in the repository.

---

*Verified against `botify` branch commit baseline.*
