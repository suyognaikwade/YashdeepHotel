# Business Logic & Operational Workflows

This document details the core operational workflows and domain rules reverse-engineered from the **Yashdeep Hotel Management System** (`RSS.exe` and `dinurss.mdb`).

---

## 1. Dining Sections & Differential Pricing

### 1.1 Floor Sections (`TABLE_NO_GROP`)
Hotel Yashdeep operates across 6 distinct dining areas, each serving different guest profiles:
1. **Family**: Dedicated family dining hall (quieter, child-friendly).
2. **AC**: Air-conditioned premium dining section.
3. **Hall**: Main open indoor dining room.
4. **Restaurant**: General casual dining and walk-in area.
5. **Garden**: Outdoor garden dining and lawn service.
6. **Parcel**: Takeaway / delivery packaging counter.

### 1.2 Differential Item Pricing
The menu item model (`item`) stores 5 distinct pricing tiers for the same underlying dish or liquor SKU:
- `salerate`: Base standard rate (used in Hall & Restaurant).
- `FAMILYRATE`: Tier for Family section orders.
- `VIPRATE`: Tier for VIP / Executive lounge guests.
- `ACRATE`: Premium tier for the Air Conditioned section.
- `WHOLESALE`: Bulk takeaway / package parcel pricing.

When an order is placed on a table in `FRMENTRY`, the system checks `TABLE_NO_GROP` and automatically applies the corresponding section rate.

---

## 2. Order Taking & Kitchen Order Ticket (KOT) Workflow

### 2.1 The KOT Lifecycle

```
Waitstaff Takes Order (Table T1)
               │
               ▼
   [ Fast Item Input in FRMENTRY ]
   ├── Waiter enters numeric Item Code (or searches name)
   ├── Enters Quantity & selects Peg/Unit (for bar items)
   └── Grid validates live counter stock
               │
               ▼
       [ Press "Save KOT" ]
               │
               ├── Insert into KOTDETAIL (Items: Table, Waiter, Item, Qty, Rate)
               ├── Insert into KOTFINAL (KOT ticket header with sequential DAYKOT)
               │
               ├──► If Dept == "Kitchen" (or Food)
               │       └─► Route to Kitchen Thermal Printer (ESC/POS)
               │           (Translates item to Marathi via `item.Marathi`)
               │
               └──► If Dept == "Liquior" (or Bar)
                       └─► Route to Bar Dispensing Printer (BOT)
                           Decrement `Opnbotlqty` or `CNTPACK_LIVE`
```

### 2.2 Table State Transitions
- **Vacant / White**: Table is empty and available for seating.
- **Occupied / Red (`tablered()`)**: Table has at least one active unbilled KOT.
- **Billed / Green**: Bill has been printed but payment is still pending.
- **Table Merge (`FRMTABLEMERGE`)**: Merges orders from table `TXTFROM` into table `TXTTO`.
- **Table Shift (`FRMTABLESHIFT`)**: Moves all open KOT lines from one physical table to another.

### 2.3 Bilingual KOT Printing (`PrintKOT` & `marathiname`)
Kitchen staff often speak and read Marathi. The system features a native translation routine:
- When printing KOTs to kitchen printers (`frmdeptkot`), the function `marathiname(String item)` looks up `item.Marathi`.
- The printer prints both the English and Devanagari script (e.g., `चिकन टिक्का` alongside `Chicken Tikka`) so kitchen cooks prepare the correct dish without misunderstanding.

---

## 3. Billing, Taxes & Dynamic Payment

### 3.1 Invoice Calculation Formula

$$\text{Subtotal} = \sum (\text{LineItem.Qty} \times \text{LineItem.Rate})$$
$$\text{FoodTotal} = \sum \text{LineItems}_{(\text{Dept}=\text{'Food'})}$$
$$\text{LiquorTotal} = \sum \text{LineItems}_{(\text{Dept}=\text{'Liquior'})}$$
$$\text{DiscounterAmt} = (\text{Subtotal} \times \text{DiscountPercent}) \quad \text{or} \quad \text{FixedDiscount}$$
$$\text{ServiceChargeAmt} = ((\text{Subtotal} - \text{Discount}) \times \text{SCPercent})$$
$$\text{CGST} = \text{FoodTotal} \times \frac{\text{CGST}\%}{100} \quad (\text{typically } 2.5\%)$$
$$\text{SGST} = \text{FoodTotal} \times \frac{\text{SGST}\%}{100} \quad (\text{typically } 2.5\%)$$
$$\text{NetPayable} = \text{Subtotal} - \text{Discount} + \text{ServiceCharge} + \text{CGST} + \text{SGST}$$

### 3.2 Dynamic UPI QR Code Generation
In `FRMENTRY.PrintBILL`, before sending commands to the thermal printer:
1. The app reads `HotelInfo.upi_id` (`dinu`).
2. Generates a dynamic UPI Payment URI:
   `upi://pay?pa=dinu@...&pn=HOTEL%20YASHDEEP&am=TOTAL&cu=INR`
3. Uses `QRCoder.dll` to render a 2D QR matrix.
4. Sends the raster graphic byte stream directly into the ESC/POS receipt stream so the guest can scan and pay via Google Pay, PhonePe, or Paytm right at the dining table.

---

## 4. Multi-Tier Liquor & Stock Inventory

Under Maharashtra State Excise rules, liquor must be accounted for across three physical states:

```
┌────────────────────────────────────────────────────────┐
│               GODOWN (Central Warehouse)               │
│               Tracked in `GodownStock`                 │
│  - Receives cases/bottles from licensed distributors    │
│  - Recorded via TP (Transport Permit) numbers          │
└───────────────────────────┬────────────────────────────┘
                            │ Inward Issue (CntRecieved)
                            ▼
┌────────────────────────────────────────────────────────┐
│             COUNTER STOCK (Bar Dispensary)             │
│              Tracked in `CNTPACK_LIVE`                 │
│  - Sealed bottles stored behind the bar counter        │
│  - Decremented when a whole bottle is sold or opened   │
└───────────────────────────┬────────────────────────────┘
                            │ Bottle Opened for Pegs
                            ▼
┌────────────────────────────────────────────────────────┐
│             LOOSE DISPENSARY (Open Bottles)            │
│              Tracked in `CNTLOOSE_LIVE`                │
│  - Measured in Millilitres (ML)                        │
│  - Dispensed in units: 30ml, 60ml, 90ml, 180ml (Nip)   │
│  - When `Opnbotlqty` hits 0, auto-opens next bottle    │
└────────────────────────────────────────────────────────┘
```

### 4.1 Peg Conversion Matrix (`ADJUSTMENT` & `BRANDML`)
- **Quart (Full Bottle)** = 750 ml
- **Pint (Half Bottle)** = 375 ml
- **Nip (Quarter Bottle)** = 180 ml
- **Large Peg** = 60 ml
- **Small Peg** = 30 ml
- Selling 1 Large Peg (60ml) deducts 60ml from `CNTLOOSE_LIVE.TOTALML`. Once 750ml is depleted, 1 sealed bottle is automatically subtracted from `CNTPACK_LIVE.CLOSING`.

---

## 5. Maharashtra State Excise Compliance (FL-III License)

Hotel Yashdeep holds license `FL III-2151444022D8ADF7`. State law requires daily reconciliation:
1. **Permit Holder Register (`ExPremiteHolder`)**:
   - Guests purchasing liquor must be registered with their state permit number and validity date.
2. **Daily Bulk Litre Statement (`FrmDailyBulkLitre`)**:
   - Computes total volume dispensed across IMFL (Indian Made Foreign Liquor), Country Liquor, Beer, and Wine in liters.
3. **Monthly Statement & Register 1 (`ExciseMonthlyStat`)**:
   - Opening balance on the 1st of the month.
   - Cumulative transport permits received.
   - Total liters sold per brand and size.
   - Closing stock reported to the Excise Inspector.

---

## 6. The "Day End" Procedure (`dayendProcedure` & `FrmDtpDayend`)

In hospitality, the business day does not end at midnight (24:00), but when the restaurant closes (typically 01:30 or 02:00 AM). The **Day End** routine performs the following sequential actions:

```
[ Trigger Day End: FrmDtpDayend ]
          │
          ├── 1. Check for open / unbilled tables (Blocks if any table is active)
          │
          ├── 2. Bulk Copy Active Data to Dayend History:
          │      INSERT INTO BILLFINAL_Dayend SELECT * FROM BILLFINAL
          │      INSERT INTO finalbill_Dayend SELECT * FROM finalbill
          │      INSERT INTO KOTFINAL_Dayend SELECT * FROM KOTFINAL
          │      INSERT INTO KOTDETAIL_Dayend SELECT * FROM KOTDETAIL
          │      INSERT INTO CancelKot_Dayend SELECT * FROM CancelKot
          │
          ├── 3. Stock Rollover:
          │      Today's CLOSING becomes Tomorrow's OPENING in `CNTPACK_LIVE`
          │
          ├── 4. Reset Daily Counters:
          │      `BILLNUMBERDAY` = 1
          │      `KOTNO` = 1
          │
          ├── 5. Advance Business Date:
          │      UPDATE DAYEND SET DATE = NextBusinessDate
          │
          └── 6. Email Automated Sales Summary:
                 Sends daily sales figures and department totals to
                 `HotelInfo.Email` (`Fahadsayyed92@gmail.com`).
```

---

## 7. Security, Auditing & Date Locking

- **Bill Correction Lock (`BillCorrLock` in `Setup`)**:
  Prevents cashiers from altering or deleting settled bills without manager authorization.
- **Date Lock Master (`dateLckMaster` / `FrmDateLockPass`)**:
  Locks historical dates from modification. To edit a past transaction, a dynamic time-based master password must be entered.
- **Hard Drive Fingerprinting (`HDDLOCK()` / `CpuId()`)**:
  The application ties licensing to local motherboard / CPU / HDD serial numbers (`serialkey`, `softkey`, `product_key`) to prevent unauthorized software copying.
