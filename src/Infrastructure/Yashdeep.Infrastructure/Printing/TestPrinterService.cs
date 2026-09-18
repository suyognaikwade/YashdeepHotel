using System.Collections.Concurrent;
using System.Text.Json;
using Yashdeep.Domain.Entities.Orders;
using Yashdeep.Domain.Entities.Billing;
using Yashdeep.Application.Common.Interfaces;

namespace Yashdeep.Infrastructure.Printing;

public class TestPrinterService : IPrinterService
{
    private readonly ConcurrentBag<string> _printedKotReceipts = new();
    private readonly ConcurrentBag<string> _printedBillReceipts = new();

    public IReadOnlyCollection<string> PrintedKotReceipts => _printedKotReceipts.ToArray();
    public IReadOnlyCollection<string> PrintedBillReceipts => _printedBillReceipts.ToArray();

    public Task<bool> PrintKotAsync(KotRecord kot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kot);
        var kotPrintData = JsonSerializer.Serialize(new
        {
            KotNumber = kot.KotNumber,
            TicketType = kot.TicketType.ToString(),
            TableNumber = kot.TableNumber,
            WaiterName = kot.WaiterName,
            PrintedAt = kot.PrintedAtUtc,
            Items = kot.LineItems.Select(i => new { i.ItemCode, i.EnglishName, i.MarathiName, i.Quantity, i.UnitVolumeMl })
        });

        _printedKotReceipts.Add(kotPrintData);
        return Task.FromResult(true);
    }

    public Task<bool> PrintReceiptAsync(Bill bill, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bill);
        var billPrintData = JsonSerializer.Serialize(new
        {
            InvoiceNumber = bill.InvoiceNumber,
            DailySequenceNumber = bill.DailySequenceNumber,
            TableNumber = bill.TableNumber,
            WaiterName = bill.WaiterName,
            SubTotal = bill.SubTotal.Amount,
            TotalDiscount = bill.TotalDiscount.Amount,
            TotalTax = bill.TotalTax.Amount,
            GrandTotal = bill.GrandTotal.Amount,
            TotalPaid = bill.TotalPaid.Amount,
            BalanceDue = bill.BalanceDue.Amount,
            PaymentStatus = bill.PaymentStatus.ToString()
        });

        _printedBillReceipts.Add(billPrintData);
        return Task.FromResult(true);
    }

    public void ClearPrintHistory()
    {
        _printedKotReceipts.Clear();
        _printedBillReceipts.Clear();
    }
}
