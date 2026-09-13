# Legacy System Analysis & Domain Reverse-Engineering Document
**Target Entity:** Hotel Yashdeep / Real Soft (RSS / Yashdeep Hotel Management System)
**Legacy Stack:** VB.NET / C# (.NET Framework 4.0 WinForms Monolith), Microsoft Access Jet 4.0 Database (`dinurss.mdb`, Password: `rss1008`), Crystal Reports
**Analysis Scope:** Complete reverse-engineered business domain model, operational workflows, calculations, validations, numbering rules, side effects, technical debt anti-patterns, and open questions.

---

## Executive Summary & System Identification

The legacy application is a combined On-Premises Restaurant POS, Bar Dispensing, Hotel Service, and Maharashtra State Excise FL-III Compliance System. It was developed in .NET Framework 4.0 using Windows Forms and a local Microsoft Access 2000-2003 (`.mdb`) database.

### Core System Properties
- **Trade Name:** HOTEL YASHDEEP
- **Location:** Bhenda / Nanded, Maharashtra, India
- **State Excise License Number:** `FL III-2151444022D8ADF7` (Maharashtra Foreign Liquor Hotel/Club License)
- **VAT / Tax Registration:** VAT TIN `27900111779v` (State Code 27 - Maharashtra)
- **Primary Data Store:** `RSS26/dinurss.mdb` (22.5 MB, 105 user tables, Jet OLEDB password `rss1008`)
- **Primary Executable:** `RSS.exe` (Monolithic WinForms client containing 190 forms and 83 support classes)

---

## 1. Outlets, Departments & Dining Sections

### 1.1 Floor Sections (`TABLE_NO_GROP` / `SECTIONS`)
Hotel Yashdeep operates across 6 distinct operational dining areas/sections:
1. **Family (`F`)**: Dedicated family dining hall (child-friendly, quieter ambience).
2. **AC (`A`)**: Air-conditioned premium dining section.
3. **VIP (`V`)**: Executive/VIP lounge service.
4. **Hall / Restaurant (`H` / `R` / `T`)**: Main indoor casual dining room.
5. **Garden (`G`)**: Outdoor garden dining lawn.
6. **Parcel / Takeaway (`P`)**: Parcel counter for takeaway packaging.

### 1.2 Kitchen & Bar Departments (`ItemDept` / `dept`)
The catalog is split into routing departments:
- **Food / Kitchen Departments:** e.g., Tandoor, Chinese, Veg Main Course, Non-Veg, Starters, Ice Cream, Cold Drinks.
- **Bar / Liquor Departments:** e.g., IMFL (Indian Made Foreign Liquor), Country Liquor (CL), Beer, Wine, Foreign Liquor.
- **Beverage Departments:** Tea, Coffee, Water.

---

## 2. Tables, Seating & Table Lifecycle

### 2.1 Table Identification & Naming Convention
Tables are represented as alphanumeric strings (e.g., `T1`, `T2`, `A1`, `F5`, `V2`, `Parcel`).
- The **first character** (`TABLECHAR`) dictates the section and default pricing tier.

### 2.2 Table Lifecycle & State Transitions
Visual representation on the main POS floor map (`FRM_CALLTBL` / `FRMENTRY`):

```
┌─────────────────────────────────────────────────────────────┐
│                    VACANT TABLE (White)                     │
│  - No active unbilled KOT                                   │
│  - Ready for guest seating                                  │
└──────────────────────────────┬──────────────────────────────┘
                               │ Guest Order Input (`FRMENTRY`)
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                   OCCUPIED TABLE (Red)                      │
│  - Active KOT printed (`tablered()`)                        │
│  - Unbilled items exist in `KOTDETAIL`                      │
└──────────────────────────────┬──────────────────────────────┘
                               │ Print Guest Bill (`PrintBILL`)
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                    BILLED TABLE (Green)                     │
│  - Bill printed, awaiting payment settlement               │
│  - Settled in `BILLFINAL`                                   │
└──────────────────────────────┬──────────────────────────────┘
                               │ Settlement / Payment
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                     TABLE RESET (Vacant)                    │
│  - Data moved to `BILLFINAL` & `finalbill`                  │
│  - Active KOT cleared (`BILLKOT` cleared)                   │
└─────────────────────────────────────────────────────────────┘
```

### 2.3 Table Operations
- **Table Merge (`FRMTABLEMERGE`)**: Merges all open KOT lines from a source table (`TXTFROM`) into a target destination table (`TXTTO`).
- **Table Shift (`FRMTABLESHIFT`)**: Transfers all active KOT lines from one physical table to another without altering order history.

---

## 3. Menu Catalog, Items, Categories & Differential Pricing

### 3.1 Item Entity Architecture (`item`)
Every menu item is assigned a short numeric code (`code`) for rapid keypad entry in POS.

| Field | Type | Description |
| :--- | :--- | :--- |
| `code` | Short String | Numeric shortcut (e.g., `101`, `205`) for high-speed cashier entry |
| `item` | String | English dish / liquor name |
| `Marathi` | String | Devanagari script name for kitchen KOT printing (e.g., `चिकन टिक्का`) |
| `dept` | String | Routing department (`Kitchen`, `Liquior`, `Beverage`) |
| `type` | String | Category (`Veg`, `NonVeg`, `IMFL`, `CL`, `Beer`) |
| `unit` | Integer | Dispensing unit in ML (`30`, `60`, `90`, `180`, `375`, `750`, `1000`) |
| `salerate` | Currency | Standard base price (Hall/Restaurant/Garden) |
| `FAMILYRATE` | Currency | Differential rate for Family section |
| `VIPRATE` | Currency | Differential rate for VIP section |
| `ACRATE` | Currency | Differential rate for AC section |
| `WHOLESALE` | Currency | Bulk / Takeaway parcel rate |
| `exciserate` | Currency | Official government excise tariff rate |
| `purchaserate`| Currency | Standard purchase / stock inward rate |
| `bottel` | Integer | Bottle-to-case conversion factor (typically 12 for 750ml, 24 for 375ml, 48 for 180ml) |

### 3.2 Dynamic Price Tier Selection Rule
When an item is selected in `FRMENTRY`, the price charged is evaluated dynamically based on `TABLECHAR` (the first character of the table name):
```vbnet
If TABLECHAR = "F" Then
    Rate = item.FAMILYRATE
ElseIf TABLECHAR = "V" Then
    Rate = item.VIPRATE
ElseIf TABLECHAR = "A" Then
    Rate = item.ACRATE
ElseIf TABLECHAR = "P" Then
    Rate = item.salerate ' or item.WHOLESALE
Else
    Rate = item.salerate ' Base rate for Hall, Restaurant, Garden
End If
```

---

## 4. Kitchen Order Ticket (KOT) & Bar Order Ticket (BOT) Workflows

### 4.1 Order Capture & KOT Generation
1. Waiter / Cashier selects table and inputs numeric Item Code in `FRMENTRY`.
2. Quantity and unit/peg size selected.
3. Upon clicking **"Save / Print KOT"**:
   - Sequential ticket number `KOTNO` generated from `SELECT MAX(KOTNO) FROM KOTFINAL`.
   - Daily sequential counter `DAYKOT` calculated.
   - Header inserted into `KOTFINAL`.
   - Line items inserted into `KOTDETAIL`.
   - Item records written to `BILLKOT` buffer table (`PRINT = 0`).

### 4.2 Multi-Printer ESC/POS Routing
- Food items (`dept = "Kitchen"`) route to the Kitchen Thermal Printer.
- Bar items (`dept = "Liquior"`) route to the Bar Counter Printer (BOT).
- **Bilingual Printing:** The KOT engine reads `item.Marathi`. If populated, prints the Devanagari text alongside the English text on the thermal print stream.

### 4.3 KOT Cancellation (`frmCancleKOT` / `CancelKot`)
If a guest cancels an order after KOT printing:
- Requires manager privilege.
- Deleted item recorded in `CancelKot` table with `BILLNO`, `TABLE_NO`, `ITEM`, `QTY`, `RATE`, `AMT`, `DATE`, and `waiter`.
- If liquor item: Restores depleted counter stock.

---

## 5. Billing, Invoicing, Taxes & Calculation Rules

### 5.1 Pricing & Calculation Formulas

$$\text{Subtotal} = \sum (\text{Qty} \times \text{Rate})$$
$$\text{FoodTotal} = \sum \text{LineItems}_{(\text{dept} \neq \text{'Liquior'})}$$
$$\text{LiquorTotal} = \sum \text{LineItems}_{(\text{dept} = \text{'Liquior'})}$$

#### Discount Calculation
- If percentage-based: $\text{DiscountAmt} = \text{Subtotal} \times \frac{\text{Discount}\%}{100}$
- If fixed amount: $\text{DiscountAmt} = \text{DiscountFixed}$

#### Service Charge Calculation
Levied on Subtotal after Discount:
$$\text{ServiceChargeAmt} = (\text{Subtotal} - \text{DiscountAmt}) \times \frac{\text{ServiceCharge}\%}{100}$$

#### GST Tax Calculation (Food Only)
In India, liquor is outside GST (governed by State Excise VAT/Sales Tax), while restaurant food is subject to CGST + SGST.
$$\text{CGST} = \text{FoodTotal} \times \frac{\text{CGST}\%}{100} \quad (\text{Default } 2.5\%)$$
$$\text{SGST} = \text{FoodTotal} \times \frac{\text{SGST}\%}{100} \quad (\text{Default } 2.5\%)$$

$$\text{Net Payable} = \text{Subtotal} - \text{DiscountAmt} + \text{ServiceChargeAmt} + \text{CGST} + \text{SGST}$$

### 5.2 Numbering Sequences
- `BILLNO`: System-wide absolute autoincrement integer in `BILLFINAL`.
- `BILLDAY` / `BILLNUMBERDAY`: Daily sequential invoice number resetting to 1 after Day End.

### 5.3 Dynamic UPI QR Code Generation
When printing a receipt:
1. System checks `HotelInfo.upi_id` (`dinu`).
2. Constructs payload string:
   `upi://pay?pa=dinu@okaxis&pn=HOTEL%20YASHDEEP&am=NET_PAYABLE&cu=INR`
3. Uses `QRCoder.dll` to generate raster matrix bitmap.
4. Streams raster bitmap directly into ESC/POS binary stream to print scannable QR on the bill footer.

---

## 6. Payments, Settlement Modes & Cash Management

### 6.1 Settlement Types (`BILLFINAL.TYPE`)
- **Cash**: Direct cash payment.
- **Card**: Credit/Debit card transaction.
- **UPI / Online**: Digital payment via Google Pay, PhonePe, Paytm.
- **Credit (Ledger)**: Guest / Corporate account ledger entry (`CUST_NAME`).
- **Complimentary**: Owner / Management complimentary (0 total charge, logged under `TYPE = 'Cancel'` or `'Compliment'`).

### 6.2 Cash Register Operations
- Tracked via `CashBookOpen`, `CashTransfer`, `Receipt`, and `Payment` tables.

---

## 7. Multi-Tier Inventory & Stock Management

Under Maharashtra State Excise regulations for FL-III license holders, liquor must be strictly tracked across **three physical states**:

```
┌───────────────────────────────────────────────────────────────────┐
│                    GODOWN (Central Warehouse)                     │
│  - Bulk stock in cases and sealed bottles (`GodownStock`)         │
│  - Inward from vendors via Transport Permits (TP)                │
└─────────────────────────────────┬─────────────────────────────────┘
                                  │ Inward Transfer (`FRMCNT_REECIEVED`)
                                  ▼
┌───────────────────────────────────────────────────────────────────┐
│                  COUNTER STOCK (Bar Dispensary)                   │
│  - Sealed bottles behind the bar (`CNTPACK_LIVE`)                 │
│  - `OPENING` + `RECIEVED` - `SALE` - `SALELOOSE` = `CLOSING`      │
└─────────────────────────────────┬─────────────────────────────────┘
                                  │ Bottle Opened for Peg Dispensing
                                  ▼
┌───────────────────────────────────────────────────────────────────┐
│                LOOSE DISPENSARY (Open Bottle ML)                  │
│  - Measured in Millilitres (`CNTLOOSE_LIVE`)                      │
│  - Dispensed in units: 30ml, 60ml, 90ml, 180ml, 375ml             │
│  - `Opnbotlqty` tracks remaining ML in current open bottle        │
└───────────────────────────────────────────────────────────────────┘
```

### 7.1 Peg Dispensing & Automatic Bottle Opening Rules
- When a 60ml or 30ml peg is sold in `FRMENTRY`:
  1. System checks `Opnbotlqty` in `CNTPACK_LIVE`.
  2. Subtraction: `Opnbotlqty = Opnbotlqty - SoldML`.
  3. **Auto-Bottle Opening Trigger:** If `Opnbotlqty <= 0`:
     - Subtract 1 sealed bottle from `CNTPACK_LIVE.CLOSING`.
     - Add 1 bottle opened to `CNTPACK_LIVE.SALELOOSE`.
     - Reset `Opnbotlqty = BottleCapacityML + Opnbotlqty` (handling negative spillover).

### 7.2 Stock Adjustments (`FrmAdjCntStk`)
Allows manual adjustment of breakage, spillage, or missing stock:
- `AddAdjml` / `MinusAdjml` fields in `CNTPACK_LIVE`.

---

## 8. Purchases, Inwarding & Vendor Operations

### 8.1 Inward Purchase Entry Forms
- **Liquor Purchase (`frmPurchaseLiquor` / `LiqPurchaseFinal`)**: Inwarding of IMFL, Beer, CL, Wine. Records Transport Permit (TP) Number, Vendor Name, Batch Numbers, Case Counts, Bottle Counts, Purchase Rate, Excise Duty, VAT.
- **Food Purchase (`frnPurchaseFood` / `FoodPurchaseFinal`)**: Inwarding of kitchen raw materials (vegetables, meat, spices, dairy).
- **Other Purchase (`OtherPurchaseFinal`)**: Operational supplies (cleaning items, cutlery, gas cylinders).

---

## 9. Maharashtra State Excise FL-III Bar Operations & Compliance

### 9.1 Mandatory Regulatory Registers & Tables
1. **Permit Holder Register (`ExPremiteHolder` / `frmExPermitHolder`)**:
   - Under Bombay Prohibition Act, guests consuming alcohol must hold a state liquor permit.
   - Captures: Permit Name, Permit Number, Expiry Date, Address.
2. **Excise Bill Generation (`frmExMakeExcise` / `ExFinalBillDetails`)**:
   - Converts POS guest checks into legal Excise Invoices.
   - Validates total ML volume per guest check against legal permit limits (`ExUnitLimit`). If order exceeds limit (e.g. >2000ml), automatically splits into multiple Excise bills under different registered permit holders.
3. **Daily Bulk Litre Statement (`FrmDailyBulkLitre`)**:
   - Calculates total volume sold in Bulk Litres (BL) and Proof Litres (PL) per liquor class.
4. **Register 1 & Monthly Statement (`ExciseMonthlyStat`)**:
   - Monthly Opening Stock, TP Purchases, Daily Sales, Closing Stock submitted to District Excise Inspector.

---

## 10. Day End (Daily Closing Audit) Workflows

In hospitality, the business day ends when the establishment closes (e.g. 02:00 AM). The **Day End** routine (`RSS_MDI.DayendF` / `dayendProcedure`) performs sequential actions:

```
[ Trigger Day End ]
        │
        ├── 1. Check Open Tables (`BILLKOT` check)
        │      (Blocks if any table has unbilled printed KOTs)
        │
        ├── 2. Auto-Backup Database:
        │      Copies `dinurss.mdb` to `RssProblemBak\Prob_YYYY_MM_DD__HH_MM.mdb`
        │
        ├── 3. Bulk Data Rollover:
        │      INSERT INTO BILLFINAL_Dayend SELECT * FROM BILLFINAL
        │      INSERT INTO finalbill_Dayend SELECT * FROM finalbill
        │      INSERT INTO KOTFINAL_Dayend SELECT * FROM KOTFINAL
        │      INSERT INTO KOTDETAIL_Dayend SELECT * FROM KOTDETAIL
        │      INSERT INTO CNTPACK_LIVE_DAYEND SELECT * FROM CNTPACK_LIVE
        │
        ├── 4. Active Table Truncation:
        │      DELETE * FROM BILLFINAL
        │      DELETE * FROM finalbill
        │      DELETE * FROM GrandBill
        │      DELETE * FROM GrandBillDetails
        │
        ├── 5. Stock Rollover:
        │      Today's CLOSING stock balance becomes tomorrow's OPENING stock
        │      `CNTPACK_LIVE.OPENING = CNTPACK_LIVE.CLOSING`
        │      `CNTPACK_LIVE.SALE = 0`
        │      `CNTPACK_LIVE.RECIEVED = 0`
        │
        ├── 6. Reset Daily Counter Numbers:
        │      `BILLNUMBERDAY` = 1
        │      `KOTNO` = 1
        │
        ├── 7. Advance Business Date:
        │      UPDATE DAYEND SET DATE = NextBusinessDate
        │
        └── 8. Automated Sales Email:
               Generates daily sales summary and emails to `HotelInfo.Email` (`Fahadsayyed92@gmail.com`).
```

---

## 11. Accounting, Ledgers, Cash Book & Vouchers

### 11.1 General Ledger (`AccountHead`)
Contains 256 account heads for vendors, customers, expense categories, and banks.

### 11.2 Cash Book & Vouchers
- **Receipts (`FRMRECEIPT` / `Receipt`)**: Inward cash from credit customers/parties.
- **Payments (`FRMPAYMENT` / `Payment`)**: Outward cash paid to suppliers/vendors.
- **Vouchers (`FRMVOUCHER` / `Voucher`)**: Petty cash disbursements (electricity, vegetable market, laundry).

---

## 12. Security, User Roles, Master Passwords & Anti-Copy Protection

### 12.1 Authentication & User Roles (`Login` / `Module1`)
- **Admin**: Full access to Setup, Price editing, Bill deletion, Reports, Day End.
  - Default Logins: `Admin` / `333`, `ADMIN` / `admin`
- **Operator / Cashier**: Limited to POS table entry, billing, and order printing.

### 12.2 Date Lock Master (`FrmDateLckMaster` / `dateLckMaster`)
Prevents modification of past business dates. Editing past transactions requires dynamic time-based password validation.

### 12.3 Hardware Machine Lock (`HDDLOCK()` / `CpuId()`)
Ties software execution to local volume serial number (`Win32_Logicaldisk`). If volume serial does not match encrypted key in `product_key`, application locks itself with `frmLOCK`.

---

## 13. Hardware Integration, Thermal Printers & ESC/POS Routing

- **Direct Win32 GDI & Direct Stream Printing:** `PrintDocument` API directly writing raw ESC/POS command bytes (`0x1B`, `0x40`, `0x1D`, `0x56`) to thermal receipt printers.
- **Multi-Printer Setup (`frmDeptMultiPrinter` / `PRINTERSETUP`)**: Configures separate physical COM / LPT / USB thermal printers for Kitchen, Bar, and Cash Counter.

---

## 14. System Configuration & Global Constants

Stored in `HotelInfo` and `Setup` tables:
- `HotelInfo.name`: `HOTEL YASHDEEP`
- `HotelInfo.licno`: `FL III-2151444022D8ADF7`
- `HotelInfo.Vattin`: `27900111779v`
- `HotelInfo.upi_id`: `dinu`
- `HotelInfo.Email`: `Fahadsayyed92@gmail.com`
- `Setup.BillCorrLock`: Lock flag for bill corrections.
- `Setup.LiveCntStk`: Enable live counter stock deduction.

---

## 15. Comprehensive Major Workflow Trace Matrix

| Workflow | Initiating Form / Method | Primary Tables Touched | Key Calculations / Rules | Validations | Numbering / Side Effects |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **KOT Creation** | `FRMENTRY.cs` / `KOTPRINTSTART` | `KOTFINAL`, `KOTDETAIL`, `BILLKOT`, `item` | Price selected by `TABLECHAR` prefix (`F`, `V`, `A`, `P`, Standard). Marathi translation lookup from `item.Marathi`. | Item code existence, Live stock availability. | Sequential `KOTNO` and `DAYKOT` generated. Changes table color to Red. |
| **Bill Settlement** | `FRMENTRY.cs` / `PrintBILL` | `BILLFINAL`, `finalbill`, `GrandBill`, `GrandBillDetails` | Food Total vs Liquor Total split. GST applied on food only. Discount & Service Charge. Net Payable = Food + Liquor - Disc + SC + CGST + SGST. | Open KOT check, Payment amount validation. | Assigns `BILLNO` (absolute) and `BILLDAY` (daily reset). Generates UPI QR byte stream. Changes table color to Green. |
| **Peg Stock Sale** | `FRMENTRY.cs` / `LiveSTK` | `CNTPACK_LIVE`, `CNTLOOSE_LIVE` | `Opnbotlqty = Opnbotlqty - ItemML`. If `Opnbotlqty <= 0`: `CLOSING = CLOSING - 1`, `SALELOOSE = SALELOOSE + 1`, `Opnbotlqty += BottleCapacity`. | Counter stock available balance check. | Auto-opens next sealed bottle behind the bar. |
| **Godown to Counter Transfer** | `FRMCNT_REECIEVED.cs` | `GodownStock`, `CNTPACK_LIVE`, `CntRecieved_N` | `GodownStock.CLOSING -= Qty`, `CNTPACK_LIVE.RECIEVED += Qty`. | Godown closing stock >= Transfer Qty. | Decrements warehouse stock, increments bar counter stock. |
| **Excise Invoice Conversion** | `frmExMakeExcise.cs` | `BILLFINAL`, `finalbill`, `ExFinalBillDetails`, `ExPremiteHolder` | Evaluates total liquor ML per check. Checks `ExUnitLimit` (permit limit). Splits over-limit orders into separate Excise invoices. | Registered Permit Holder validation. | Assigns sequential Excise `invoice` number. Updates Excise Register 1 balance. |
| **Hotel Day End Closing** | `RSS_MDI.cs` / `DayendF` | `BILLFINAL` -> `BILLFINAL_Dayend`, `finalbill` -> `finalbillcopy_Dayend`, `DAYEND`, `CNTPACK_LIVE` | Copies active records to `_Dayend` tables. Truncates active transaction tables. Sets `CNTPACK_LIVE.OPENING = CLOSING`. Resets `BILLDAY = 1`, `KOTNO = 1`. | Blocks if unbilled `BILLKOT` lines remain. | Advances system business date. Triggers automated daily sales email dispatch. Creates `.mdb` backup in `RssProblemBak`. |

---

## 16. Legacy Technical Debt & Anti-Patterns To NOT Carry Forward

1. **Direct Table Truncation & Destruction on Day End:**
   - *Legacy Pattern:* Day End executes `DELETE * FROM BILLFINAL` and `DELETE * FROM finalbill` after copying to `*_Dayend` tables.
   - *Modern Target:* Unified `Orders` and `Invoices` tables with status flags (`IsDayEnded`, `DayEndId`, `BusinessDate`).
2. **Implicit String Prefix Pricing Logic (`TABLECHAR`):**
   - *Legacy Pattern:* Table name string starting with `F`, `V`, `A`, or `P` hardcodes the pricing tier.
   - *Modern Target:* Explicit `SectionId` foreign key linked to a configurable `PriceTier` policy.
3. **Multi-Table Shadow Schema Duplication:**
   - *Legacy Pattern:* Tables duplicated as `BILLFINAL` vs `BILLFINAL_Dayend`, `KOTDETAIL` vs `KOTDETAIL_DAYEND`, `CNTPACK_LIVE` vs `CNTPACK_LIVE_DAYEND`.
   - *Modern Target:* Single relational entity with indexed temporal / partitioning columns.
4. **Hardcoded Monolithic SQL & Unescaped String Concatenation:**
   - *Legacy Pattern:* SQL strings formed via `SELECT * FROM item WHERE item='` + `LSTITEM.Text` + `'`.
   - *Modern Target:* EF Core / Strongly-typed LINQ + Parameterized Queries.
5. **Machine Hardware-Lock Coupling:**
   - *Legacy Pattern:* Binding license to local Windows LogicalDisk serial number via WMI (`HDDLOCK()`).
   - *Modern Target:* Modern JWT / SaaS Tenant Subscription Management.
6. **Bilingual Text Stored in Presentation Tier:**
   - *Legacy Pattern:* Raw Devanagari script text stored directly in database column `item.Marathi`.
   - *Modern Target:* Structured Localization / i18n resources.

---

## 17. Contradictions & Unclear Behaviors / Open Questions

1. **Split Invoice vs Single Invoice Numbering:**
   - In `FRMENTRY.cs`, `GrandBill` and `BILLFINAL` both maintain separate billing counters (`BILLNO` vs `BILLDAY`). Clarification required on whether guest tax invoices must follow strict continuous legal sequence across Day End boundaries.
2. **Complimentary / Void Order Excise Balance Reconciliation:**
   - When a liquor order is cancelled in `CancelKot`, counter stock is restored, but behavior regarding generated Excise permit slips in `ExTemp` requires operational clarification.
3. **Negative Loose Peg Stock Handling:**
   - If `Opnbotlqty` becomes negative during peak rush before bottle opening is recorded, the system temporarily allows negative ML balance. In modern architecture, strict atomic inventory transitions are required.

---
*Document completed by Agent 2 — Legacy Domain Analyst.*
