using System.Globalization;
using Taxing.Core.Models;

namespace Taxing.Parsing.Profiles;

/// <summary>
/// Declarative mapping from a broker CSV's columns/action words to the canonical model.
/// Header matching is case-insensitive and tolerant of surrounding whitespace.
/// </summary>
public sealed class ColumnMap
{
    /// <summary>Candidate header names for the transaction date column (first match wins).</summary>
    public required string[] DateHeaders { get; init; }

    /// <summary>Candidate header names for the action/type column.</summary>
    public required string[] ActionHeaders { get; init; }

    /// <summary>Candidate header names for the security symbol column.</summary>
    public required string[] SymbolHeaders { get; init; }

    /// <summary>
    /// Candidate header names for a security description/name column. Used as a fallback
    /// source for the security identity when the symbol column is blank or absent.
    /// </summary>
    public string[] DescriptionHeaders { get; init; } =
        { "Description", "Security Description", "Security", "Security Name", "Investment", "Investment Name", "Fund Name" };

    /// <summary>Candidate header names for the quantity column.</summary>
    public required string[] QuantityHeaders { get; init; }

    /// <summary>Candidate header names for the per-share price column.</summary>
    public required string[] PriceHeaders { get; init; }

    /// <summary>Candidate header names for the cash amount column.</summary>
    public required string[] AmountHeaders { get; init; }

    /// <summary>Accepted date formats for parsing (invariant culture).</summary>
    public string[] DateFormats { get; init; } =
        { "MM/dd/yyyy", "M/d/yyyy", "yyyy-MM-dd", "dd-MMM-yyyy", "MMM dd, yyyy", "MMM-dd-yyyy" };

    /// <summary>
    /// Candidate header names for a lot acquisition-date column, used when the upload is a
    /// cost-basis / tax-lot export (one row per held lot) rather than a transaction history.
    /// </summary>
    public string[] LotAcquiredHeaders { get; init; } =
        { "Date Acquired", "Acquired", "Acquisition Date", "Date of Acquisition", "Acquired Date",
          "Open Date", "Purchase Date", "Acquired Date/Lot" };

    /// <summary>Candidate header names for a lot's per-share cost basis.</summary>
    public string[] LotCostPerShareHeaders { get; init; } =
        { "Cost Basis Per Share", "Cost Per Share", "Cost/Share", "Cost basis/share", "Unit Cost",
          "Average Cost Basis", "Acquisition Price", "Price Per Share", "Acquisition Cost Per Share" };

    /// <summary>Candidate header names for a lot's total cost basis.</summary>
    public string[] LotTotalCostHeaders { get; init; } =
        { "Cost Basis", "Total Cost Basis", "Adjusted Cost Basis", "Cost Basis Total", "Total Cost",
          "Acquisition Cost", "Cost" };

    /// <summary>
    /// Maps a broker action string to a canonical <see cref="TransactionType"/>.
    /// Return null to skip the row (unrecognized/irrelevant action).
    /// </summary>
    public required Func<string, TransactionType?> ActionResolver { get; init; }
}

/// <summary>
/// Generic CSV parser driven by a <see cref="ColumnMap"/>. Concrete broker profiles
/// only need to supply their column map and metadata.
/// </summary>
public abstract class MappedCsvParser : IStatementParser
{
    public abstract Broker Broker { get; }
    public abstract string DisplayName { get; }
    protected abstract ColumnMap Map { get; }

    public BrokerStatement Parse(string csv, StatementMetadata meta)
    {
        ArgumentNullException.ThrowIfNull(meta);
        var rows = CsvReader.Parse(csv ?? string.Empty);
        if (rows.Count == 0)
            throw new FormatException("The statement is empty or could not be read.");

        // Find the header row: the first row that resolves all required columns.
        int headerIndex = -1;
        int[]? idx = null;
        for (int r = 0; r < rows.Count; r++)
        {
            var candidate = ResolveColumns(rows[r]);
            if (candidate is not null)
            {
                headerIndex = r;
                idx = candidate;
                break;
            }
        }

        if (idx is null)
        {
            // Not a transaction-history export. Try a cost-basis / tax-lot export (one row per
            // held lot), which lets users who can only download "lots" (not full transaction
            // history) still generate per-security Schedule FA A3 rows with real cost basis.
            var lotTxns = TryParseLots(rows);
            if (lotTxns is not null)
                return BuildStatement(meta, lotTxns);

            throw new FormatException(
                $"Could not locate the expected columns for {DisplayName}. " +
                "Please confirm you selected the correct broker and exported the transaction " +
                "history or a cost-basis (tax lots) CSV.");
        }
        var (dateI, actionI, symbolI, qtyI, priceI, amountI, descI) =
            (idx[0], idx[1], idx[2], idx[3], idx[4], idx[5], idx[6]);

        var txns = new List<BrokerTransaction>();
        for (int r = headerIndex + 1; r < rows.Count; r++)
        {
            var cols = rows[r];
            if (cols.Length == 0 || cols.All(string.IsNullOrWhiteSpace)) continue;

            var action = Get(cols, actionI);
            var type = Map.ActionResolver(action);
            if (type is null) continue;

            if (!TryParseDate(Get(cols, dateI), out var date)) continue;

            var quantity = ParseDecimal(Get(cols, qtyI));
            var price = ParseDecimal(Get(cols, priceI));
            var amount = ParseDecimal(Get(cols, amountI));

            // Prefer the dedicated symbol column; when it is blank or absent, fall back to
            // the description/security column so per-security schedules (e.g. FA A3) can
            // still be built. Some broker exports name the security only in Description.
            // Fees are pure cash events and are left symbol-less to avoid bogus holdings.
            var symbol = Get(cols, symbolI).Trim().ToUpperInvariant();
            var description = Get(cols, descI).Trim();
            if (symbol.Length == 0 && type.Value != TransactionType.Fee)
                symbol = NormalizeDescriptionSymbol(description);

            txns.Add(new BrokerTransaction
            {
                Date = date,
                Type = type.Value,
                Symbol = symbol,
                Quantity = Math.Abs(quantity),
                PricePerShare = Math.Abs(price),
                Amount = Math.Abs(amount),
                Note = description.Length > 0 ? description : action
            });
        }

        return BuildStatement(meta, txns);
    }

    private BrokerStatement BuildStatement(StatementMetadata meta, List<BrokerTransaction> txns) =>
        new()
        {
            Broker = Broker,
            InstitutionName = string.IsNullOrWhiteSpace(meta.InstitutionName)
                ? DisplayName : meta.InstitutionName,
            InstitutionAddress = meta.InstitutionAddress,
            CountryCode = meta.CountryCode,
            CountryCodeItr = meta.CountryCodeItr,
            AccountNumber = Masking.MaskAccount(meta.AccountNumber),
            AccountOpenedDate = meta.AccountOpenedDate,
            Currency = meta.Currency,
            ClosingCashBalance = meta.ClosingCashBalance,
            Transactions = txns
        };

    /// <summary>
    /// Attempts to interpret the rows as a cost-basis / tax-lot export where each row is a held
    /// lot (symbol, acquisition date, quantity, cost basis). Returns one <see cref="TransactionType.Vest"/>
    /// transaction per lot, with the per-share cost basis as the acquisition price so Schedule FA
    /// A3 initial values are non-zero. Returns null when the rows are not a recognizable lots export.
    /// </summary>
    private List<BrokerTransaction>? TryParseLots(IReadOnlyList<string[]> rows)
    {
        int headerIndex = -1;
        int acquiredI = -1, qtyI = -1, symbolI = -1, descI = -1, costPerShareI = -1, totalCostI = -1;

        for (int r = 0; r < rows.Count; r++)
        {
            int Find(string[] names)
            {
                for (int i = 0; i < rows[r].Length; i++)
                {
                    var h = rows[r][i].Trim();
                    foreach (var n in names)
                        if (h.Equals(n, StringComparison.OrdinalIgnoreCase))
                            return i;
                }
                return -1;
            }

            var a = Find(Map.LotAcquiredHeaders);
            var q = Find(Map.QuantityHeaders);
            var cps = Find(Map.LotCostPerShareHeaders);
            var tc = Find(Map.LotTotalCostHeaders);

            // A lots export must identify an acquisition date, a quantity, and some cost basis.
            if (a >= 0 && q >= 0 && (cps >= 0 || tc >= 0))
            {
                headerIndex = r;
                acquiredI = a; qtyI = q; costPerShareI = cps; totalCostI = tc;
                symbolI = Find(Map.SymbolHeaders);
                descI = Find(Map.DescriptionHeaders);
                break;
            }
        }

        if (headerIndex < 0) return null;

        var txns = new List<BrokerTransaction>();
        for (int r = headerIndex + 1; r < rows.Count; r++)
        {
            var cols = rows[r];
            if (cols.Length == 0 || cols.All(string.IsNullOrWhiteSpace)) continue;

            if (!TryParseDate(Get(cols, acquiredI), out var date)) continue;

            var quantity = Math.Abs(ParseDecimal(Get(cols, qtyI)));
            if (quantity <= 0) continue;

            var perShare = Math.Abs(ParseDecimal(Get(cols, costPerShareI)));
            var totalCost = Math.Abs(ParseDecimal(Get(cols, totalCostI)));
            // Prefer explicit per-share cost; otherwise derive it from total cost basis / quantity.
            if (perShare <= 0 && totalCost > 0)
                perShare = totalCost / quantity;
            if (totalCost <= 0 && perShare > 0)
                totalCost = perShare * quantity;

            var symbol = Get(cols, symbolI).Trim().ToUpperInvariant();
            var description = Get(cols, descI).Trim();
            if (symbol.Length == 0)
                symbol = NormalizeDescriptionSymbol(description);
            if (symbol.Length == 0) continue; // cannot build a holding without an identity

            txns.Add(new BrokerTransaction
            {
                Date = date,
                Type = TransactionType.Vest,
                Symbol = symbol,
                Quantity = quantity,
                PricePerShare = perShare,
                Amount = totalCost,
                Note = description.Length > 0 ? description : "Tax lot"
            });
        }

        return txns.Count > 0 ? txns : null;
    }

    private int[]? ResolveColumns(string[] header)
    {
        int Find(string[] names)
        {
            for (int i = 0; i < header.Length; i++)
            {
                var h = header[i].Trim();
                foreach (var n in names)
                    if (h.Equals(n, StringComparison.OrdinalIgnoreCase))
                        return i;
            }
            return -1;
        }

        var dateI = Find(Map.DateHeaders);
        var actionI = Find(Map.ActionHeaders);
        var symbolI = Find(Map.SymbolHeaders);
        var qtyI = Find(Map.QuantityHeaders);
        var priceI = Find(Map.PriceHeaders);
        var amountI = Find(Map.AmountHeaders);
        var descI = Find(Map.DescriptionHeaders);

        // Required: date, action, amount. Symbol/qty/price/description may be blank on cash rows.
        if (dateI < 0 || actionI < 0 || amountI < 0) return null;
        return new[] { dateI, actionI, symbolI, qtyI, priceI, amountI, descI };
    }

    private static string Get(string[] cols, int i) =>
        i >= 0 && i < cols.Length ? cols[i] : string.Empty;

    /// <summary>
    /// Turns a security description into a stable identity usable as a symbol when no ticker
    /// column value is available (e.g. "Microsoft Corp" -&gt; "MICROSOFT CORP"). Returns an
    /// empty string for values that are clearly not securities (blank).
    /// </summary>
    private static string NormalizeDescriptionSymbol(string description)
    {
        var d = description.Trim();
        if (d.Length == 0) return string.Empty;
        // Collapse internal whitespace and upper-case for consistent grouping.
        return string.Join(' ',
            d.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    }

    private bool TryParseDate(string value, out DateOnly date)
    {
        value = value.Trim();
        foreach (var fmt in Map.DateFormats)
        {
            if (DateOnly.TryParseExact(value, fmt, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out date))
                return true;
        }
        return DateOnly.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
    }

    private static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0m;
        value = value.Trim()
            .Replace("$", string.Empty)
            .Replace(",", string.Empty)
            .Replace("(", "-").Replace(")", string.Empty);
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            ? d : 0m;
    }
}
