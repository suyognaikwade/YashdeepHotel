# Agent Operational Rules for Yashdeep Hotel Management System

These rules govern all AI agent actions within the `YashdeepHotelMS` workspace.

## 1. Context Awareness & System Identity
- System: **Yashdeep Hotel Management System (RSS / Real Soft)**.
- Domain: Multi-section Hotel, Restaurant, and Maharashtra State Excise FL-III Licensed Bar.
- Current Repository State: Legacy binaries and database (`RSS26/`), extracted schemas (`schema_extracted/`), comprehensive domain documentation (`docs/`), and target modernization specs (.NET 9 + Blazor Hybrid + SQLite + PostgreSQL).

## 2. Security & Credentials Rules
- Production database `RSS26/dinurss.mdb` password: `<PRODUCTION_MDB_PASSWORD>`.
- Backup databases (`dinurss - Copy.mdb`, `OLD.mdb`) Password: `<BACKUP_MDB_PASSWORD>`.
- Default application logins: `Admin` (PW: `<DEFAULT_ADMIN_PASSWORD>`), `ADMIN` (PW: `<DEFAULT_ADMIN_PASSWORD>`).
- NEVER commit plain-text credentials in new code; use environment variables, user secrets, or Key Vault.

## 3. Architecture & Target Stack Rules
- **No Direct Access Jet Dependencies**: Target modern architecture MUST NOT rely on Access Jet or OLEDB. Modern code must use **PostgreSQL** for cloud/server and **SQLite** for edge POS terminals.
- **Offline-First Mandate**: POS order taking and thermal printing MUST function seamlessly offline via an **Outbox Pattern** sync engine.
- **Reporting Architecture**: Migrate Crystal Reports to **QuestPDF** code-first templates supporting ESC/POS thermal printers (80mm/58mm) and A4 invoices.
- **Bilingual Support**: Preserve Devanagari script UTF-8 strings (`item.Marathi`, e.g., `चिकन टिक्का`) for KOT slips and menu displays.
- **Excise Compliance**: Preserve strict state excise tracking rules (Permit holders, daily bulk litre calculations, bottle opening, and monthly statements).

## 4. Protected Assets & Operating Constraints
- Do NOT modify or delete raw legacy files under `RSS26/`.
- Generated metadata belongs in `schema_extracted/`.
- Modern C# code must target **.NET 9** with `Nullable: enable` and Clean Architecture conventions.
- Retain domain terminology (`KOT`, `BOT`, `Godown`, `Counter`, `Peg`, `Day End`).
