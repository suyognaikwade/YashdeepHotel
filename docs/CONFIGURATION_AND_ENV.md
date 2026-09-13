# Configuration Management, Environment Variables, Hardware Protocols & Security

This document details the configuration requirements, environment variables, security policies, hardware printing protocols, logging, error handling, and caching policies for the **Yashdeep Hotel Management System**.

---

## 1. Environment Variables & Configuration Matrix

In accordance with modern SaaS security best practices, **no plain-text production passwords, JWT keys, or private database connections are stored in source code repositories**.

The following matrix documents all environment variables used by the modern .NET 9 Web API, Sync Engine, and POS clients:

| Variable Name | Required / Optional | Purpose | Example / Placeholder Value |
| :--- | :---: | :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | **Required** | Defines runtime environment | `Development`, `Staging`, `Production` |
| `ConnectionStrings__PostgreSQL` | **Required** | Cloud PostgreSQL server connection string | `Host=cloud-db.internal;Port=5432;Database=yashdeep_hotel;Username=app_user;Password=YOUR_POSTGRES_PASSWORD_HERE` |
| `ConnectionStrings__Sqlite` | **Required** | Local edge POS SQLite database path | `Data Source=C:\YashdeepPOS\Data\local_pos.db;` |
| `ConnectionStrings__LegacyAccessMdb` | Optional | Path to legacy Access MDB for data ingestion | `Provider=Microsoft.ACE.OLEDB.12.0;Data Source=RSS26\dinurss.mdb;Jet OLEDB:Database Password=<PRODUCTION_MDB_PASSWORD>;` |
| `JwtSettings__SecretKey` | **Required** | Secret key for signing JWT tokens | `YOUR_SUPER_SECRET_JWT_KEY_MIN_32_CHARS_LONG` |
| `JwtSettings__Issuer` | **Required** | Token issuer claim | `https://api.yashdeephotel.com` |
| `JwtSettings__Audience` | **Required** | Token audience claim | `https://app.yashdeephotel.com` |
| `JwtSettings__ExpiryMinutes` | Optional | JWT lifetime in minutes | `480` |
| `HotelConfig__TradeName` | Optional | Hotel display trade name | `HOTEL YASHDEEP` |
| `HotelConfig__ExciseLicense` | Optional | State Excise license string | `FL III-2151444022D8ADF7` |
| `HotelConfig__VatTin` | Optional | State Tax VAT TIN | `27900111779v` |
| `HotelConfig__UpiVpa` | Optional | Default VPA for billing payment QR codes | `dinu@upi` |
| `HotelConfig__AdminEmail` | Optional | Recipient address for automated Day End sales reports | `Fahadsayyed92@gmail.com` |
| `Smtp__Host` | Optional | SMTP mail server hostname | `smtp.mailgun.org` |
| `Smtp__Port` | Optional | SMTP mail port | `587` |
| `Smtp__Username` | Optional | SMTP login username | `postmaster@yashdeephotel.com` |
| `Smtp__Password` | Optional | SMTP login password | `YOUR_SMTP_PASSWORD_HERE` |

---

## 2. Hardware Printing Protocols (Thermal ESC/POS & Reports)

Hotel operations rely heavily on rapid receipt and kitchen order ticket (KOT) printing.

### 2.1 ESC/POS Thermal Receipt Printing
- **Supported Thermal Roll Widths**: 80mm ( standard counter printer) and 58mm (portable handheld POS).
- **Communication Protocols**:
  - **USB Direct Raw Spooler**: Sends raw byte streams (`System.Drawing` or native ESC/POS commands) to Windows print queue.
  - **Network / LAN Thermal Printers**: TCP socket connections to port `9100` (e.g., Kitchen / Bar remote printers).
  - **Bluetooth POS Terminals**: Serial Port Profile (SPP) byte stream dispatch.

### 2.2 QuestPDF Document Engine
Crystal Reports (`.rpt`) are replaced with **QuestPDF** code-first C# templates:
- **`BillDocument.cs`**: Generates 80mm / 58mm customer receipts containing itemized lines, split taxes (CGST/SGST), and a dynamic rasterized UPI payment QR code generated from `HotelConfig__UpiVpa`.
- **`KotDocument.cs`**: Generates kitchen slips with native UTF-8 Devanagari script strings (`item.Marathi`, e.g., `चिकन टिक्का`) for cook readability.
- **`ExciseReportDocument.cs`**: Generates A4 PDF returns for Maharashtra State Excise Officers (Daily Bulk Litre Statement & Register 1).

---

## 3. Error Handling, Logging & Diagnostics

### 3.1 Structured Logging (Serilog)
The application emits structured JSON logs to both console and file sinks:
- **Sink Formats**: Console (ANSI colorized), Rolling Daily Log File (`logs/yashdeep-api-.log`).
- **Enriched Properties**: `TenantId`, `UserId`, `TerminalId`, `BusinessDate`, `CorrelationId`.

### 3.2 Error Handling & Resilience
- **Global Exception Middleware**: Intercepts unhandled errors, logs detailed stack traces, and returns standardized RFC 7807 Problem Details JSON to clients.
- **Database Transient Fault Handling**: Uses EF Core's `EnableRetryOnFailure()` for PostgreSQL cloud connections to automatically handle brief network dropouts.

---

## 4. Caching & Security Policies

### 4.1 Caching Strategy
- **Menu Catalog & Pricing**: Cached in-memory locally (`IMemoryCache` or SQLite cache table) with invalidation triggers when menu prices or item rates update.
- **Table Layout State**: Real-time table states (Vacant, KOT Active, Billed) synchronized via SignalR WebSockets and cached locally.

### 4.2 Security Requirements
- **Authentication**: ASP.NET Core Identity with password hashing (PBKDF2 / bcrypt).
- **Authorization**: Role-Based Access Control (RBAC) supporting roles: `Administrator`, `Manager`, `Cashier`, `Captain`, `Waiter`.
- **Multi-Tenant Data Isolation**: EF Core global query filters automatically enforce `WHERE TenantId = @currentTenantId` on all database operations.
