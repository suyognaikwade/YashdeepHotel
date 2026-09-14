# Yashdeep Hotel MS - Printing & Hardware Architecture Specification

> **Module**: Thermal Receipts (58mm / 80mm), A4 Tax Invoices, KOT/BOT Routing, ESC/POS Binary Commands, Marathi Bilingual Printing, Dynamic UPI QR Rasterization & Offline Spooling
> **Status**: Approved Architectural Specification
> **Target Platform**: .NET 9 Blazor Hybrid (MAUI), QuestPDF, SkiaSharp, ESC/POS Driver, SQLite Local Spooler Queue

---

## 1. Executive Summary & Legacy Analysis

### 1.1 Legacy Implementation Overview
In the legacy system, physical printing was handled via GDI+ `PrintDocument` page drawing routines (`PrintBILL`, `PrintKOT`, `PrintKotDept` in `FRMENTRY.vb`) and direct LPT/COM raw port output stored in the `PRINTERSETUP` table.
- **Thermal Printing**: Hardcoded line-by-line GDI+ coordinate drawing (`e.Graphics.DrawString`).
- **Kitchen Tickets (KOT)**: Routed based on `ItemDept.printer` settings, calling `marathiname()` to fetch Devanagari text from `item.Marathi`.
- **Dynamic UPI QR**: Generated as PNG via `QRCoder.dll` and drawn onto GDI+ print surfaces.

### 1.2 Flaws & Failure Modes in Legacy Architecture

| Legacy Flaw | Impact & Failure Mode | Modern Architectural Solution |
| :--- | :--- | :--- |
| **Windows Print Spooler Hangs** | Printer offline errors caused entire VB.NET UI thread to freeze or crash (`0x80004005`). | Asynchronous, non-blocking background print queue worker using SQLite local spooling. |
| **GDI+ Raster Scaling Distortions** | Drawing text as Windows GDI+ coordinates produced blurry or misaligned text on 80mm/58mm thermal printers. | Native ESC/POS raw binary stream generation for text + high-contrast 1-bit raster graphics for images/Devanagari. |
| **Devanagari Encoding Failures** | Thermal printers lacking built-in Marathi codepages printed garbled characters (`???`). | SkiaSharp bitmap rasterization of Marathi script into 1-bit ESC/POS graphics streams (`GS v 0`). |
| **No Printer Redundancy** | If a kitchen printer ran out of paper, KOT tickets were silently lost with no fallback. | Smart Fallback Engine: Auto-detects paper out / offline status and reroutes ticket to backup counter printer with visual alert. |

---

## 2. Hardware Topology & Print Target Formats

```
                                  ┌───────────────────────────┐
                                  │   Print Dispatcher Engine │
                                  └─────────────┬─────────────┘
                                                │
         ┌──────────────────────────────────────┼──────────────────────────────────────┐
         ▼                                      ▼                                      ▼
┌─────────────────────────┐            ┌─────────────────────────┐            ┌─────────────────────────┐
│  58mm Thermal (203 DPI) │            │  80mm Thermal (203 DPI) │            │    A4 Tax Invoice Doc   │
│  - Line Width: 32 chars │            │  - Line Width: 48 chars │            │  - Laser / Inkjet       │
│  - Parcel / Bar Slips   │            │  - Guest Dine-in Bills  │            │  - Formal Tax Invoice   │
│  - Compact KOT Tickets  │            │  - Dynamic UPI QR       │            │  - B2B & Excise Returns │
└─────────────────────────┘            └─────────────────────────┘            └─────────────────────────┘
```

### 2.1 Print Target Specifications

| Format | Resolution / Width | Character Capacity | Primary Use Case | Transport Protocol |
| :--- | :--- | :--- | :--- | :--- |
| **58mm Thermal** | 203 DPI (384 dots/line) | 32 Chars (Font A: 12x24) | Express takeaway slips, Bar BOT tickets | USB HID, Serial (COM), Bluetooth SPP |
| **80mm Thermal** | 203 DPI (576 dots/line) | 48 Chars (Font A: 12x24) | Main dining guest receipts, Full KOT slips | Direct TCP/IP (Port 9100), USB |
| **A4 Sheet** | 300+ DPI (Standard Page) | Full Page Layout | Formal Tax Invoices, Monthly Credit Ledger | Windows Spooler / CUPS / Network PDF |

---

## 3. Native ESC/POS Binary Command Engine

Instead of relying on OS printer drivers, the system generates direct **ESC/POS byte streams** sent straight to raw network or USB endpoints.

### 3.1 ESC/POS Byte Command Definitions

```csharp
namespace Yashdeep.Infrastructure.Printing.EscPos;

public static class EscPosCommands
{
    public static readonly byte[] InitializePrinter = [0x1B, 0x40];           // ESC @
    public static readonly byte[] SelectFontA = [0x1B, 0x4D, 0x00];           // ESC M 0
    public static readonly byte[] SelectFontB = [0x1B, 0x4D, 0x01];           // ESC M 1

    // Formatting
    public static readonly byte[] AlignLeft = [0x1B, 0x61, 0x00];             // ESC a 0
    public static readonly byte[] AlignCenter = [0x1B, 0x61, 0x01];           // ESC a 1
    public static readonly byte[] AlignRight = [0x1B, 0x61, 0x02];            // ESC a 2

    public static readonly byte[] BoldOn = [0x1B, 0x45, 0x01];                // ESC E 1
    public static readonly byte[] BoldOff = [0x1B, 0x45, 0x00];               // ESC E 0

    public static readonly byte[] DoubleHeightWidth = [0x1D, 0x21, 0x11];     // GS ! 0x11
    public static readonly byte[] NormalSize = [0x1D, 0x21, 0x00];            // GS ! 0x00

    // Hardware Actions
    public static readonly byte[] PartialCut = [0x1D, 0x56, 0x42, 0x00];       // GS V 66 0
    public static readonly byte[] FullCut = [0x1D, 0x56, 0x00];              // GS V 0
    public static readonly byte[] OpenCashDrawer = [0x1B, 0x70, 0x00, 0x19, 0xFA]; // ESC p 0 25 250
}
```

---

## 4. Kitchen (KOT) & Bar (BOT) Ticket Routing

Orders entered in Blazor POS are evaluated by the `OrderRoutingService`, which splits line items by department (`Kitchen` vs `Liquor`) and routes them to dedicated physical printers.

```
                             [ Order Submitted ]
                                      │
               ┌──────────────────────┴──────────────────────┐
               ▼                                             ▼
      [ Department == Food ]                      [ Department == Liquor ]
               │                                             │
               ▼                                             ▼
  [ Kitchen Printer Queue ]                     [ Bar Printer Queue ]
  ├── Generate Bilingual Slip                   ├── Generate BOT Slip
  ├── Render Marathi Devanagari                 ├── Decrement Loose ML Volume
  └── Send to Kitchen IP (192.168.1.100)        └── Send to Bar USB/IP Printer
```

---

## 5. Bilingual Marathi/Devanagari Thermal Printing Strategy

Thermal printer hardware NVRAM typically lacks Marathi Devanagari character sets. The modern architecture solves this via **SkiaSharp Memory Canvas Rasterization**.

### 5.1 SkiaSharp Rasterization Pipeline

```csharp
public byte[] RenderMarathiTextToEscPosRaster(string englishName, string marathiText, int qty, int paperWidthDots = 576)
{
    using var bitmap = new SKBitmap(paperWidthDots, 80);
    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(SKColors.White);

    using var paintText = new SKPaint
    {
        Color = SKColors.Black,
        TextSize = 28,
        IsAntialias = false, // Crisp monochrome for 203 DPI thermal
        Typeface = SKTypeface.FromFamilyName("Noto Sans Devanagari", SKFontStyle.Bold)
    };

    // Draw English + Marathi Text on SKCanvas
    canvas.DrawText($"{qty} x {englishName}", 10, 30, paintText);
    canvas.DrawText($"   ({marathiText})", 10, 68, paintText);

    // Convert SKBitmap to 1-bit ESC/POS Raster Bytes (`GS v 0`)
    return ConvertBitmapToEscPosRaster(bitmap);
}
```

---

## 6. Dynamic UPI QR Code In-Stream Printing

For 80mm guest bills, dynamic UPI QR codes are rendered as 200x200 1-bit monochrome bitmaps and embedded directly into the receipt footer before the cutter command.

```
         ┌──────────────────────────────────────────────┐
         │               HOTEL YASHDEEP                 │
         │           Bhenda, Maharashtra                │
         │----------------------------------------------│
         │ Bill No: INV-YASH-202503-0012               │
         │ Date: 13/03/2025 21:45                       │
         │ Table: T-4 (AC)          Waiter: Ramesh      │
         │----------------------------------------------│
         │ 1  Paneer Butter Masala          ₹ 240.00    │
         │ 2  Butter Naan                   ₹  80.00    │
         │ 1  Chicken Tikka                 ₹ 320.00    │
         │----------------------------------------------│
         │ Subtotal:                        ₹ 640.00    │
         │ CGST (2.5%):                     ₹  16.00    │
         │ SGST (2.5%):                     ₹  16.00    │
         │ NET PAYABLE:                     ₹ 672.00    │
         │----------------------------------------------│
         │           SCAN & PAY WITH ANY UPI            │
         │                                              │
         │               ┌──────────────┐               │
         │               │  █▀▀▀▀▀█ ▄   │               │
         │               │  █ ███ █ █▀  │               │
         │               │  █ ▀▀▀ █ ▄▀  │               │
         │               │  ▀▀▀▀▀▀▀ ▀   │               │
         │               └──────────────┘               │
         │            GPay / PhonePe / Paytm            │
         │----------------------------------------------│
         │           Thank You! Visit Again!            │
         └──────────────────────────────────────────────┘
```

---

## 7. Offline Local Spooling & Print Queue Engine

To guarantee zero print loss during network drops or printer outages, all print jobs are written to a local SQLite **Print Queue Table** before transmission.

```csharp
public class PrintJobRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string PrinterName { get; init; } = string.Empty;
    public string TargetEndpoint { get; init; } = string.Empty; // IP:Port or USB Path
    public byte[] BinaryPayload { get; init; } = Array.Empty<byte>();
    public PrintJobStatus Status { get; set; } = PrintJobStatus.Queued;
    public int RetryCount { get; set; } = 0;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public string? LastError { get; set; }
}
```

---

## 8. Printer Fallback & Redundancy Strategy

When a primary printer is unreachable or out of paper (`DLE EOT 1` status poll):
1. **Status Polling**: Background worker polls `DLE EOT 1` (Printer Status) prior to job transmission.
2. **Automatic Rerouting**: If Kitchen Printer 1 fails, job automatically reroutes to Backup Kitchen Printer 2 or Cashier Counter Printer.
3. **Alert Banner**: Appends prominent warning header on rerouted slips: `*** REROUTED TICKET - CHECK KITCHEN PRINTER 1 ***`.
