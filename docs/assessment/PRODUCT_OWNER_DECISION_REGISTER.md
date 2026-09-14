# Product Owner Decision Register

**Repository**: `https://github.com/suyognaikwade/YashdeepHotel`
**Target Branch**: `botify`
**Purpose**: Document mandated product decisions, recommended architectural defaults, and open decisions requiring explicit product owner approval.

---

## 1. Mandated Product Policies (Non-Negotiable Baseline)

The following policies are authoritative baseline requirements for the Yashdeep modernization project:

1. **Access / MDB Dependency Prohibition**: Microsoft Access (`dinurss.mdb`), Jet 4.0, and MDB binaries are strictly **migration and reference sources**. They must **never** be used as production runtime databases or dependencies.
2. **Offline-First Resilience**: Edge POS terminals (Windows and Android) must operate 100% autonomously without requiring continuous internet connectivity for core POS, KOT, billing, thermal printing, and stock decrements.
3. **Weekly Mandatory Connectivity**: Every registered device must connect to the internet at least once every **seven days (168 hours)** for synchronization, entitlement validation, device trust checks, and policy refresh.
4. **No Direct Client-to-Cloud Database Connections**: Clients must connect strictly via the Cloud API Gateway (`https://api.yashdeep.saas`). Direct connections from POS edge devices to PostgreSQL are strictly prohibited.
5. **No DevOps Restructuring in Task Scope**: Docker, Kubernetes, CI/CD pipeline restructuring, or infrastructure-as-code modifications are explicitly out of scope for this assessment task.

---

## 2. Decision Classification Register

### Category A: Mandated Decisions
| Decision ID | Domain | Policy Decision Summary | Rationale & Enforcement |
| :--- | :--- | :--- | :--- |
| **DEC-01** | Connectivity | Mandatory 7-day (168h) network check-in requirement for all active POS devices. | Enforces SaaS subscription compliance, security policy refresh, and data sync. |
| **DEC-02** | Database | Local SQLite + SQLCipher AES-256 for edge storage; PostgreSQL 16 for cloud central. | Guarantees offline performance and multi-tenant security. |
| **DEC-03** | Security | Token signing using Ed25519 (EdDSA) cryptographic key pairs. | High-performance, lightweight signature verification on low-power POS devices. |
| **DEC-04** | Security | Zero production client credentials stored in plaintext. | Documentation security policy compliance. |
| **DEC-05** | Printing | ESC/POS raw thermal streams + QuestPDF code-first report templates. | Eliminates legacy Crystal Reports dependency and WinForms GDI+ print deadlocks. |

---

### Category B: Recommended Technical Defaults (Pending PO Ratification)
| Decision ID | Domain | Recommended Default | Alternative Option | Impact if Changed |
| :--- | :--- | :--- | :--- | :--- |
| **DEC-06** | Connectivity | **7-Day Soft Grace Period** after 7-day offline expiration before triggering hard read-only lock (Total 14 days). | Immediate hard lock at Day 8 without grace period. | Softer user experience in rural locations during extended ISP outages. |
| **DEC-07** | Sync Engine | **Last-Write-Wins (LWW) with Server Timestamp Authority** for master data; **Additive Append-Only Outbox** for transactions. | Manual human conflict resolution UI for every sync collision. | Appends prevent transaction loss; LWW simplifies master data sync. |
| **DEC-08** | Entitlements | **Edition Capabilities (Starter, Pro, Enterprise)** controlled via signed JWT scope claims. | Dynamic feature flags fetched via live HTTP on startup. | Allows offline capability evaluation without requiring online server checks at boot. |
| **DEC-09** | Updates | **Velopack Auto-Updater** with background download and deferred install on application restart. | Mandatory immediate app restart on update download. | Prevents app restarts in the middle of active dining rush hours. |
| **DEC-10** | Migration | **Dry-Run ETL Execution Mode with Dual Ledger Reconciliation** prior to live tenant onboarding. | Direct one-pass database import without pre-flight reconciliation. | Guarantees zero financial or inventory discrepancy during Access -> PostgreSQL cutover. |

---

### Category C: Open Decisions Requiring Explicit Product Owner Approval
| Decision ID | Domain | Open Question / Decision Required | Option 1 | Option 2 | Impact & Recommendation |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **DEC-11** | Offline POS | What is the policy for offline UPI / Credit payment recording when network is disconnected? | Allow operator to manually record offline UPI reference string and settle bill. | Block UPI payment option when offline; force Cash only. | **Recommend Option 1**: Retail operations cannot halt due to internet drops. Warn operator on UI. |
| **DEC-12** | Entitlements | What is the exact behavior when an organization downgrades its subscription tier? | Automatically disable access to higher-tier UI modules on next sync. | Allow current billing cycle to complete before disabling modules. | **Recommend Option 2**: Prevents sudden mid-day operational disruptions. |
| **DEC-13** | Inventory | Are inter-branch stock transfers allowed to auto-approve or do they require receiving branch confirmation? | Require receiving branch manager approval before stock enters recipient inventory ledger. | Auto-approve stock transfer upon dispatch from source branch. | **Recommend Option 1**: Standard audit trail requirement for multi-location liquor compliance. |
| **DEC-14** | Hotel vs POS | Should single-property clients running "Bar & Restaurant Only" edition be able to upgrade to "Hotel + Bar" without re-installing client software? | Yes, modular single-binary architecture enables Hotel features upon license refresh. | No, separate installer builds for Hotel vs Restaurant editions. | **Recommend Option 1**: Modular monorepo design supports dynamic module enablement. |
| **DEC-15** | Audit Logs | What is the mandatory retention period for State Excise audit logs and transaction history? | 7 Years (84 Months) in cloud cold storage. | 3 Years (36 Months). | **Recommend Option 1**: Maharashtra State Excise statutory inspection regulations require long-term record preservation. |

---

*Decision Register maintained for Product Owner review and formal sign-off.*
