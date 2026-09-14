# Restaurant POS Domain Correctness & Invariant Audit Review

**Date:** May 2025
**Target Branch:** `botify`
**Status:** Audit Completed & Domain Hardened
**Scope:** `Order`, `OrderItem`, `KotRecord`, `Bill`, `BillTaxLine`, `Payment`, `StockMovement`, `AuditEvent`, `CompletePosWorkflowUseCase`, `PosTerminalUiController`

---

## 1. Executive Summary

This correctness review evaluates the restaurant POS domain implementation established in the vertical slice. The audit focused on state transition guarantees, financial correctness, tax calculation policies, multi-tier inventory movement, split-tender payment reconciliation, time-provider determinism, and multi-tenant domain boundary isolation.

### Key Audit Findings & Remediation Summary

1. **Deterministic Time Abstraction (`IDateTimeProvider`)**:
   - **Finding**: Direct usage of `DateTime.UtcNow` existed across domain constructors (`Order`, `KotRecord`, `Bill`, `Payment`, `StockMovement`, `AuditEvent`, `OutboxMessage`).
   - **Fix**: Injected `IDateTimeProvider` (or timestamp parameter derived from `IDateTimeProvider`) into all entity constructors and workflow methods, completely removing direct non-deterministic `DateTime.UtcNow` calls in domain logic.

2. **Order Immutability Post-Billing**:
   - **Finding**: Mutation operations like `AddItem`, `GenerateKot`, and re-billing were not strictly guarded against `OrderStatus.Billed` or `OrderStatus.Completed`.
   - **Fix**: Added strict guards in `Order.AddItem`, `Order.GenerateKot`, and `Order.MarkBilled` throwing `InvalidOperationException` if an order is not in `OrderStatus.Open`.

3. **Preventing Double Billing**:
   - **Finding**: `Order.MarkBilled()` could be invoked multiple times without error.
   - **Fix**: `Order.MarkBilled()` now verifies `Status == OrderStatus.Open` and throws if already billed or completed.

4. **Split Payment & Over-Settlement Protection**:
   - **Finding**: Payments could exceed the bill's `GrandTotal` / `BalanceDue` without explicit permission.
   - **Fix**: Hardened `Bill.AddPayment` and `CompletePosWorkflowUseCase` to enforce exact balance reconciliation and reject over-settling unless `allowOverpayment` is explicitly set to `true`.

5. **Value Object & Quantity Validation**:
   - **Finding**: Negative money amounts and zero/negative quantities were improperly guarded in parts of the domain.
   - **Fix**: Hardened `Money` struct to reject negative amounts, `OrderItem` to reject quantity $\le 0$, and `StockMovement` to reject quantity $\le 0$ unless explicitly permitted.

6. **Tax Calculation Policies**:
   - **Finding**: Unexplained inline hardcoded tax percentages (2.5% CGST, 2.5% SGST) existed in workflow logic.
   - **Fix**: Extracted tax parameters into `PosTaxPolicyOptions` DTO and validated tax rate non-negativity in `BillTaxLine`.

7. **Item Consolidation & KOT Generation**:
   - **Finding**: Repeated item additions with matching `MenuItemId` and `UnitPrice` required clean consolidation rules.
   - **Fix**: `Order.AddItem` consolidates unsent order items matching `MenuItemId` and `UnitPrice`. Items already sent to KOT are preserved separately. KOT generation validates that unsent items exist for the requested department before generating a ticket.

---

## 2. Verified Invariant Matrix

| Domain Entity / Area | Invariant Rule | Enforcement Mechanism | Status |
| :--- | :--- | :--- | :--- |
| `Order` | Order Immutability | `Status == OrderStatus.Open` checked before item addition or KOT generation. | Verified |
| `Order` | Single Billing Guarantee | `MarkBilled()` fails if order is already billed/completed. | Verified |
| `Order` | Item Consolidation | Merges unsent items with same `MenuItemId` & `UnitPrice`. Keeps KOT-sent items separate. | Verified |
| `KotRecord` | Department Routing | `KotKitchen` maps to `Kitchen`, `BotBar` maps to `Bar`. Throws if 0 unsent items. | Verified |
| `Bill` | Over-settlement Guard | `AddPayment` throws if total payments exceed `GrandTotal` unless overpayment flag set. | Verified |
| `Payment` | Exact Reconciled Split | Split tenders (`Cash` + `UPI` + `Card`) must sum exactly to `GrandTotal`. | Verified |
| `Money` | Currency & Non-negativity | Rejects negative amounts (`Amount < 0`) and currency mismatch operations. | Verified |
| `StockMovement` | Audit Trail Integrity | Rejects non-positive quantities for sale deductions; links `ReferenceTransactionId`. | Verified |
| `AuditEvent` | Audit Recording | Atomically generated and persisted within `LocalPosUnitOfWork`. | Verified |
| Domain Wide | Time Authority | All entity creation & status timestamps sourced via `IDateTimeProvider`. | Verified |
| Domain Wide | Tenant Boundary Isolation | `TenantId` and `BranchId` validated across Order, Bill, Payment, Stock, and Audit. | Verified |

---

## 3. Verification & Automated Test Coverage

The test suite in `tests/Yashdeep.Tests/PosVerticalSliceTests.cs` was expanded to validate all edge cases and domain invariants.

### Test Cases Executed

1. `Domain_Money_OperatorsAndRounding_BehaveWithExactPrecision`: Verifies monetary rounding and exact arithmetic.
2. `Domain_Money_NegativeAmount_ThrowsArgumentOutOfRangeException`: Verifies negative money amounts are rejected.
3. `Domain_Bill_TaxCalculationPolicy_ComputesCgstSgstCorrectly`: Verifies CGST/SGST policy tax calculation.
4. `Domain_Order_BilingualMarathiItem_GeneratesKotWithMarathiNames`: Verifies Marathi localization preservation on KOT line items.
5. `Domain_Order_MutationAfterBilling_ThrowsInvalidOperationException`: Verifies post-billing item additions and KOT generation are rejected.
6. `Domain_Order_ItemConsolidation_ConsolidatesUnsentItems`: Verifies item merging pre-KOT and separate line creation post-KOT.
7. `Domain_Order_DuplicateKotGeneration_ThrowsWhenNoUnsentItems`: Verifies duplicate KOT generation attempts with 0 unsent items are rejected.
8. `Domain_Bill_OverSettlement_RejectsPaymentExceedingBalanceDue`: Verifies over-settlement rejection.
9. `Domain_Bill_TenantOrBranchMismatch_ThrowsInvalidOperationException`: Verifies tenant/branch context mismatches are rejected.
10. `OfflineIntegration_WorkflowCompletesOffline_PersistsAtomically_CreatesOutbox_AndSyncsWhenOnline`: End-to-end integration test with offline outbox persistence, printer verification, and idempotent cloud synchronization.
11. `Security_TenantIsolation_EnforcesStrictDataSeparation`: Cross-tenant boundary query isolation check.

### Test Execution Output

```
Test run for /app/tests/Yashdeep.Tests/bin/Debug/net10.0/Yashdeep.Tests.dll (.NETCoreApp,Version=v10.0)
VSTest version 18.0.1 (x64)

Passed!  - Failed:     0, Passed:    32, Skipped:     0, Total:    32, Duration: 435 ms - Yashdeep.Tests.dll (net10.0)
```

---

## 4. Conclusion

All restaurant POS domain invariants, state transition rules, money value object guards, tax policy configurations, and time provider abstractions have been verified and hardened. The vertical slice implementation is deterministic, testable, and compliant with system architecture requirements.
