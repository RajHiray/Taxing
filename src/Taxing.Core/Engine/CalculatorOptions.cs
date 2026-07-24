namespace Taxing.Core.Engine;

/// <summary>
/// Tunable options for the <see cref="ItrCalculator"/>.
/// </summary>
public sealed class CalculatorOptions
{
    /// <summary>
    /// Holding period (in months) beyond which a foreign/unlisted share sale is
    /// treated as long-term. Default is 24 months.
    /// </summary>
    public int LongTermHoldingMonths { get; init; } = 24;

    /// <summary>
    /// Optional per-symbol closing (31 Dec) market price in foreign currency,
    /// used to value Schedule FA closing holdings. When a symbol is absent, the
    /// last transaction price seen during the calendar year is used instead.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> ClosingPricesForeign { get; init; }
        = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Optional per-share acquisition (vest-day) market price in foreign currency, used as the
    /// Schedule FA A3 <em>Initial value</em> and the capital-gains cost basis when a vest/acquisition
    /// row carries no price or cash amount (common for RSU/ESPP share-deposit rows). Keys may be
    /// either a bare <c>SYMBOL</c> (applies to every lot of that security) or a date-specific
    /// <c>SYMBOL@yyyy-MM-dd</c> (applies to lots vested on that date, taking precedence over the
    /// bare-symbol entry). Lookups are case-insensitive.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> AcquisitionPricesForeign { get; init; }
        = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Number of decimal places to round INR outputs to. Default 0 (whole rupees).</summary>
    public int InrRoundingDecimals { get; init; } = 0;
}
