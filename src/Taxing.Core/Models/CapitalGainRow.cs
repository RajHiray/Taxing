namespace Taxing.Core.Models;

/// <summary>Long-term vs short-term classification for capital gains.</summary>
public enum GainTerm
{
    ShortTerm,
    LongTerm
}

/// <summary>
/// Schedule CG — one matched sale (FIFO against acquired lots) of a foreign share.
/// Cost equals the fair-market value on the vesting date (already taxed as a
/// salary perquisite), avoiding double taxation.
/// </summary>
public sealed class CapitalGainRow
{
    public string Symbol { get; init; } = string.Empty;
    public DateOnly AcquisitionDate { get; init; }
    public DateOnly SaleDate { get; init; }
    public decimal Quantity { get; init; }

    /// <summary>Sale proceeds in INR (converted at the sale-date rate).</summary>
    public decimal ProceedsInr { get; init; }

    /// <summary>Cost of acquisition in INR (vesting FMV at the acquisition-date rate).</summary>
    public decimal CostInr { get; init; }

    /// <summary>Gain (or loss) in INR.</summary>
    public decimal GainInr => ProceedsInr - CostInr;

    /// <summary>
    /// Long-term if held more than 24 months (foreign/unlisted shares), else short-term.
    /// </summary>
    public GainTerm Term { get; init; }

    // Audit trail.
    public decimal ProceedsForeign { get; init; }
    public decimal CostForeign { get; init; }
    public int HoldingMonths { get; init; }
}
