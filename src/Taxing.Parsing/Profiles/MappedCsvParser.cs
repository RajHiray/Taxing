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

    /// <summary>Candidate header names for the quantity column.</summary>
    public required string[] QuantityHeaders { get; init; }

    /// <summary>Candidate header names for the per-share price column.</summary>
    public required string[] PriceHeaders { get; init; }

    /// <summary>Candidate header names for the cash amount column.</summary>
    public required string[] AmountHeaders { get; init; }

    /// <summary>Accepted date formats for parsing (invariant culture).</summary>
    public string[] DateFormats { get; init; } =
        { "MM/dd/yyyy", "M/d/yyyy", "yyyy-MM-dd", "dd-MMM-yyyy", "MMM dd, yyyy" };

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
            throw new FormatException(
                $"Could not locate the expected columns for {DisplayName}. " +
                "Please confirm you selected the correct broker and exported the transaction history.");

        var (dateI, actionI, symbolI, qtyI, priceI, amountI) =
            (idx[0], idx[1], idx[2], idx[3], idx[4], idx[5]);

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

            txns.Add(new BrokerTransaction
            {
                Date = date,
                Type = type.Value,
                Symbol = Get(cols, symbolI).Trim().ToUpperInvariant(),
                Quantity = Math.Abs(quantity),
                PricePerShare = Math.Abs(price),
                Amount = Math.Abs(amount),
                Note = action
            });
        }

        return new BrokerStatement
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

        // Required: date, action, amount. Symbol/qty/price may be blank on cash rows.
        if (dateI < 0 || actionI < 0 || amountI < 0) return null;
        return new[] { dateI, actionI, symbolI, qtyI, priceI, amountI };
    }

    private static string Get(string[] cols, int i) =>
        i >= 0 && i < cols.Length ? cols[i] : string.Empty;

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
