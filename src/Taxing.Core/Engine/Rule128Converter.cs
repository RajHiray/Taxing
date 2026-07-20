using Taxing.Core.Fx;

namespace Taxing.Core.Engine;

/// <summary>
/// Currency-conversion helpers implementing the conventions used across the
/// Indian ITR foreign-asset/income schedules.
/// </summary>
public static class Rule128Converter
{
    /// <summary>
    /// Rule 128 (Foreign Tax Credit) telegraphic transfer buying rate date:
    /// income and the foreign tax thereon are converted using the SBI TTBR on the
    /// last day of the month <b>immediately preceding</b> the month in which the
    /// income accrued / tax was paid.
    /// </summary>
    public static DateOnly Rule128RateDate(DateOnly incomeDate)
    {
        // First day of the income month, minus one day => last day of previous month.
        var firstOfMonth = new DateOnly(incomeDate.Year, incomeDate.Month, 1);
        return firstOfMonth.AddDays(-1);
    }

    /// <summary>
    /// Converts a foreign-currency income amount to INR using the Rule 128 rate date.
    /// </summary>
    public static decimal ConvertIncome(
        IFxRateProvider fx, string currency, decimal foreignAmount, DateOnly incomeDate)
    {
        var rate = fx.GetRate(currency, Rule128RateDate(incomeDate));
        return foreignAmount * rate;
    }

    /// <summary>
    /// Converts a foreign-currency value to INR using the SBI TTBR on the given
    /// valuation date (with the provider applying the preceding-working-day fallback).
    /// Used for Schedule FA peak/closing balances valued on a specific date.
    /// </summary>
    public static decimal ConvertOnDate(
        IFxRateProvider fx, string currency, decimal foreignAmount, DateOnly valuationDate)
    {
        var rate = fx.GetRate(currency, valuationDate);
        return foreignAmount * rate;
    }
}
