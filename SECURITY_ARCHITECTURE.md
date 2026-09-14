# SaaS Platform Security Architecture

## Executive Summary & Security Philosophy

This document defines the security architecture for the **Yashdeep Hotel Management SaaS Platform**. The platform operates as a hybrid, offline-first cloud-synchronized solution comprising edge POS terminals (Windows, Android, iOS, Blazor Hybrid / MAUI) and an ASP.NET Core 9 cloud backend backed by PostgreSQL 16+.

The central security philosophy is **Zero-Trust Client Isolation**. Edge devices controlled by customers, employees, or third parties are considered untrusted environments operating in hostile physical locations.

---

## 1. Commercial Requirement: Strict Secret Isolation

To protect the SaaS platform, SaaS infrastructure, multi-tenant cloud data, and intellectual property, **customer-controlled devices MUST NEVER receive or store platform-level secrets**.

### 1.1 Forbidden Secrets Matrix

The following secrets are strictly prohibited from residing on, passing through, or being accessible to edge devices or client application binaries:

| Secret Category | Examples / Assets | Protection & Enforcement Mechanism |
| :--- | :--- | :--- |
| **Cloud Database Credentials** | Cloud PostgreSQL connection strings, DB usernames, master passwords, connection pool tokens. | Edge devices access cloud data exclusively via authenticated REST/WebSocket APIs (`/api/v1/sync`). Direct database connections from edge devices are blocked at the cloud firewall/VPC boundary. |
| **Master Encryption Keys** | KMS Root Keys, Database Master Keys (DEK/KEK root secrets), Envelope Encryption parent keys. | Key management operations are restricted to cloud HSMs (AWS KMS / Azure Key Vault / HashiCorp Vault). Edge devices only hold local database encryption keys generated per-device. |
| **Cloud Administrator Credentials** | AWS/Azure IAM keys, Kubernetes cluster admin tokens, Cloudflare API tokens, DevOps pipeline secrets. | CI/CD pipelines and infrastructure management use OIDC short-lived identity federation. No human or machine credential for cloud management ever exists on client hardware. |
| **JWT Signing Secrets** | HMAC-SHA256 private keys, Ed25519/RSA private signing keys used for issuing JWTs. | JWT issuance and signing occur solely within the cloud Identity Service (`/api/v1/auth/token`). Client applications only receive signed, short-lived JWT access tokens and public verification keys (JWKS). |
| **Private Service Credentials** | Payment Gateway Secret Keys (Razorpay/Stripe API secrets), SMS Gateway API keys, SMTP passwords, LLM/AI tokens. | External API interactions are proxied through cloud microservices. Client devices receive ephemeral, scoped public keys or payment tokens (e.g., client session tokens for checkout widgets). |
| **SaaS Platform Secrets** | Multi-tenant master configuration keys, global system flags, internal RPC secrets. | Infrastructure and tenant configurations are isolated within server-side memory and protected by cloud secret managers. |

---

## 2. Authentication Architecture

The platform implements a dual-mode authentication scheme designed to handle both online cloud interactions and disconnected, offline edge operations.

```
+-----------------------------------------------------------------------------------+
|                                  CLIENT DEVICE                                    |
|                                                                                   |
|  +---------------------------+                 +-------------------------------+  |
|  |   Online Auth (OAuth2)    |                 |     Offline Auth (Local PIN)  |  |
|  | Short-lived JWT + Refresh |                 |  PBKDF2/Argon2id Salted Hash  |  |
|  +-------------+-------------+                 +---------------+---------------+  |
+----------------|-----------------------------------------------+------------------+
                 |                                               |
                 | HTTPS / TLS 1.3                               | SQLite Local Auth DB
                 v                                               v
+---------------------------------+             +----------------------------------+
|   Cloud Identity API Service    |             | Encrypted Local Database         |
|   - OAuth 2.0 + OIDC            |             | - Verified against local hash    |
|   - JWT Signing (ECDSA P-256)   |             | - Session valid until reconnect  |
+---------------------------------+             +----------------------------------+
```

### 2.1 Online Authentication (Cloud Identity Service)
- **Protocol**: OAuth 2.0 with OpenID Connect (OIDC) using the Authorization Code Flow with PKCE (Proof Key for Code Exchange - RFC 7636).
- **Primary Credentials**: User login via Username/Email and Password, backed by ASP.NET Core Identity.
- **Password Hashing**: Passwords hashed on the cloud identity server using **Argon2id** (Memory: 64MB, Iterations: 3, Parallelism: 4) or **PBKDF2 with HMAC-SHA512** (min 600,000 iterations).
- **Multi-Factor Authentication (MFA)**: TOTP (RFC 6238) required for `Administrator` and `Manager` roles during cloud portal login and device registration workflows.

### 2.2 Offline Authentication (Edge POS Mode)
- **Problem**: When internet connectivity is lost, POS terminals must allow Cashiers and Waiters to continue taking orders without cloud round-trips.
- **Mechanism**:
  1. During online sync, a cryptographically salted hash of the user's PIN/Password digest is synced to the edge SQLite database (`Users` table).
  2. The local database holds the user's assigned role and hashed PIN using **Argon2id** (or PBKDF2-SHA256 with 210,000 iterations).
  3. When offline, user PIN entry triggers local hash validation.
  4. Successful local authentication grants a local session token stored in memory, tied strictly to the registered POS device ID.

---

## 3. Authorization & Multi-Tenant RBAC

### 3.1 Role-Based Access Control (RBAC) Matrix

| Role | Cloud Management Portal | POS Billing & Cashier | KOT / Order Entry | Day-End Closing | Excise Compliance Reports |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Super Admin (SaaS Ops)** | Full (Platform) | None | None | None | None |
| **Tenant Admin (Hotel Owner)** | Full (Tenant Scope) | Full | Full | Full | Full |
| **Manager** | Limited | Full | Full | Full | Full |
| **Cashier** | None | Full (Settle/Print) | Full | Read Only | Read Only |
| **Captain / Waiter** | None | Order View Only | Full (Table/KOT) | None | None |
| **Kitchen Staff** | None | None | Read KOT / Bump | None | None |

### 3.2 Multi-Tenant Data Isolation Strategy
- **Shared Database, Isolated Schemas / Rows**: PostgreSQL utilizes a single-database, multi-tenant architecture with `TenantId` enforcement.
- **EF Core Global Query Filters**: All database queries automatically append `WHERE TenantId = @CurrentTenantId`.
- **PostgreSQL Row-Level Security (RLS)**:
  ```sql
  ALTER TABLE public.bills ENABLE ROW LEVEL SECURITY;

  CREATE POLICY tenant_isolation_policy ON public.bills
      USING (tenant_id = current_setting('app.current_tenant_id')::uuid);
  ```
- **Sync Boundary Control**: Edge devices belong to a single tenant (`TenantId`). The Outbox Sync Engine strictly filters sync payloads by `TenantId` and `DeviceId`, preventing cross-tenant data leaks.

---

## 4. Device Lifecycle Architecture

Edge terminals are critical components of the SaaS architecture. Every device must be explicitly onboarded, authenticated, and capable of instant revocation.

```
[ Device Enrollment Flow ]

  Unregistered POS Device               SaaS Cloud Portal                Cloud Identity API
         |                                     |                                 |
         | --- 1. Request Reg Code ----------> |                                 |
         |                                     | --- 2. Admin Generates Code --> | (6-digit / QR Code, 15m exp)
         | <--- 3. Code Issued To Admin ------ |                                 |
         |                                                                       |
         | --- 4. POST /api/v1/devices/register (Code + Hardware Fingerprint) -> |
         |                                                                       | -- Validates Code & Fingerprint
         |                                                                       | -- Generates Device Certificate
         | <--- 5. Return X.509 Device Cert + Scoped Device Token -------------- |
```

### 4.1 Device Registration & Enrollment Workflow
1. **Activation Code Generation**: Tenant Admin generates a single-use, 6-digit or QR activation code via the Cloud Portal (valid for 15 minutes).
2. **Hardware Fingerprinting**: The edge client collects hardware characteristics:
   - System UUID / Motherboard Serial
   - CPU ID & CPU Core Count
   - Storage Controller Serial Number
   - MAC Address (Primary Interface)
   - Combined hash created: `DeviceFingerprint = HMAC-SHA256(RawHardwareStrings, PlatformSalt)`
3. **Enrollment Payload**: The device sends the Activation Code, `DeviceFingerprint`, Device Name, and a locally generated Certificate Signing Request (CSR) to `/api/v1/devices/register`.
4. **Certificate Issuance**: Cloud API verifies the code, creates a `Devices` record, and issues an X.509 Device Certificate bound to the `DeviceId` and `TenantId`.

### 4.2 Device Authentication (mTLS / Device Tokens)
- **Transport Level**: Device-to-Cloud communication utilizes **Mutual TLS (mTLS)** where possible, presenting the client device certificate.
- **Application Level**: Outbox sync requests include `X-Device-Id` and `X-Device-Signature` headers signed by the device's private key (stored in OS secure storage).

### 4.3 Device Revocation & Kill-Switch
- **Revocation Triggers**:
  - Device reported stolen or lost by Tenant Admin.
  - Fraudulent activity or rogue sync payloads detected.
  - Subscription expiration or contract termination.
- **Revocation Enforcement**:
  1. Tenant Admin clicks "Revoke Device" in Cloud Portal.
  2. Cloud API invalidates the device's Certificate/Refresh Token in Redis/DB and updates the Device Revocation List (DRL).
  3. **Real-time Push Kill-Switch**: Server dispatches a revocation payload over SignalR / WebSockets.
  4. **Pull / Sync Intercept**: Any subsequent API request from the revoked device returns `401 Unauthorized` with payload `X-Action: TERMINATE_LOCAL_DATABASE`.
  5. **Edge Action**: The client app wipes the local SQLite database encryption key from OS Secure Storage, rendering the local database permanently unreadable.

---

## 5. Token Lifecycle & Management

```
+---------------------------------------------------------------------------------+
|                                 TOKEN LIFECYCLE                                 |
|                                                                                 |
|   +--------------------------+                 +----------------------------+   |
|   |   Access Token (JWT)     |                 |    Refresh Token (Opaque)  |   |
|   |   - Expire: 15 Minutes   |                 |    - Expire: 30 Days        |   |
|   |   - Stored in Memory     |                 |    - OS Keychain / DPAPI   |   |
|   +------------+-------------+                 +-------------+--------------+   |
+----------------|---------------------------------------------|------------------+
                 |                                             |
                 | Expired (15m)                               | Rotation on Use
                 v                                             v
+---------------------------------------------------------------------------------+
|                         POST /api/v1/auth/refresh-token                         |
|   - Sends Refresh Token + Device Signature                                      |
|   - Cloud verifies Device Certificate + Token Validity                          |
|   - Issues NEW Access Token + NEW Rotated Refresh Token                         |
+---------------------------------------------------------------------------------+
```

### 5.1 Token Types & Lifetimes

| Token Type | Lifespan | Format | Storage Location | Revocation Model |
| :--- | :--- | :--- | :--- | :--- |
| **Access Token** | 15 Minutes | JWT (ECDSA P-256) | Application Memory (RAM only) | Short expiry (No DB lookup needed) |
| **Refresh Token** | 30 Days (Sliding) | Opaque Crypto Guid | OS Keyring / Windows DPAPI / Android Keystore / iOS Keychain | Stored in Cloud DB; instantly revokable |
| **Device Sync Token** | 90 Days | Opaque HMAC token | Bound to Device Hardware Cert | Bound to Device ID & Certificate |

### 5.2 Token Storage Rules
- **Prohibited**: Plaintext file storage, `localStorage` / `sessionStorage` in web browsers, unprotected SQLite columns, static configuration files.
- **Allowed Hardware Secure Enclaves**:
  - **Windows**: Windows Data Protection API (DPAPI) / Credential Manager.
  - **Android**: Android KeyStore + EncryptedSharedPreferences.
  - **iOS / macOS**: Apple Keychain Services.
  - **Linux**: libsecret / Keyring service.

### 5.3 Refresh Token Rotation (RTR)
- Every time a refresh token is presented to `/api/v1/auth/refresh-token`, it is **invalidated immediately** and a new refresh token is issued.
- If a previously used refresh token is presented again (indicating token theft or replay), the Identity Service detects a reuse attack, **revokes the entire token family**, and invalidates all active sessions for that device and user.

---

## 6. Cryptographic Architecture & Key Strategy

### 6.1 Cryptographic Algorithms Standard

| Primitive | Algorithm / Standard | Specification |
| :--- | :--- | :--- |
| **Symmetric Encryption** | AES-256-GCM | Authenticated Encryption with Associated Data (AEAD) |
| **Asymmetric Signing** | Ed25519 / RSA | Ed25519 (EdDSA) or RSA-4096 / ECDSA P-256 |
| **Asymmetric Key Exchange** | ECDH | Elliptic Curve Diffie-Hellman over Curve25519 |
| **Hashing & Digests** | SHA-256 / SHA-512 | Cryptographic hash functions |
| **Key Derivation (KDF)** | HKDF / Argon2id / PBKDF2 | HKDF-SHA256 for key derivation |

### 6.2 Key Hierarchy & Envelope Encryption

```
+------------------------------------------------------------------+
|               Cloud HSM / Master Key (KMS / Key Vault)            |
|                  (Root Key - Never leaves HSM)                   |
+------------------------------------------------------------------+
                                  |
                                  v Encrypts
+------------------------------------------------------------------+
|                   Key Encryption Key (KEK)                       |
|               (Tenant-Level Key - Cloud Managed)                 |
+------------------------------------------------------------------+
                                  |
                                  v Encrypts
+------------------------------------------------------------------+
|                    Data Encryption Key (DEK)                     |
|            (Per-Database / Local SQLite Encryption Key)          |
+------------------------------------------------------------------+
```

1. **Root Master Key (KMS)**: Stored in cloud FIPS 140-2 Level 3 Hardware Security Modules (AWS KMS / Azure Key Vault).
2. **Key Encryption Key (KEK)**: Generated per tenant, protected by the KMS Root Key.
3. **Data Encryption Key (DEK)**:
   - **Cloud PostgreSQL**: Column-level encryption keys derived from KEK.
   - **Local Edge SQLite**: Each POS terminal generates its own 256-bit DEK stored inside the OS hardware secure enclave (DPAPI/Keystore).

---

## 7. Encryption Standards (At Rest & In Transit)

### 7.1 Encryption at Rest

#### Local SQLite Database (Edge POS)
- **Engine**: **SQLCipher 4.x** (256-bit AES-CBC with HMAC-SHA512 per page verification and PBKDF2 key derivation with 64,000 iterations).
- **Key Generation**: Upon first boot/enrollment, the client app generates a random 256-bit key using `RandomNumberGenerator.GetBytes(32)`.
- **Key Protection**: The key is stored exclusively inside OS Secure Storage (DPAPI/Android KeyStore/Keychain).

#### Cloud Database (PostgreSQL 16)
- **Disk-Level**: Transparent Data Encryption (TDE) enabled on Cloud Managed Database storage volumes (AWS EBS / Azure Managed Disks with AES-256).
- **Column-Level Application Encryption**: Sensitive columns (e.g. Excise License numbers, User PII, Payment Account Details) encrypted via EF Core Value Converters using AES-256-GCM prior to database insertion.

### 7.2 Encryption in Transit
- **TLS Protocol**: Minimum **TLS 1.3** enforced across all public endpoints; TLS 1.2 allowed only for legacy receipt printer gateways with explicit cipher suite restrictions.
- **Cipher Suites**:
  - `TLS_AES_256_GCM_SHA384`
  - `TLS_CHACHA20_POLY1305_SHA256`
  - `TLS_AES_128_GCM_SHA256`
- **HTTP Strict Transport Security (HSTS)**: Header set to `max-age=31536000; includeSubDomains; preload`.
- **Certificate Pinning**: The Blazor Hybrid / MAUI client pins the Cloud API public key hash (SPKI Pinning) to protect against Man-in-the-Middle (MitM) attacks from intercepting proxies or rogue local certificates.

---

## 8. Key Rotation & Secret Management

### 8.1 Key Rotation Schedule

| Key / Secret Type | Rotation Frequency | Rotation Mechanism | Impact / Fallback |
| :--- | :--- | :--- | :--- |
| **KMS Master Keys** | Every 365 Days | Automatic Cloud KMS Rotation | Zero downtime; previous key handles decryption |
| **JWT Signing Keys** | Every 90 Days | Dual-key active window (JWKS) | Tokens signed with old key valid until expiration |
| **Device Certificates** | Every 180 Days | Background CSR re-enrollment | Auto-renewed during active sync |
| **Database Encryption Keys (DEK)** | Every 365 Days | Background re-key (`PRAGMA rekey`) | Performed during off-peak hours / Day End |

### 8.2 Secret Management Strategy
- **Cloud Infrastructure**: Azure Key Vault / AWS Secrets Manager integrated with ASP.NET Core `Microsoft.Extensions.Configuration`. Secrets injected at runtime into application memory via Managed Identity (zero plaintext passwords in `appsettings.json`).
- **Development Environment**: Developers use .NET User Secrets (`dotnet user-secrets`) or local environment variables. Hardcoded credentials in source code are strictly blocked by pre-commit `git-leaks` hooks.

---

## 9. Audit Logging & Security Monitoring

### 9.1 Structured Audit Logging Schema
All security-critical events emit structured JSON log events using Serilog to immutable cloud storage (AWS S3 Object Lock / Azure Immutable Blob).

```json
{
  "Timestamp": "2026-03-30T10:15:30.123Z",
  "EventId": "SEC_AUTH_LOGIN_SUCCESS",
  "Severity": "INFO",
  "TenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "UserId": "usr_998231a4",
  "DeviceId": "dev_pos_table_01",
  "IPAddress": "103.21.124.12",
  "UserAgent": "YashdeepPOS/2.4.0 (Windows NT 10.0; Win64; x64)",
  "Details": {
    "Method": "PinAuth",
    "OfflineMode": false
  }
}
```

### 9.2 Audited Security Events
- User Authentication (Login, Logout, Failed attempts, Password/PIN changes).
- Device Lifecycle (Registration, Key Renewal, Revocation, Kill-switch activation).
- Authorization Failures (Permission denied, cross-tenant access attempts).
- Sensitive Data Access (Exporting Excise Reports, Modifying Historical Bills, Voiding KOTs).
- Day-End Closing operations and financial settlements.

### 9.3 Security Monitoring & Threat Detection
- **Rate Limiting**: Cloud Gateway enforces rate limiting via Redis (`AspNetCoreRateLimit`):
  - Login Endpoint (`/api/v1/auth/login`): 5 attempts per minute per IP.
  - Sync Batch Endpoint (`/api/v1/sync/batch`): 60 requests per minute per Device.
- **SIEM & Anomaly Detection**:
  - Real-time alerts dispatched to Security Ops when multiple failed logins occur across distinct devices for the same user.
  - Automatic isolation of devices sending anomalous sync payloads (e.g. out-of-order sequence numbers or modified historical bills).

---

## 10. Comprehensive Threat Model & Attack Vectors

### 10.1 STRIDE Threat Analysis

| Threat Category | Potential Attack Vector | Impact | Architecture Mitigation |
| :--- | :--- | :--- | :--- |
| **Spoofing** | Adversary attempts to impersonate a legitimate POS device to inject fake transactions. | High | Hardware-bound X.509 device certificates, mTLS, and HMAC hardware fingerprint verification on every sync request. |
| **Tampering** | Local operator modifies SQLite database directly to zero out sales before cloud sync. | High | SQLCipher 256-bit encryption + HMAC integrity verification per page + cloud side audit ledger cross-checks. |
| **Repudiation** | Cashier denies voiding a bill or deleting a KOT item. | Medium | Immutably signed audit events capturing `UserId`, `DeviceId`, timestamp, and signature. |
| **Information Disclosure** | Physical theft of a POS terminal to read customer data or cloud secrets. | Critical | Zero cloud secrets on client; local SQLite encrypted with SQLCipher; keys stored in OS Secure Enclave; remote wipe kill-switch. |
| **Denial of Service** | Flooding cloud sync API with invalid requests during peak dinner rush. | High | Cloudflare DDoS mitigation, Redis rate limiting, offline-first queueing ensuring local POS operations continue unabated. |
| **Elevation of Privilege** | Waiter attempts to invoke Day-End settlement or access Excise compliance reports. | High | Server-side RBAC validation with Claims-based policies and EF Core multi-tenant filters. |

### 10.2 Offline Attack Vectors & Countermeasures

```
[ Physical Theft of POS Terminal Scenario ]

Adversary Steals POS Hardware
       |
       v
Attempts to extract SQLite Database / Master Secrets
       |
       +---> 1. Inspects Disk File System: `local_pos.db` is SQLCipher encrypted (AES-256).
       |
       +---> 2. Attempts Memory Dump / Reverse Engineering:
       |        - No Cloud DB credentials exist in binary or memory.
       |        - No Master Keys or JWT signing keys exist in binary or memory.
       |
       +---> 3. Attempts Key Extraction: Encryption key is protected by OS Secure Enclave (DPAPI/Keystore).
       |
       +---> 4. Terminal Connects to Internet:
                - Cloud triggers Remote Wipe Kill-Switch.
                - OS Secure Storage key deleted -> SQLite database permanently destroyed.
```

---

## 11. Architectural Compliance Matrix

| Security Requirement | Status | Architecture Blueprint Verification |
| :--- | :---: | :--- |
| **Zero Cloud Credentials on Device** | **COMPLIANT** | Enforced via REST/WebSocket proxy architecture and absolute secret prohibition in Section 1. |
| **Offline Authentication** | **COMPLIANT** | Argon2id/PBKDF2 salted local hashes in encrypted SQLite DB (Section 2.2). |
| **Device Revocation / Kill-Switch** | **COMPLIANT** | Certificate Revocation List + SignalR push + DPAPI key destruction (Section 4.3). |
| **Short-Lived Access Tokens** | **COMPLIANT** | 15-minute JWTs + Refresh Token Rotation in Secure OS Enclaves (Section 5). |
| **Database Encryption** | **COMPLIANT** | Local SQLCipher 4.x (AES-256) + Cloud PostgreSQL TDE (Section 7.1). |
| **Audit Logging** | **COMPLIANT** | Serilog JSON structured logging to immutable cloud storage (Section 9). |
