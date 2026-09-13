# Comprehensive Database Schema Reference

This document provides a complete reference for all **105 user tables** present in the production Microsoft Access Jet 4.0 database (`RSS26/dinurss.mdb`, Password: `rss1008`).

---

## 1. Domain Grouping & Summary

The database tables fall into 9 functional domains:

| Domain | Table Count | Key Tables |
| :--- | :---: | :--- |
| **1. Sales & Billing** | 16 | `BILLFINAL`, `BILLFINAL_Dayend`, `GrandBill`, `GrandBillDetails`, `finalbill`, `finalbillcopy`, `sectionwiseBillfinal_Temp` |
| **2. Kitchen Orders (KOT)** | 10 | `KOTDETAIL`, `KOTDETAIL_DAYEND`, `KOTFINAL`, `KOTFINAL_DAYEND`, `CancelKot`, `BILLKOT` |
| **3. Menu, Items & Categories** | 6 | `item`, `ItemDept`, `BRANDML`, `BtPerCase`, `TABLE_NO_GROP`, `NewFood` |
| **4. Inventory & Stock** | 17 | `GodownStock`, `FoodStock`, `CNTPACK_LIVE`, `CNTPACK_LIVE_DAYEND`, `CNTLOOSE_LIVE`, `CntRecieved_N` |
| **5. Purchases** | 6 | `LiqPurchase`, `LiqPurchaseFinal`, `FoodPurchase`, `FoodPurchaseFinal`, `OtherPurchase`, `OtherPurchaseFinal` |
| **6. Maharashtra State Excise** | 15 | `ExPremiteHolder`, `ExciseOpening`, `ExciseClosingAutoSale`, `ExciseMonthlyStat`, `ExStoreInfo`, `ExUnitLimit` |
| **7. Accounting & Ledgers** | 11 | `AccountHead`, `CashBookOpen`, `CashTransfer`, `NewCustomer`, `NewVendor`, `NewBank`, `Payment`, `Receipt`, `Voucher` |
| **8. Staff & Customers** | 4 | `Waiter`, `Customer`, `ContactList`, `ContactList_Temp` |
| **9. System Configuration & Security** | 20 | `HotelInfo`, `Setup`, `Login`, `dateLckMaster`, `datelock`, `DAYEND`, `PRINTERSETUP`, `SoftwareName` |

---

## 2. Detailed Table Specifications

### 2.1 Sales & Billing Domain

#### `BILLFINAL` (Active Daily Invoices)
*Row Count: 3,693*
Represents finalized customer guest checks before Day End rollover.

| Column | Type | Nullable | Description |
| :--- | :--- | :---: | :--- |
| `BILLNO` | INTEGER | NO | Primary Bill / Invoice sequence number |
| `TABLE_NO` | VARCHAR(255) | YES | Table identifier (e.g., "T1", "A2", "Parcel") |
| `WAITER` | VARCHAR(255) | YES | Staff member / captain responsible |
| `DATE` | TIMESTAMP | YES | Bill settlement date |
| `PAID` | INTEGER | YES | Flag: 1 = Settled / Paid, 0 = Open check |
| `TYPE` | VARCHAR(255) | YES | Settlement mode: Cash, Credit, Compliment, Card, UPI |
| `CUST_NAME` | VARCHAR(255) | YES | Customer name (if credit or corporate) |
| `SERVICE_CHARGE`| INTEGER | YES | Levied service charge amount |
| `DISCOUNT` | INTEGER | YES | Applied discount amount |
| `TOTAL` | INTEGER | YES | Net payable bill total |
| `BILLDAY` | INTEGER | YES | Daily sequential counter index |
| `TIME` | TIMESTAMP | YES | Bill settlement timestamp |
| `CLIENTID` | INTEGER | YES | Terminal identifier that finalized the bill |
| `OPERATOR` | VARCHAR(255) | YES | Cashier username |
| `INTIME` | TIMESTAMP | YES | Table seating / order creation time |
| `CGST` | DOUBLE PRECISION| YES | Central GST tax amount |
| `SGST` | DOUBLE PRECISION| YES | State GST tax amount |
| `FOODTOT` | INTEGER | YES | Total food-only subtotals (for split tax rates) |
| `SECTIONS` | VARCHAR(255) | YES | Section name: Family, Ac, Hall, Restaurant, Garden |

#### `finalbill` (Itemized Bill Breakdown)
*Row Count: 13,400*
Contains line-item records for items on every customer invoice.

| Column | Type | Nullable | Description |
| :--- | :--- | :---: | :--- |
| `BILLNO` | INTEGER | YES | Foreign Key referencing `BILLFINAL.BILLNO` |
| `TABLE_NO` | VARCHAR(255) | YES | Table identifier |
| `SRNO` | INTEGER | YES | Line item sequence number |
| `ITEM` | VARCHAR(255) | YES | Menu item name |
| `QTY` | INTEGER | YES | Quantity sold |
| `RATE` | INTEGER | YES | Unit rate charged |
| `AMT` | INTEGER | YES | Line item gross amount (`QTY * RATE`) |
| `DATE` | TIMESTAMP | YES | Order date |
| `DEPT` | VARCHAR(255) | YES | Department: "Food", "Liquior", "Beverage" |
| `TYPE` | VARCHAR(255) | YES | Item category (e.g., IMFL, Country Liquor, Veg) |
| `UNIT` | INTEGER | YES | Dispensing unit volume (ml: 30, 60, 90, 180, 750) |
| `PACKING` | VARCHAR(255) | YES | Bottle or pack size |
| `BRAND` | VARCHAR(255) | YES | Liquor brand association |
| `BILLTYPE` | VARCHAR(255) | YES | "Normal", "Complimentary", "Cancel" |
| `CLIENTID` | INTEGER | YES | POS Terminal ID |
| `OPERATOR` | VARCHAR(255) | YES | Cashier username |
| `SECTIONS` | VARCHAR(255) | YES | Floor section |

---

### 2.2 Kitchen Orders (KOT) Domain

#### `KOTFINAL` (KOT Header)
*Row Count: 10,625*

| Column | Type | Nullable | Description |
| :--- | :--- | :---: | :--- |
| `KOTNO` | VARCHAR(255) | YES | KOT Ticket number |
| `TABLE_NO` | VARCHAR(255) | YES | Dining table number |
| `WAITER` | VARCHAR(255) | YES | Waiter name / ID |
| `TOTAL` | INTEGER | YES | Order subtotal |
| `DATE` | TIMESTAMP | YES | Timestamp when KOT was printed |
| `DAYKOT` | VARCHAR(255) | YES | Sequential daily KOT number |

#### `KOTDETAIL` (KOT Items)
*Row Count: 19,382*

| Column | Type | Nullable | Description |
| :--- | :--- | :---: | :--- |
| `TABLE_NO` | VARCHAR(255) | YES | Table number |
| `WAITER` | VARCHAR(255) | YES | Waiter name |
| `SRNO` | INTEGER | YES | Line serial number |
| `ITEM` | VARCHAR(255) | YES | Item ordered |
| `QTY` | INTEGER | YES | Quantity ordered |
| `RATE` | INTEGER | YES | Item unit price |
| `AMT` | INTEGER | YES | Line subtotal |
| `DATE` | TIMESTAMP | YES | Order timestamp |
| `KOTNO` | VARCHAR(255) | YES | Associated KOT ticket number |

#### `CancelKot` (Voided / Cancelled Kitchen Orders)
*Row Count: 1,389*
Audit trail of items removed from tables after initial KOT printing.

| Column | Type | Nullable | Description |
| :--- | :--- | :---: | :--- |
| `BILLNO` | INTEGER | YES | Bill number |
| `TABLE_NO` | VARCHAR(255) | YES | Table number |
| `SRNO` | INTEGER | YES | Line serial number |
| `ITEM` | VARCHAR(255) | YES | Cancelled item name |
| `QTY` | INTEGER | YES | Cancelled quantity |
| `RATE` | INTEGER | YES | Item rate |
| `AMT` | INTEGER | YES | Cancelled value |
| `Date` | TIMESTAMP | YES | Cancellation timestamp |
| `waiter` | VARCHAR(255) | YES | Waiter requesting cancellation |

---

### 2.3 Menu, Items & Category Domain

#### `item` (Master Menu Items Catalog)
*Row Count: 1,607*

| Column | Type | Nullable | Description |
| :--- | :--- | :---: | :--- |
| `code` | VARCHAR(255) | YES | Short numeric code for ultra-fast POS entry |
| `item` | VARCHAR(255) | YES | Item name (English) |
| `stock` | VARCHAR(255) | YES | Stock tracking group |
| `dept` | VARCHAR(255) | YES | Department ("Kitchen", "Bar", "Beverages") |
| `type` | VARCHAR(255) | YES | Liquor / Food type (Whisky, Rum, Beer, Starter) |
| `unit` | INTEGER | YES | Volume in ML (30, 60, 90, 180, 375, 750, 1000) |
| `packing` | VARCHAR(255) | YES | Packaging description |
| `salerate` | INTEGER | YES | Standard base selling price (INR) |
| `exciserate` | INTEGER | YES | Official state excise rate |
| `purchaserate`| INTEGER | YES | Standard purchase / procurement cost |
| `bottel` | INTEGER | YES | Bottles per case conversion factor |
| `openingstock`| INTEGER | YES | Base opening balance |
| `FAMILYRATE` | INTEGER | YES | Differential rate for Family section |
| `VIPRATE` | INTEGER | YES | Differential rate for VIP lounge |
| `ACRATE` | INTEGER | YES | Differential rate for AC hall |
| `WHOLESALE` | INTEGER | YES | Wholesale / Bulk takeaway rate |
| `BRAND` | VARCHAR(255) | YES | Manufacturer / Liquor Brand |
| `Marathi` | VARCHAR(255) | YES | Devanagari translation for KOT slip printing |
| `IT_CODE` | VARCHAR(255) | YES | Extended SKU / barcode |

#### `ItemDept` (Kitchen & Bar Routing Departments)
*Row Count: 39*

| Column | Type | Nullable | Description |
| :--- | :--- | :---: | :--- |
| `code` | INTEGER | YES | Department code |
| `name` | VARCHAR(255) | YES | Department name (e.g., "Tandoor", "Bar", "Chinese") |
| `child` | INTEGER | YES | Sub-department hierarchy level |
| `setup` | INTEGER | YES | Configuration flag |
| `id` | INTEGER | YES | Sequence order |
| `printer` | VARCHAR(255) | YES | Target printer name (for multi-printer KOT routing) |

---

### 2.4 Inventory & Stock Domain

#### `GodownStock` (Central Bulk Storage / Warehouse)
*Row Count: 1,926*
Tracks cases and sealed bottles in the main hotel storage godown before being issued to the bar counter.

| Column | Type | Nullable | Description |
| :--- | :--- | :---: | :--- |
| `code` | INTEGER | YES | Item code |
| `item` | VARCHAR(255) | YES | Item name |
| `stock` | VARCHAR(255) | YES | Category |
| `dept` | VARCHAR(255) | YES | Department |
| `type` | VARCHAR(255) | YES | Liquor class |
| `unit` | INTEGER | YES | ML per bottle |
| `packing` | VARCHAR(255) | YES | Packing format |
| `salerate` | INTEGER | YES | Selling price |
| `OPENING` | INTEGER | YES | Opening stock balance (bottles/units) |
| `OpenTemp` | INTEGER | YES | Temporary opening balance |
| `Purchase` | INTEGER | YES | Inward stock from vendor purchases |
| `Recieved` | INTEGER | YES | Transfers received |
| `CLOSING` | INTEGER | YES | Computed closing balance |
| `BRAND` | VARCHAR(255) | YES | Brand name |
| `TOTALPACK` | INTEGER | YES | Total pack count |
| `DATE` | TIMESTAMP | YES | Balance snapshot date |

#### `CNTPACK_LIVE` (Counter Sealed Pack Stock)
*Row Count: 1,934*
Live stock of sealed bottles available behind the dispensing counter bar.

| Key Columns | Type | Description |
| :--- | :--- | :--- |
| `code`, `item` | VARCHAR | SKU identifiers |
| `salerate` | INTEGER | Selling price |
| `OPENING`, `RECIEVED` | INTEGER | Start of day balance + Godown inward stock |
| `SALE`, `SALELOOSE` | INTEGER | Full bottles sold vs bottles opened for loose dispensing |
| `CLOSING`, `CLOSINGLOOSE`| INTEGER | Remaining sealed bottles & open bottles |
| `Opnbotlqty` | INTEGER | Quantity in currently open dispensing bottles |
| `AddAdjml`, `MinusAdjml` | INTEGER | Positive and negative breakage / spillage adjustments |

---

### 2.5 Maharashtra State Excise Domain (FL-III License)

#### `HotelInfo` (Licensee & Entity Configuration)
*Row Count: 1*

| Field | Production Value | Business Purpose |
| :--- | :--- | :--- |
| `name` | `HOTEL YASHDEEP` | Trade Name |
| `address` | `bhenda` | Registered hotel location |
| `licno` | `FL III-2151444022D8ADF7` | Maharashtra State Excise FL-III License |
| `RuleUsed`| `Register of sales of foregin liquor in units...`| Legal mandatory header format for Register 1 |
| `Vattin` | `27900111779v` | Maharashtra Commercial Tax / VAT TIN |
| `upi_id` | `dinu` | Default UPI VPA for payment QR code |
| `AdminMobile`| `7741870808` | Hotel management mobile |
| `Email` | `Fahadsayyed92@gmail.com` | Automated sales report recipient |

#### `ExPremiteHolder` (Customer Liquor Permit Register)
*Row Count: 524*
Under Bombay Prohibition Act & Maharashtra State Excise rules, holders of FL-III licenses must maintain a register of permit holders buying foreign liquor.

| Column | Type | Nullable | Description |
| :--- | :--- | :---: | :--- |
| `code` | INTEGER | YES | Permit holder internal ID |
| `name` | VARCHAR(255) | YES | Permit holder full legal name |
| `licence` | INTEGER | YES | State liquor permit number |
| `validity` | VARCHAR(255) | YES | Validity status (Daily / Annual / LLD) |
| `phone` | VARCHAR(255) | YES | Contact number |
| `Address` | VARCHAR(255) | YES | Residential address |
| `Issued` | TIMESTAMP | YES | Permit date of issue |
| `Expiry` | TIMESTAMP | YES | Permit expiration date |

---

### 2.6 Accounting & Ledgers Domain

#### `AccountHead` (General Ledger Chart of Accounts)
*Row Count: 256*

| Column | Type | Nullable | Description |
| :--- | :--- | :---: | :--- |
| `code` | INTEGER | YES | Account Head code |
| `name` | VARCHAR(255) | YES | Account Name (e.g., "Electricity", "Vegetables") |
| `address` | VARCHAR(255) | YES | Vendor/Party address |
| `mobile` | VARCHAR(255) | YES | Party contact |
| `credit` | DOUBLE PRECISION| YES | Current credit balance |
| `debit` | DOUBLE PRECISION| YES | Current debit balance |

#### `Voucher` (Petty Cash & Expense Vouchers)
*Row Count: 6,770*

| Column | Type | Nullable | Description |
| :--- | :--- | :---: | :--- |
| `code` | INTEGER | YES | Voucher reference number |
| `date` | TIMESTAMP | YES | Voucher date |
| `name` | VARCHAR(255) | YES | Expense account head / Payee |
| `narr` | VARCHAR(255) | YES | Narration / Expense reason |
| `amt` | DOUBLE PRECISION| YES | Cash amount disbursed |

---

## 3. PostgreSQL Migration DDL

The full DDL script to instantiate all 105 tables in modern PostgreSQL 16+ is available at:
[`schema_extracted/postgres_schema.sql`](file:///c:/xampp/htdocs/AntigravityProjects/YashdeepHotelMS/schema_extracted/postgres_schema.sql)
