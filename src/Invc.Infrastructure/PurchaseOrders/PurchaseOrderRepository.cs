using Dapper;
using Invc.Core.Inventory;
using Invc.Core.PurchaseOrders;
using Invc.Infrastructure.Data;
using Invc.Infrastructure.Inventory;

namespace Invc.Infrastructure.PurchaseOrders;

/// <summary>Dapper implementation of <see cref="IPurchaseOrderRepository"/> — SELECT only.</summary>
public sealed class PurchaseOrderRepository(ISqlConnectionFactory connections) : IPurchaseOrderRepository
{
    public async Task<IReadOnlyList<int>> GetOpenBudgetYearsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var years = await connection.QueryAsync<string>(Cmd(PurchaseOrderSql.OpenBudgetYears, null, cancellationToken)).ConfigureAwait(false);
        return years.Select(y => int.TryParse(y?.Trim(), out var n) ? n : (int?)null).Where(n => n.HasValue).Select(n => n!.Value).Distinct().OrderBy(n => n).ToList();
    }

    public async Task<IReadOnlyList<int>> GetPoFiscalYearsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var prefixes = await connection.QueryAsync<string>(Cmd(PurchaseOrderSql.PoFiscalYearPrefixes, null, cancellationToken)).ConfigureAwait(false);
        return prefixes.Select(p => PurchaseOrderRules.FiscalYearFromPoNumber(p)).Where(y => y.HasValue).Select(y => y!.Value).Distinct().OrderByDescending(y => y).ToList();
    }

    public async Task<IReadOnlyList<PurchaseOrderSummary>> GetHeadersAsync(int fiscalYear, string? keyword, CancellationToken cancellationToken = default)
    {
        var prefix = PurchaseOrderRules.PoPrefix(fiscalYear);
        var normalized = SearchKeyword.Normalize(keyword);
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = normalized is null
            ? Cmd(PurchaseOrderSql.HeadersByFiscalYear, new { PrefixPattern = PrefixPattern(prefix) }, cancellationToken)
            : Cmd(PurchaseOrderSql.HeadersByFiscalYearSearch,
                  new { PrefixPattern = PrefixPattern(prefix), Pattern = InventoryRepository.LikePattern(normalized) }, cancellationToken);

        var rows = await connection.QueryAsync<PurchaseOrderSummary>(command).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<PurchaseOrderDetail?> GetDetailAsync(string realPo, CancellationToken cancellationToken = default)
    {
        if (!PurchaseOrderRules.IsValidPoNumber(realPo))
        {
            return null;
        }

        var realPoParam = new { RealPo = PoParam(realPo) };
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Four statements at most (header, lines, receipts, receipt lines) — never one per line/receipt.
        var header = (await connection.QueryAsync<PurchaseOrderSummary>(Cmd(PurchaseOrderSql.HeaderByRealPo, realPoParam, cancellationToken)).ConfigureAwait(false)).ToList();
        if (header.Count == 0)
        {
            return null;
        }

        // REAL_PO is not unique by constraint; take the first deterministically (ORDER BY in SQL) and keep all in view of tests.
        var h = header[0];
        var lines = await connection.QueryAsync<PurchaseOrderLine>(Cmd(PurchaseOrderSql.Lines(), new { PoNo = PoParam(h.PoNo) }, cancellationToken)).ConfigureAwait(false);
        var receipts = (await connection.QueryAsync<ReceiptRow>(Cmd(PurchaseOrderSql.Receipts(), realPoParam, cancellationToken)).ConfigureAwait(false)).ToList();
        var receiptLines = receipts.Count == 0
            ? []
            : (await connection.QueryAsync<ReceiptLineRow>(Cmd(PurchaseOrderSql.ReceiptLines(), realPoParam, cancellationToken)).ConfigureAwait(false)).ToList();

        return new PurchaseOrderDetail
        {
            Header = h,
            Lines = lines.AsList(),
            Receipts = AssembleReceipts(receipts, receiptLines),
        };
    }

    public async Task<IReadOnlyList<DateTime>> GetBillOutDatesAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var dates = await connection.QueryAsync<DateTime>(Cmd(PurchaseOrderSql.BillOutDates,
            new { PrefixPattern = PrefixPattern(PurchaseOrderRules.PoPrefix(fiscalYear)) }, cancellationToken)).ConfigureAwait(false);
        return dates.AsList();
    }

    internal static IReadOnlyList<PurchaseOrderReceipt> AssembleReceipts(IReadOnlyList<ReceiptRow> receipts, IReadOnlyList<ReceiptLineRow> lines)
    {
        var byReceipt = lines.GroupBy(l => l.ReceiveNo ?? string.Empty).ToDictionary(g => g.Key, g => g.ToList());
        return receipts.Select(r => new PurchaseOrderReceipt
        {
            ReceiveNo = r.ReceiveNo,
            InvoiceNo = r.InvoiceNo,
            InvoiceDate = r.InvoiceDate,
            DateReceive = r.DateReceive,
            TotalCost = r.TotalCost,
            TotalItem = r.TotalItem,
            DateAcc = r.DateAcc,
            Lines = byReceipt.TryGetValue(r.ReceiveNo ?? string.Empty, out var ls)
                ? ls.Select(l => new PurchaseOrderReceiptLine
                {
                    WorkingCode = l.WorkingCode ?? string.Empty,
                    DrugName = l.DrugName,
                    QtyOrder = l.QtyOrder,
                    PackRatio1 = l.PackRatio1,
                    BuyUnitCost = l.BuyUnitCost,
                    QtyFree = l.QtyFree,
                    PackRatio2 = l.PackRatio2,
                    ExpiredDate1 = l.ExpiredDate1,
                    Location1 = l.Location1,
                    LotNo = l.LotNo,
                    ManufacCode = l.ManufacCode,
                }).ToList()
                : [],
        }).ToList();
    }

    /// <summary>"69" → "69%" for the sargable LIKE on PO_NO. Prefix is always two digits, so no escaping is needed.</summary>
    internal static DbString PrefixPattern(string prefix) => new() { Value = prefix + "%", IsAnsi = false, Length = 10 };

    internal static DbString PoParam(string value) => new() { Value = value, IsAnsi = false, Length = PurchaseOrderRules.PoNumberMaxLength };

    private CommandDefinition Cmd(string sql, object? parameters, CancellationToken cancellationToken)
        => new(ReadOnlySql.Ensure(sql), parameters, commandTimeout: connections.CommandTimeoutSeconds, cancellationToken: cancellationToken);

    internal sealed class ReceiptRow
    {
        public string? ReceiveNo { get; init; }
        public string? InvoiceNo { get; init; }
        public DateTime? InvoiceDate { get; init; }
        public DateTime? DateReceive { get; init; }
        public decimal? TotalCost { get; init; }
        public decimal? TotalItem { get; init; }
        public DateTime? DateAcc { get; init; }
    }

    internal sealed class ReceiptLineRow
    {
        public string? ReceiveNo { get; init; }
        public string? WorkingCode { get; init; }
        public string? DrugName { get; init; }
        public decimal? QtyOrder { get; init; }
        public decimal? PackRatio1 { get; init; }
        public decimal? BuyUnitCost { get; init; }
        public decimal? QtyFree { get; init; }
        public decimal? PackRatio2 { get; init; }
        public DateTime? ExpiredDate1 { get; init; }
        public string? Location1 { get; init; }
        public string? LotNo { get; init; }
        public string? ManufacCode { get; init; }
    }
}
