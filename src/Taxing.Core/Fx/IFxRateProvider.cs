namespace Taxing.Core.Fx;

/// <summary>
/// Provides SBI Telegraphic Transfer Buying Rate (TTBR) INR-per-unit-of-foreign-currency
/// for a given currency and date. Implementations must apply the "previous working day"
/// fallback when a rate for the exact date is unavailable (weekend/holiday).
/// </summary>
public interface IFxRateProvider
{
    /// <summary>
    /// Returns the SBI TTBR (INR per 1 unit of <paramref name="currency"/>) applicable
    /// for <paramref name="date"/>. If no rate exists for that exact date, the most
    /// recent preceding available rate is returned.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no rate at or before the date exists.</exception>
    decimal GetRate(string currency, DateOnly date);
}
