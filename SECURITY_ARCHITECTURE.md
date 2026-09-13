# Security Architecture Specification
## Yashdeep Hotel Management System — SaaS Platform & Edge POS

---

## 1. Executive Summary & Commercial Non-Negotiables

This document specifies the end-to-end Security Architecture for the modernized **Yashdeep Hotel Management System**, an offline-first, cloud-synchronized SaaS platform built on .NET 9, ASP.NET Core Web API, Blazor Hybrid / MAUI edge POS clients, local SQLite databases, and cloud PostgreSQL databases.

### 1.1 Commercial Non-Negotiable Requirements

By fundamental commercial policy and architecture design, **customer-controlled edge devices (Windows PCs, Android tablets, touchscreen POS terminals, handheld devices) MUST NEVER receive, store, or process any of the following assets**:

| Secret / Asset Type | Risk Exposure if Leaked | Architectural Mitigation & Isolation Guarantee |
| :--- | :--- | :--- |
| **Cloud Database Credentials** | Full unauthorized access to multi-tenant cloud PostgreSQL database. | **Strict Isolation**: Edge devices interact *exclusively* with the Cloud API via HTTPS/WSS. Cloud DB connection strings (`ConnectionStrings__PostgreSQL`) reside strictly inside cloud environment variables / Cloud Key Vault. |
| **Master Encryption Keys** | Compromise of all encrypted tenant data at rest across the entire platform. | **Key Separation**: Cloud master keys (KMS Root Keys) never leave Cloud Key Vault / HSM. Edge device keys are generated locally on device activation and locked to the device's OS Secure Enclave / DPAPI. |
| **Cloud Administrator Credentials** | Complete administrative control over SaaS infrastructure, tenancy, and billing. | **IAM Boundary**: Cloud infrastructure credentials exist strictly in cloud IAM systems with mandatory Multi-Factor Authentication (MFA) and IP whitelisting. |
| **JWT Signing Secrets** | Ability to forge arbitrary JWT tokens, impersonate any user or tenant across the platform. | **Asymmetric Signing (RS256 / ES256)**: Cloud API signs tokens using a private key stored in Cloud Key Vault. Clients receive short-lived access tokens and only use public keys (JWKS) to verify signature validity if needed. Private signing key NEVER touches client devices. |
| **Private Service Credentials** | Unauthorized access to payment gateways, SMS gateways, tax/excise APIs, and email servers. | **Backend Proxying**: All third-party service calls (UPI payment verification, SMS receipt dispatch, Excise portal submissions) are processed exclusively by Cloud API microservices using backend secret stores. |
| **Inter-Service mTLS Keys** | Impersonation of internal microservices or event buses. | **Zero-Trust Network Architecture**: Internal service communication is constrained within cloud VPC / private cluster networks. |

---

## 2. Security Boundaries & Threat Modeling

### 2.1 System Trust Zones

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│ ZONE 0: Cloud Trust Zone (Fully Trusted)                                        │
│  - Cloud API Microservices (.NET 9 Web API)                                    │
│  - Primary Cloud Database (PostgreSQL 16 + Row-Level Security)                 │
│  - Cloud Secret Manager (Azure Key Vault / HashiCorp Vault / AWS KMS)           │
│  - JWT Asymmetric Private Key & Cloud Audit Store                              │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        ▲
                                        │ HTTPS (TLS 1.3 + Certificate Pinning)
                                        │ mTLS / Hardware Device Signatures
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│ ZONE 1: Untrusted Transport & Boundary (Public Internet / Cellular / Local LAN)  │
│  - Wi-Fi Networks, Router Hardware, Public Internet Routers                     │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        ▲
                                        │ HTTPS / WSS / Outbox Sync Client
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│ ZONE 2: Customer Device Boundary (Untrusted Endpoint Hardware)                  │
│  - Customer Windows PC / Android Tablet / Handheld POS Terminal                 │
│  - Local Encrypted SQLite (SQLCipher via OS DPAPI / Android Keystore Key)        │
│  - Blazor Hybrid App Runtime & Outbox Sync Queue                                │
│  - Local ESC/POS Thermal Printers & Barcode Scanners                            │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

### 2.2 Threat Model (STRIDE)

| STRIDE Category | Threat Description | Attack Vector | Severity | Architectural Mitigation |
| :--- | :--- | :--- | :---: | :--- |
| **Spoofing** | Rogue device impersonates a legitimate POS terminal to inject fake sales/stock records. | Compromised device activation key or stolen local token. | **High** | Unique Device Identity Certificates (X.509), hardware fingerprinting, short-lived JWTs, and mandatory tenant/device binding on every API request. |
| **Tampering** | Operator tampers with local SQLite DB to erase bills or alter Excise stock logs before sync. | Direct modification of `.db` file using SQLite tools on local disk. | **High** | SQLCipher database encryption with AES-256-CBC, local HMAC-SHA256 record integrity hashes, and cloud Outbox audit gap detection. |
| **Repudiation** | Cashier voids a bill or grants a discount, then denies performing the action. | Missing or falsified audit logs. | **Medium** | Immutable append-only audit logging with digital signatures on every bill mutation, recorded locally and synced to cloud append-only ledger. |
| **Information Disclosure** | Competitor or thief steals physical POS tablet to extract customer PII, sales history, or Excise records. | Cold boot, physical storage extraction, or raw disk dumping. | **High** | SQLCipher database encryption using hardware-backed OS key storage (DPAPI / Keystore), zero plaintext storage on disk, and immediate remote kill-switch wipe capabilities. |
| **Denial of Service** | Malicious local node spams Outbox Sync endpoint with huge payloads to freeze cloud server. | Replay attack or sync endpoint flooding. | **Medium** | Cloud API rate limiting (IP & Tenant/Device throttles), request size limits, payload schema validation, and idempotency key checks. |
| **Elevation of Privilege** | Waiter/Captain uses client app manipulation to gain Manager/Admin rights and alter rates. | Memory editing, client-side UI flag toggling, or API parameter tampering. | **Critical** | Server-authoritative authorization. All privilege checks are strictly enforced by the API; local UI state toggles have zero effect on backend operations. |

---

### 2.3 Offline Attack Model & Realistic Edge Protection Limits

On a customer-controlled device, physical access gives an attacker complete control over hardware, memory, and disk. The security architecture explicitly documents **what CAN and CANNOT realistically be protected on an edge device**:

```
REALISTIC EDGE PROTECTION BOUNDARIES:

┌──────────────────────────────────────────────────┐  ┌──────────────────────────────────────────────────┐
│ WHAT CAN REALISTICALLY BE PROTECTED              │  │ WHAT CANNOT REALISTICALLY BE PROTECTED           │
├──────────────────────────────────────────────────┤  ├──────────────────────────────────────────────────┤
│ 1. Protection of data at rest when power is OFF  │  │ 1. Live process memory against root/admin memory │
│    via SQLCipher + OS DPAPI / Android Keystore.  │  │    dumping while app is running and unlocked.    │
│ 2. Protection against casual network sniffing     │  │ 2. Prevention of reverse-engineering of client   │
│    and MitM attacks via TLS 1.3 & Cert Pinning.  │  │    binary code (Native AOT raises cost, but      │
│ 3. Prevention of unauthorized device pairing     │  │    disassembly is always possible given time).   │
│    via Tenant-approved TOTP activation tokens.   │  │ 3. Fraudulent physical user inputs performed by   │
│ 4. Isolation of tenant cloud data (never stored   │  │    legitimate logged-in users sharing credentials.│
│    on client device beyond local scope).         │  │ 4. Prevention of physical screen capturing or    │
│ 5. Instant remote device lockout & wipe signal    │  │    hardware bus tapping on unlocked screens.     │
│    dispatch upon device loss report.             │  │                                                  │
└──────────────────────────────────────────────────┘  └──────────────────────────────────────────────────┘
```

---

## 3. Authentication Architecture

### 3.1 Dual-Tier Identity Strategy

The platform employs a two-layer authentication architecture to support seamless offline operation while maintaining strict cloud security:

```
                            ┌───────────────────────────────────────┐
                            │          USER AUTHENTICATION          │
                            └───────────────────────────────────────┘
                                                │
                       ┌────────────────────────┴────────────────────────┐
                       ▼                                                 ▼
        [ ONLINE AUTHENTICATION ]                             [ OFFLINE AUTHENTICATION ]
  - User submits credentials to Cloud API               - User enters User PIN / Local Password
  - Authenticated via ASP.NET Core Identity             - Validated against locally stored Argon2id
  - Receives short-lived JWT Access Token                hash stored in encrypted SQLCipher DB
  - Token signed via Cloud RS256 Private Key            - Session bound to locally authenticated role
```

---

### 3.2 Cloud OAuth2 / OIDC + PKCE Authentication Flow

1. **Client Identity**: Web Admin and Mobile/Desktop clients authenticate using OAuth 2.0 with Proof Key for Code Exchange (PKCE) (RFC 7636).
2. **Identity Provider**: ASP.NET Core Identity integrated with OpenIddict / Duende IdentityServer on the Cloud API.
3. **Password Security**: Passwords hashed using Argon2id (Memory: 64MB, Iterations: 3, Parallelism: 4) or PBKDF2 with HMAC-SHA256 (600,000 iterations).

---

### 3.3 Device Authentication & Hardware Binding

Every edge POS device must be individually authenticated and bound to physical hardware before participating in sync operations:

1. **Hardware Fingerprint Generation**:
   - Windows: SHA-256 digest of (`CPU Processor ID` + `Motherboard UUID` + `System Drive Serial`).
   - Android: Hardware-backed `ANDROID_ID` combined with KeyStore hardware-backed attestation key.
2. **Device Identity Certificate**:
   - Device generates an RSA 2048-bit or ECC P-256 keypair inside OS KeyStore (DPAPI/Keyring).
   - Public Key CSR submitted during registration.
   - Cloud Device CA issues a X.509 Device Identity Certificate (`CN=Device-{DeviceId}, OU=Tenant-{TenantId}`).
3. **API Mutual Authentication**:
   - Every HTTPS sync request includes the Device Identity Certificate (mTLS) or an `X-Device-Signature` HTTP header signed with the device private key:
     `X-Device-Signature: t={timestamp}, nonce={nonce}, sig=HMAC-SHA256(device_private_key, method + uri + timestamp + nonce + body_hash)`

---

## 4. Authorization & Role-Based Access Control (RBAC)

### 4.1 Role Hierarchy & Permissions Matrix

The system enforces fine-grained Role-Based Access Control across Hotel and Maharashtra State Excise FL-III Bar workflows:

| Role | Target Users | Allowed Operations | Forbidden Operations |
| :--- | :--- | :--- | :--- |
| **SaaS Platform Admin** | SaaS System Operators | Global system monitoring, tenant provisioning, global license management. | Accessing raw customer hotel transaction data without explicit audit approval. |
| **Tenant Admin / Hotel Owner** | Hotel Owner / MD | Full tenant configuration, menu rate updates, staff user creation, Excise registers, Day End close approval, financial reports. | Accessing other tenants' data. |
| **Manager** | Restaurant / Bar Manager | Order management, bill creation, discount approval (up to policy limit), bill split, Day End closing execution, stock transfer. | Modifying staff roles, changing system encryption keys, deleting audit logs. |
| **Cashier** | Billing Counter Staff | Table order viewing, bill generation, payment collection (Cash/Card/UPI), receipt printing, daily cash register balancing. | Approving discounts above limit, voiding settled bills without Manager PIN, editing menu prices. |
| **Captain / Order Taker** | Floor Captain / Steward | Table selection, KOT/BOT creation, item additions, KOT cancellation (before kitchen print), table shift/merge. | Settling payments, applying discounts, viewing daily revenue totals, altering item prices. |
| **Waiter** | Service Staff | Order viewing, table status viewing, item delivery status update. | Creating KOTs (if limited by hotel policy), billing, discounts, cancellations. |
| **Kitchen / Bar Display** | Cook / Bartender | KOT / BOT order ticket view, mark item as preparing/ready. | Financial operations, billing, menu editing. |

---

### 4.2 Policy Enforcement Engine

ASP.NET Core Policy-Based Authorization decorates all API endpoints:

```csharp
// Example Authorization Policy Registration
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Permissions.Bills.Void", policy =>
        policy.RequireClaim("permission", "bills:void")
              .RequireClaim("tenant_id"));

    options.AddPolicy("Permissions.Excise.Read", policy =>
        policy.RequireClaim("permission", "excise:read")
              .RequireRole("TenantAdmin", "Manager"));
});
```

---

### 4.3 Multi-Tenant Isolation Architecture

Multi-tenancy isolation is enforced at every layer to prevent cross-tenant data leaks:

```
[ Incoming Request ] ──► [ JWT Validation Middleware ] ──► Extracts tenant_id Claim
                                                                   │
                                                                   ▼
                                                 [ EF Core Global Query Filter ]
                                                 WHERE TenantId = @currentTenantId
                                                                   │
                                                                   ▼
                                                 [ PostgreSQL Row-Level Security ]
                                                 CREATE POLICY tenant_isolation_policy ON table
                                                 USING (tenant_id = current_setting('app.current_tenant_id'));
```

1. **Global Query Filters**: Every DbContext entity implements `ITenantScoped`. EF Core automatically appends `WHERE TenantId = @currentTenantId` to all LINQ queries.
2. **PostgreSQL Row-Level Security (RLS)**: PostgreSQL enforces table-level RLS policies, ensuring that even raw SQL queries cannot read or write across tenant boundaries.

---

## 5. Device Lifecycle & Revocation Architecture

### 5.1 Device Registration & Enrollment Flow

```
   Edge Device (POS Terminal)                       Tenant Admin (Cloud Console)                 Cloud API / CA Engine
───────────────┬───────────────                  ───────────────┬───────────────               ───────────┬───────────
               │                                                │                                         │
               │  1. Request Registration Code                  │                                         │
               ├───────────────────────────────────────────────►│                                         │
               │                                                │  2. Generate One-Time Activation Token  │
               │                                                │     (16-digit TOTP, 15 min expiry)     │
               │                                                ├────────────────────────────────────────►│
               │  3. Enter Activation Token + Device Fingerprint│                                         │
               ├─────────────────────────────────────────────────────────────────────────────────────────►│
               │                                                │                                         │ 4. Validate Token & Tenant Limit
               │                                                │                                         │    Generate CSR Challenge
               │◄─────────────────────────────────────────────────────────────────────────────────────────┤
               │                                                │                                         │
               │  5. Generate RSA-2048 keypair in OS SecureStore│                                         │
               │     Submit CSR + Hardware Fingerprint Hash     │                                         │
               ├─────────────────────────────────────────────────────────────────────────────────────────►│
               │                                                │                                         │ 6. Issue X.509 Device Identity Cert
               │                                                │                                         │    Persist Device Record
               │◄─────────────────────────────────────────────────────────────────────────────────────────┤
               │                                                │                                         │
               │  7. Store Certificate in OS DPAPI / KeyStore   │                                         │
               │     Initialize Local SQLCipher DB with key     │                                         │
               ▼                                                ▼                                         ▼
```

---

### 5.2 Device Revocation & Instant Remote Kill-Switch

When a device is stolen, compromised, or decommissioned:

1. **Revocation Trigger**: Tenant Admin marks the device as `REVOKED` in Cloud Admin Console.
2. **Instant Blacklist**: Cloud API immediately adds Device ID & Certificate Serial Number to Redis distributed Revocation Cache.
3. **Push Kill Signal**:
   - **Active Connection**: Cloud sends immediate SignalR WebSocket revocation frame `CMD_DEVICE_WIPE`.
   - **Passive / Sync Request**: Next Outbox Sync HTTP request receives `403 Forbidden` with header `X-Action: TERMINATE_AND_WIPE`.
4. **Device Execution Payload**:
   - Device process immediately revokes active user session tokens.
   - Clears SQLCipher encryption key from OS DPAPI / Keystore.
   - Deletes local SQLite file (`local_pos.db`) and memory buffers.
   - Terminates runtime application process.

---

## 6. Token Lifecycle & Key Strategy

### 6.1 Token Lifecycle Matrix

| Token Type | Issuer | Life Span | Storage Location | Invalidation Strategy |
| :--- | :--- | :--- | :--- | :--- |
| **Cloud Access Token (JWT)** | Cloud API | 15 Minutes | In-Memory (Secure RAM buffer) | Short TTL; immediate blacklisting via Token Revocation List in Redis. |
| **Cloud Refresh Token** | Cloud API | 7 Days (Sliding) | OS DPAPI / Encrypted Secure Storage | Rotated on every refresh; blacklisted in Cloud DB upon logout/revocation. |
| **Offline Session Token** | Local Device | 8 Hours | Local RAM | Purged on app exit or shift logout. |
| **Device Identity Cert** | Cloud CA | 365 Days | OS DPAPI / Android Keystore / CNG | CRL / OCSP revocation check on every mTLS sync connection. |

---

### 6.2 Token Claims Structure

```json
{
  "iss": "https://api.yashdeephotel.com",
  "aud": "https://app.yashdeephotel.com",
  "sub": "usr_9841029348102938",
  "tenant_id": "tnt_mah_fl3_215144",
  "device_id": "dev_pos_counter_01",
  "role": "Cashier",
  "permissions": [
    "bills:create",
    "bills:read",
    "kot:create",
    "payments:collect"
  ],
  "iat": 1740000000,
  "exp": 1740000900,
  "jti": "jwt_nonce_881029341"
}
```

---

## 7. Encryption Architecture (At Rest & In Transit)

### 7.1 Encryption Matrix

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ DATA IN TRANSIT                                                                        │
├───────────────────┬────────────────────────────────────────────────────────────────────┤
│ TLS Protocol      │ Mandatory TLS 1.3 (Fallback to TLS 1.2 with AEAD ciphers only)    │
│ Cipher Suites     │ TLS_AES_256_GCM_SHA384, TLS_CHACHA20_POLY1305_SHA256               │
│ Certificate Pin   │ SPKI Public Key Pinning enforced in Blazor Hybrid / MAUI client    │
│ Transport         │ HTTPS for Web API, WSS for SignalR WebSockets                      │
└───────────────────┴────────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────────────────────────────┐
│ DATA AT REST (CLOUD)                                                                   │
├───────────────────┬────────────────────────────────────────────────────────────────────┤
│ Database          │ PostgreSQL Transparent Data Encryption / Encrypted EBS (AES-256)   │
│ Field-Level (FLE) │ Credit Card Tokens, Customer Phone, Excise License PII encrypted   │
│                   │ via AES-256-GCM before database insertion                          │
│ Backups           │ AES-256 encrypted database dumps with KMS-managed backup keys      │
└───────────────────┴────────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────────────────────────────┐
│ DATA AT REST (EDGE DEVICE)                                                             │
├───────────────────┬────────────────────────────────────────────────────────────────────┤
│ Local Database    │ SQLite encrypted via SQLCipher 4.x (AES-256-CBC, 64,000 PBKDF2    │
│                   │ HMAC-SHA512 iterations, 4096-byte page size)                       │
│ Key Storage       │ Windows DPAPI / CNG, Android Keystore, iOS Keychain                │
└───────────────────┴────────────────────────────────────────────────────────────────────┘
```

---

### 7.2 Local Database Encryption Key Derivation Flow

```
[ Physical Hardware ]
       │
       ├─► CPU ID + Disk Serial + TPM Seed
       │
       ▼
[ OS Secure Enclave ] (Windows DPAPI / Android Keystore)
       │
       ├─► Retrieve Master Hardware Key (MHK)
       │
       ▼
[ Argon2id KDF ]
   Input: MHK + Device ID + Tenant Salt
   Params: Memory=64MB, Iterations=4, Parallelism=2
       │
       ▼
[ 256-Bit SQLCipher Passphrase ] ──► PRAGMA key = "x'a8f90c...'";
```

---

## 8. Key Rotation & Secret Management

### 8.1 Zero-Downtime Key Rotation Architecture

```
                                  ┌────────────────────────────────┐
                                  │   CLOUD KEY ROTATION ENGINE    │
                                  └────────────────────────────────┘
                                                  │
                 ┌────────────────────────────────┼────────────────────────────────┐
                 ▼                                ▼                                ▼
  [ JWT ASYMMETRIC KEYS ]             [ SQLCIPHER DB KEYS ]            [ TLS CA CERTIFICATES ]
  - Dual Key ID (kid) Window          - Executed via local background    - Automated renewal via ACME
  - Key A (Active, Signing)             job when online                   / cert-manager
  - Key B (Retiring, Verification)    - Executed using SQLCipher        - 30-day overlap window
  - Rotation frequency: 90 days        `PRAGMA rekey = 'new_key'`       - Rotation frequency: 180 days
```

1. **JWT Key Rotation**:
   - Cloud API maintains a JWKS endpoint publishing active and retiring public keys (`kid_2026_q1`, `kid_2026_q2`).
   - Tokens signed with new key `kid_2026_q2` are accepted alongside older unexpired tokens during a 24-hour grace window.
2. **Local SQLCipher Key Rotation**:
   - Tenant Admin triggers key rotation or automatic policy enforces 180-day rotation.
   - Device connects online, fetches new key seed component, re-keys local SQLite database via `PRAGMA rekey`, and updates OS Secure Store.

---

### 8.2 Secret Management Strategy

- **Cloud Secrets**: All production secrets (PostgreSQL passwords, SMTP credentials, third-party API keys) are injected into ASP.NET Core runtime via environment variables backed by **Azure Key Vault** / **HashiCorp Vault** / **AWS Secrets Manager**.
- **Source Code Protection**: No secrets permitted in git repositories. Pre-commit hooks (`gitleaks`, `trufflehog`) run on all developer branches.

---

## 9. Audit Logging & Security Monitoring

### 9.1 Immutable Cloud Audit Store (Hash-Chained Ledger)

To guarantee non-repudiation and tamper-evidence for financial and Maharashtra State Excise FL-III compliance logs, audit log entries are cryptographically hash-chained:

```
[ Audit Record N-1 ] ──► SHA-256 Digest (Hash_N-1)
                                │
                                ▼
[ Audit Record N   ] ──► Payload + Hash_N-1 ──► SHA-256 Digest (Hash_N)
                                │
                                ▼
[ Audit Record N+1 ] ──► Payload + Hash_N   ──► SHA-256 Digest (Hash_N+1)
```

If an attacker modifies Record $N$ directly in PostgreSQL, all subsequent hashes ($N+1, N+2, \dots$) fail validation.

---

### 9.2 Local Offline Audit Buffer

1. Edge POS generates structured audit entries for local events (e.g., Bill Created, Bill Cancelled, Cash Drawer Opened).
2. Local audit entries are signed using the device private key and stored in the encrypted SQLite `AuditQueue` table.
3. Upon reconnection, the Outbox Sync Engine uploads signed audit entries. Cloud verifies device signature and appends verified logs to the Cloud Audit Store.

---

### 9.3 SIEM & Automated Incident Detection

Cloud API streams structured JSON logs (Serilog) to Cloud SIEM (Elastic / Datadog / Azure Monitor) configured with real-time alerting rules:

```
INCIDENT MONITORING RULES:
1. UNUSUAL_SYNC_VOLUME   : > 500 Outbox mutations from a single POS device in < 1 minute.
2. FAILED_AUTH_BURST     : > 10 failed PIN / login attempts on device in < 2 minutes.
3. SEQUENCE_GAP_DETECTED : Outbox sequence gap indicates local DB tampering or rollbacks.
4. GEO_IMPROBABLE_LOGIN  : Device sync requests originating from multiple distant IP subnets simultaneously.
5. REPEATED_VOID_PATTERN : > 5 KOT/Bill voids executed by the same Cashier in a single shift.
```

---
