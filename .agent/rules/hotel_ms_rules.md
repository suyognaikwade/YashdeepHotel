# Agent Rules for Yashdeep Hotel Management System

These rules govern all AI agent actions within the `YashdeepHotelMS` repository.

## 1. Context Awareness & System Identity
- This repository represents **Yashdeep Hotel Management System (RSS / Real Soft)**.
- Business Type: Combined Hotel, Multi-section Restaurant, and Maharashtra State Excise FL-III Licensed Bar.
- Current codebase state: Legacy compiled .NET 4.0 WinForms application with Access Jet database (`RSS26/`), fully decoded schemas, reverse-engineered metadata, and modernization specifications.

## 2. Security & Credentials
- Database `RSS26/dinurss.mdb` has password: `rss1008`.
- Backup databases (`dinurss - Copy.mdb`, `OLD.mdb`) have password: `dinu`.
- Legacy user logins: `Admin` (PW: `333`), `ADMIN` (PW: `admin`).
- NEVER commit plain-text credentials in newly written modernization code; use environment variables, user secrets, or Key Vault.

## 3. Architecture & Modernization Rules
- **No Direct MDB Dependencies in Target Architecture**: The modern rewrite MUST NOT rely on Microsoft Access or OLEDB. Modern code must use **PostgreSQL** for cloud / server and **SQLite** for edge / offline POS terminals.
- **Offline-First Mandate**: The restaurant POS and KOT printing MUST function seamlessly even if the internet connection is completely lost. Orders, bills, and stock movements must be queued locally in an **Outbox Pattern** and synced when online.
- **Reporting Architecture**: Replace legacy Crystal Reports (`.rpt`) with **QuestPDF** code-first templates that support ESC/POS thermal printers (80mm and 58mm) and standard A4 invoices.
- **Marathi / Multilingual Support**: Preserve bilingual support (Marathi Devanagari script `रिअल सॉफ्ट` and English) for item names, KOT kitchen slips, and bills.
- **Excise Compliance**: Preserve strict state excise tracking logic (Permit holders, daily bulk litre consumption, godown-to-counter transfers, bottle opening, and monthly statements).

## 4. Coding & File Management Standards
- Do NOT modify or delete raw binary files in `RSS26/`.
- Generated migration scripts belong in `schema_extracted/` or `src/Yashdeep.Migration/`.
- Modern C# code must target **.NET 9** with C# 13, `Nullable: enable`, and strict styling.
- Domain terminology must match standard Indian hospitality conventions (`KOT`, `BOT`, `Godown`, `Counter`, `Table Group/Section`, `Peg`, `Day End`).
