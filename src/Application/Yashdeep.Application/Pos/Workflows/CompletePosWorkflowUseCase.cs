using System.Text.Json;
using Yashdeep.Application.Common.Interfaces;
using Yashdeep.Application.Pos.DTOs;
using Yashdeep.Domain.Entities.Audit;
using Yashdeep.Domain.Entities.Billing;
using Yashdeep.Domain.Entities.Inventory;
using Yashdeep.Domain.Entities.Orders;
using Yashdeep.Domain.Outbox;
using Yashdeep.Domain.ValueObjects;
using Yashdeep.Shared.Time;

namespace Yashdeep.Application.Pos.Workflows;

public class CompletePosWorkflowUseCase
{
    private readonly ILocalPosUnitOfWork _unitOfWork;
    private readonly IPrinterService _printerService;
    private readonly ICloudSyncEngine _syncEngine;
    private readonly IDateTimeProvider _timeProvider;

    public CompletePosWorkflowUseCase(
        ILocalPosUnitOfWork unitOfWork,
        IPrinterService printerService,
        ICloudSyncEngine syncEngine,
        IDateTimeProvider? timeProvider = null)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _printerService = printerService ?? throw new ArgumentNullException(nameof(printerService));
        _syncEngine = syncEngine ?? throw new ArgumentNullException(nameof(syncEngine));
        _timeProvider = timeProvider ?? new SystemDateTimeProvider();
    }

    public async Task<PosWorkflowResult> ExecuteAsync(
        CompletePosWorkflowCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(command));
        if (command.BranchId == Guid.Empty) throw new ArgumentException("BranchId is required.", nameof(command));
        if (command.Items == null || command.Items.Count == 0)
            throw new InvalidOperationException("Cannot process workflow with no items.");

        // 1. Order Creation & Item Allocation
        var order = new Order(
            Guid.NewGuid(),
            command.TenantId,
            command.BranchId,
            command.LocationId,
            command.TableNumber,
            command.Section,
            command.OrderType,
            command.OrderNumber,
            command.BusinessDate,
            command.CaptainUserId,
            command.WaiterName,
            _timeProvider
        );

        foreach (var itemReq in command.Items)
        {
            order.AddItem(
                itemReq.MenuItemId,
                itemReq.ItemCode,
                itemReq.EnglishName,
                itemReq.MarathiName,
                itemReq.Department,
                itemReq.Quantity,
                new Money(itemReq.UnitPrice),
                itemReq.UnitVolumeMl
            );
        }

        await _unitOfWork.Orders.AddAsync(order, cancellationToken);

        // 2. KOT / BOT Generation where operationally applicable
        var kotIds = new List<Guid>();
        var kotsToPrint = new List<KotRecord>();

        bool hasKitchenItems = order.Items.Any(i => i.Department == DepartmentType.Kitchen);
        if (hasKitchenItems)
        {
            string kitchenKotNo = $"KOT-{command.BusinessDate:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpperInvariant()}";
            var kitchenKot = order.GenerateKot(KotTicketType.KotKitchen, kitchenKotNo, _timeProvider);
            kotIds.Add(kitchenKot.Id);
            kotsToPrint.Add(kitchenKot);
        }

        bool hasBarItems = order.Items.Any(i => i.Department == DepartmentType.Bar);
        if (hasBarItems)
        {
            string barBotNo = $"BOT-{command.BusinessDate:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpperInvariant()}";
            var barBot = order.GenerateKot(KotTicketType.BotBar, barBotNo, _timeProvider);
            kotIds.Add(barBot.Id);
            kotsToPrint.Add(barBot);
        }

        // 3. Bill Generation with safe sequence numbering & explicit tax policy
        long dailySeq = await _unitOfWork.Bills.GetNextDailySequenceNumberAsync(
            command.TenantId,
            command.BranchId,
            command.BusinessDate,
            cancellationToken
        );

        string invoiceNo = $"INV-{command.BranchId.ToString()[..4].ToUpperInvariant()}-{command.BusinessDate:yyyyMMdd}-{dailySeq:D4}";

        var taxPolicy = command.TaxPolicy ?? new PosTaxPolicyOptions();

        var bill = new Bill(
            Guid.NewGuid(),
            command.TenantId,
            command.BranchId,
            command.LocationId,
            order.Id,
            command.BusinessDate,
            invoiceNo,
            dailySeq,
            command.TableNumber,
            command.WaiterName,
            order.CalculateFoodSubTotal(),
            order.CalculateLiquorSubTotal(),
            _timeProvider,
            command.DiscountPercentage,
            foodCgstPercent: taxPolicy.FoodCgstPercent,
            foodSgstPercent: taxPolicy.FoodSgstPercent,
            liquorVatPercent: taxPolicy.LiquorVatPercent
        );

        order.MarkBilled();

        // 4. Payment Recording (supporting split tenders)
        decimal totalPaymentAmount = command.Payments?.Sum(p => p.Amount) ?? 0m;

        if (command.Payments == null || command.Payments.Count == 0)
            throw new InvalidOperationException("At least one payment method is required.");

        if (!command.AllowOverpayment && totalPaymentAmount > bill.GrandTotal.Amount)
        {
            throw new InvalidOperationException($"Total payments ({totalPaymentAmount}) exceed bill grand total ({bill.GrandTotal.Amount}). Split payments must reconcile exactly.");
        }

        foreach (var payReq in command.Payments)
        {
            var payment = new Payment(
                Guid.NewGuid(),
                bill.Id,
                command.TenantId,
                command.BranchId,
                payReq.Method,
                new Money(payReq.Amount),
                payReq.TransactionReference,
                command.CashierUserId,
                _timeProvider
            );

            bill.AddPayment(payment, _timeProvider, command.AllowOverpayment);
        }

        if (bill.PaymentStatus == PaymentStatus.Paid)
        {
            order.MarkCompleted(_timeProvider);
        }

        await _unitOfWork.Bills.AddAsync(bill, cancellationToken);
        await _unitOfWork.Orders.UpdateAsync(order, cancellationToken);

        // 5. Inventory Movement (Auditable stock deductions tied to transaction)
        var stockMovementIds = new List<Guid>();
        foreach (var item in order.Items)
        {
            int? volumeDeducted = item.UnitVolumeMl.HasValue ? (int)(item.UnitVolumeMl.Value * item.Quantity) : null;
            var movement = new StockMovement(
                Guid.NewGuid(),
                command.TenantId,
                command.BranchId,
                item.MenuItemId,
                item.ItemCode,
                item.EnglishName,
                StockMovementType.SaleDeduction,
                item.Quantity,
                _timeProvider,
                volumeDeducted,
                referenceTransactionId: bill.Id
            );
            await _unitOfWork.Stock.AddMovementAsync(movement, cancellationToken);
            stockMovementIds.Add(movement.Id);
        }

        // 6. Audit Event Record
        var auditEvent = new AuditEvent(
            Guid.NewGuid(),
            command.TenantId,
            command.BranchId,
            "POS_WORKFLOW_COMPLETED",
            "BILL_SETTLED",
            command.CaptainUserId,
            command.WaiterName,
            bill.Id,
            JsonSerializer.Serialize(new
            {
                OrderId = order.Id,
                BillId = bill.Id,
                InvoiceNumber = bill.InvoiceNumber,
                GrandTotal = bill.GrandTotal.Amount,
                PaymentStatus = bill.PaymentStatus.ToString()
            }),
            _timeProvider
        );
        await _unitOfWork.Audits.AddEventAsync(auditEvent, cancellationToken);

        // 7. Local Offline Persistence & Outbox Event Generation
        var outboxEventIds = new List<Guid>();

        var outboxPayload = JsonSerializer.Serialize(new
        {
            OrderId = order.Id,
            BillId = bill.Id,
            InvoiceNumber = bill.InvoiceNumber,
            GrandTotal = bill.GrandTotal.Amount,
            TenantId = command.TenantId,
            BranchId = command.BranchId,
            BusinessDate = command.BusinessDate
        });

        var outboxMsg = new OutboxMessage(
            Guid.NewGuid(),
            "Bill",
            bill.Id,
            command.TenantId,
            command.BranchId,
            command.DeviceId,
            bill.DailySequenceNumber,
            "PosTransactionCompletedEvent",
            1,
            _timeProvider.UtcNow,
            outboxPayload
        );
        await _unitOfWork.Outbox.AddMessageAsync(outboxMsg, cancellationToken);
        outboxEventIds.Add(outboxMsg.EventId);

        // Atomic commit to local POS storage
        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        // Print KOTs and Bill Receipts via Printer abstraction
        foreach (var kot in kotsToPrint)
        {
            await _printerService.PrintKotAsync(kot, cancellationToken);
        }
        await _printerService.PrintReceiptAsync(bill, cancellationToken);

        // 8. Background / Restored Cloud Synchronization
        bool cloudSynced = false;
        bool isOnline = await _syncEngine.IsNetworkAvailableAsync(cancellationToken);
        if (isOnline)
        {
            int syncedCount = await _syncEngine.SynchronizePendingOutboxAsync(command.TenantId, cancellationToken);
            cloudSynced = syncedCount > 0;
        }

        return new PosWorkflowResult(
            OrderId: order.Id,
            BillId: bill.Id,
            InvoiceNumber: bill.InvoiceNumber,
            DailySequenceNumber: bill.DailySequenceNumber,
            GrandTotal: bill.GrandTotal,
            TotalPaid: bill.TotalPaid,
            PaymentStatus: bill.PaymentStatus,
            KotIds: kotIds,
            StockMovementIds: stockMovementIds,
            AuditEventId: auditEvent.Id,
            OutboxEventIds: outboxEventIds,
            OfflineSaved: true,
            CloudSynced: cloudSynced
        );
    }
}
