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

    /// <summary>Number of decimal places to round INR outputs to. Default 0 (whole rupees).</summary>
    public int InrRoundingDecimals { get; init; } = 0;
}
