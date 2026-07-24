using Taxing.Core.Models;

namespace Taxing.Parsing.Profiles;

/// <summary>Column/action mapping for Fidelity account transaction CSV exports.</summary>
public sealed class FidelityParser : MappedCsvParser
{
    public override Broker Broker => Broker.Fidelity;
    public override string DisplayName => "Fidelity";

    protected override ColumnMap Map { get; } = new()
    {
        DateHeaders = new[] { "Run Date", "Date", "Transaction Date" },
        ActionHeaders = new[] { "Action", "Transaction Type", "Description" },
        SymbolHeaders = new[] { "Symbol", "Ticker", "Symbol/CUSIP", "Symbol / CUSIP" },
        QuantityHeaders = new[] { "Quantity", "Shares" },
        PriceHeaders = new[] { "Price", "Price ($)", "Price Per Share" },
        AmountHeaders = new[] { "Amount", "Amount ($)", "Net Amount" },
        ActionResolver = ResolveAction
    };

    internal static TransactionType? ResolveAction(string action)
    {
        var a = action?.Trim().ToUpperInvariant() ?? string.Empty;
        if (a.Length == 0) return null;

        if (a.Contains("WITHHOLD") || a.Contains("TAX") || a.Contains("NRA"))
            return TransactionType.TaxWithheld;
        // Fidelity "REINVESTMENT REINVEST @ $1.000" rows are commonly broker cash-sweep
        // fund movements (for example MMKT fund), not equity vest/acquisition events for A3.
        if (a.Contains("REINVEST"))
            return null;
        if (a.Contains("DIVIDEND") || a.Contains("DIV"))
            return TransactionType.Dividend;
        if (a.Contains("YOU BOUGHT") || a.Contains("VEST") || a.Contains("DEPOSIT") ||
            a.Contains("RECEIVED") || a.Contains("RSU") || a.Contains("ESPP"))
            return TransactionType.Vest;
        if (a.Contains("YOU SOLD") || a.Contains("SELL") || a.Contains("SALE") ||
            a.Contains("REDEMPTION"))
            return TransactionType.Sale;
        if (a.Contains("FEE") || a.Contains("COMMISSION"))
            return TransactionType.Fee;
        return null;
    }
}

/// <summary>Column/action mapping for Morgan Stanley StockPlan Connect CSV exports.</summary>
public sealed class MorganStanleyParser : MappedCsvParser
{
    public override Broker Broker => Broker.MorganStanley;
    public override string DisplayName => "Morgan Stanley";

    protected override ColumnMap Map { get; } = new()
    {
        DateHeaders = new[] { "Date", "Transaction Date", "Activity Date" },
        ActionHeaders = new[] { "Activity", "Transaction Type", "Type", "Description" },
        SymbolHeaders = new[] { "Symbol", "Security", "Ticker", "Security Symbol" },
        QuantityHeaders = new[] { "Quantity", "Shares", "Number of Shares" },
        PriceHeaders = new[] { "Price", "Share Price", "Market Value Per Share" },
        AmountHeaders = new[] { "Amount", "Net Amount", "Total Value" },
        ActionResolver = ResolveAction
    };

    internal static TransactionType? ResolveAction(string action)
    {
        var a = action?.Trim().ToUpperInvariant() ?? string.Empty;
        if (a.Length == 0) return null;

        if (a.Contains("WITHHOLD") || a.Contains("TAX"))
            return TransactionType.TaxWithheld;
        if (a.Contains("DIVIDEND") || a.Contains("DIV"))
            return TransactionType.Dividend;
        if (a.Contains("RELEASE") || a.Contains("VEST") || a.Contains("DEPOSIT") ||
            a.Contains("LAPSE") || a.Contains("PURCHASE"))
            return TransactionType.Vest;
        if (a.Contains("SALE") || a.Contains("SELL") || a.Contains("SOLD"))
            return TransactionType.Sale;
        if (a.Contains("FEE") || a.Contains("COMMISSION"))
            return TransactionType.Fee;
        return null;
    }
}

/// <summary>Column/action mapping for E*TRADE / Morgan Stanley at Work CSV exports.</summary>
public sealed class ETradeParser : MappedCsvParser
{
    public override Broker Broker => Broker.ETrade;
    public override string DisplayName => "E*TRADE";

    protected override ColumnMap Map { get; } = new()
    {
        DateHeaders = new[] { "TransactionDate", "Transaction Date", "Date" },
        ActionHeaders = new[] { "TransactionType", "Transaction Type", "Type", "Description" },
        SymbolHeaders = new[] { "Symbol", "SecurityType", "Ticker" },
        QuantityHeaders = new[] { "Quantity", "Shares" },
        PriceHeaders = new[] { "Price", "ExecutionPrice", "Price ($)" },
        AmountHeaders = new[] { "Amount", "NetAmount", "Amount ($)" },
        ActionResolver = ResolveAction
    };

    internal static TransactionType? ResolveAction(string action)
    {
        var a = action?.Trim().ToUpperInvariant() ?? string.Empty;
        if (a.Length == 0) return null;

        if (a.Contains("WITHHOLD") || a.Contains("TAX"))
            return TransactionType.TaxWithheld;
        if (a.Contains("DIVIDEND") || a.Contains("DIV"))
            return TransactionType.Dividend;
        if (a.Contains("VEST") || a.Contains("RELEASE") || a.Contains("DEPOSIT") ||
            a.Contains("BOUGHT") || a.Contains("PURCHASE"))
            return TransactionType.Vest;
        if (a.Contains("SOLD") || a.Contains("SALE") || a.Contains("SELL"))
            return TransactionType.Sale;
        if (a.Contains("FEE") || a.Contains("COMMISSION"))
            return TransactionType.Fee;
        return null;
    }
}
