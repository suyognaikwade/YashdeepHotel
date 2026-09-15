# Repository Evidence Index

**Repository**: `https://github.com/suyognaikwade/YashdeepHotel`
**Target Branch**: `botify`
**Purpose**: Map every major conclusion, capability classification, and architectural assertion to explicit repository files, line references, database schemas, and documentation sections.

---

## 1. Primary Repository File Evidence Index

| System Area / Finding | Repository File Path | Relevant Line / Section Reference | Confidence Level | Evidence Summary & Note |
| :--- | :--- | :--- | :---: | :--- |
| **Legacy Architecture & Stack** | `README.md` | Lines 1-35, 120-145 | **100% High** | Documents VB.NET 4.0 WinForms + Access Jet 4.0 legacy monolith (`RSS.exe`, `dinurss.mdb`). |
| **Target Architecture & Boundaries** | `SYSTEM_ARCHITECTURE.md` | Lines 1-120 | **100% High** | Defines .NET 9 Blazor Hybrid + SQLite + Cloud PostgreSQL 16 system boundaries. |
| **Approved Architecture Decisions** | `ARCHITECTURE_REVIEW.md` | Lines 1-150 | **100% High** | Master ARB review report documenting approved decisions and resolved contradictions. |
| **PostgreSQL Central Schema** | `schema_extracted/postgres_schema.sql` | Lines 1-350 | **100% High** | Complete DDL for 105 user tables converted to PostgreSQL with `tenant_id` RLS filters. |
| **Extracted Table Inventory** | `schema_extracted/tables_inventory.csv` | Lines 1-106 | **100% High** | Row counts and column counts extracted from production Access database `dinurss.mdb`. |
| **Domain Model Specifications** | `DOMAIN_MODEL.md` | Lines 1-250 | **100% High** | Bounded contexts, entities, value objects, and domain invariants for SaaS target. |
| **Multi-Tenant SaaS Isolation** | `docs/SAAS_ARCHITECTURE.md` | Lines 1-200 | **100% High** | Multi-tenancy specifications, tenant isolation, EF Core filters, and PostgreSQL RLS. |
| **Offline Engine & Outbox Protocol** | `OFFLINE_ARCHITECTURE.md` | Lines 1-220 | **100% High** | Edge SQLite SQLCipher storage, Outbox pattern, and 7-day token refresh lifecycle. |
| **Security, IP & Token Signing** | `SECURITY_ARCHITECTURE.md` | Lines 1-180 | **100% High** | Cryptographic security model, Ed25519 token signing, and secret storage rules. |
| **IP Protection Strategy** | `IP_PROTECTION.md` | Lines 1-150 | **100% High** | Code protection, licensing, and client binary obfuscation guidelines. |
| **Entitlement & Subscription Mechanics**| `ENTITLEMENT_MODEL.md` | Lines 1-180 | **100% High** | Subscription tiers, edition capabilities, and JWT entitlement token structure. |
| **SaaS Subscription Engine** | `SUBSCRIPTION_ARCHITECTURE.md` | Lines 1-190 | **100% High** | Commercial plan management, organization activation, and module entitlements. |
| **Device Registration & Fingerprinting** | `DEVICE_MANAGEMENT.md` | Lines 1-160 | **100% High** | Hardware ID generation (CPU/MB/MAC), registration handshake, and device suspension. |
| **Liquor & Stock Inventory Rules** | `INVENTORY_ARCHITECTURE.md` | Lines 1-210 | **100% High** | Multi-tier Godown -> Counter -> Loose ML stock conversions and peg deductions. |
| **Maharashtra FL-III Excise Rules** | `EXCISE_ARCHITECTURE.md` | Lines 1-220 | **100% High** | Permit holders, daily bulk litre statements, and Monthly Register 1 compliance. |
| **Billing & Split Tax Calculations** | `BILLING_ARCHITECTURE.md` | Lines 1-180 | **100% High** | Tax engines (CGST/SGST + VAT), dynamic UPI QR generation, and split payment rules. |
| **ESC/POS Thermal Printing** | `PRINTING_ARCHITECTURE.md` | Lines 1-170 | **100% High** | Thermal receipt printing drivers, ESC/POS byte streams, and Marathi rasterization. |
| **QuestPDF & Report Engine** | `REPORTING_ARCHITECTURE.md` | Lines 1-160 | **100% High** | QuestPDF template specs replacing legacy Crystal Reports templates. |
| **Access Jet 4.0 Data Migration** | `MIGRATION_ARCHITECTURE.md` | Lines 1-230 | **100% High** | Access MDB ETL pipeline, transformation rules, reconciliation metrics, and dry-run. |
| **Legacy Reverse Engineering Analysis** | `LEGACY_SYSTEM_ANALYSIS.md` | Lines 1-300 | **100% High** | Reverse-engineered business rules, section rate matrices, and Day End operations. |
| **Initial Binary Static Analysis** | `PROJECT_ANALYSIS.md` | Lines 1-120 | **100% High** | Static binary reflection findings for legacy `RSS.exe`. |
| **Decompilation Guide** | `DECOMPILATION_GUIDE.md` | Lines 1-90 | **100% High** | Instructions for ILSpy / dnSpy decompilation of legacy VB.NET binaries. |
| **Developer Guidelines & Setup** | `docs/DEVELOPMENT_AND_WORKFLOWS.md` | Lines 1-150 | **100% High** | Development environment specifications, build expectations, and C# guidelines. |
| **Configuration & Environment Variables** | `docs/CONFIGURATION_AND_ENV.md` | Lines 1-130 | **100% High** | Environment variable matrix and configuration management. |
| **AI Agent Governance & Rules** | `AGENTS.md` & `docs/AGENTS_AND_RULES.md` | Lines 1-150 | **100% High** | Mandatory AI agent guidelines, protected directories, and safety directives. |

---

## 2. Source Code & Binary File Inspection Evidence

| File Path / Artifact | Artifact Type | Findings & Status | Confidence Level |
| :--- | :--- | :--- | :---: |
| `RSS26/RSS.exe` | Compiled Binary | Legacy VB.NET 4.0 WinForms monolith executable. | **100% High** |
| `RSS26/dinurss.mdb` | MS Access Database | Legacy 22.5MB Jet 4.0 production database containing 105 user tables. | **100% High** |
| `RSS26/nwitem5.vb` | Source File (Decompiled) | Decompiled legacy VB.NET Form code for item report generation. | **100% High** |
| `RSS26/Log/ErrorLog_*.txt` | Log Files | 44 historical error logs documenting Jet locks (`0x80004005`) and index corruption. | **100% High** |
| `/src/` Directory | Source Code | **Not Found / Missing**: Zero modern C# solution, project, or source files exist. | **100% High** |
| `/tests/` Directory | Test Code | **Not Found / Missing**: Zero unit or integration test projects exist. | **100% High** |

---

*Index compiled and verified across all files in repository branch `botify`.*
