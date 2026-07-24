namespace Taxing.Core.Models;

/// <summary>
/// A single normalized transaction parsed from a broker statement.
/// All monetary amounts are in the account's foreign currency (assumed USD).
/// </summary>
public sealed class BrokerTransaction
{
    /// <summary>Date the transaction occurred (settlement/credit date).</summary>
    public DateOnly Date { get; init; }

    /// <summary>Kind of transaction.</summary>
    public TransactionType Type { get; init; }

    /// <summary>Security symbol, e.g. "MSFT".</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Number of shares involved (0 for pure cash events such as fees).</summary>
    public decimal Quantity { get; init; }

    /// <summary>
    /// Per-share price in foreign currency at the time of the event.
    /// For a Vest this is the fair market value used as the acquisition cost.
    /// </summary>
    public decimal PricePerShare { get; init; }

    /// <summary>
    /// Gross cash amount of the event in foreign currency.
    /// Positive for credits (dividends, sale proceeds), positive magnitude for tax/fees.
    /// </summary>
    public decimal Amount { get; init; }

    /// <summary>Free-form note from the source statement (optional).</summary>
    public string? Note { get; init; }
}
