namespace Taxing.Core.Models;

/// <summary>
/// Schedule FA — Section A2: Foreign Custodial Account.
/// Represents the brokerage account itself. All INR values use SBI TTBR
/// on the relevant valuation date; peak/closing use the calendar-year window.
/// </summary>
public sealed class ScheduleFaA2
{
    public string CountryCode { get; init; } = "US";
    public string CountryCodeItr { get; init; } = "2";
    public string InstitutionName { get; init; } = string.Empty;
    public string InstitutionAddress { get; init; } = string.Empty;
    public string AccountNumber { get; init; } = string.Empty;
    public string Status { get; init; } = "Owner";
    public DateOnly? AccountOpenedDate { get; init; }

    /// <summary>Peak balance of the account during the calendar year, in INR.</summary>
    public decimal PeakBalanceInr { get; init; }

    /// <summary>Closing balance of the account at 31 Dec, in INR.</summary>
    public decimal ClosingBalanceInr { get; init; }

    /// <summary>
    /// Gross amount paid/credited to the account during the period
    /// (dividends + sale proceeds), in INR.
    /// </summary>
    public decimal GrossCreditedInr { get; init; }

    // Foreign-currency figures retained for the audit trail.
    public decimal PeakBalanceForeign { get; init; }
    public decimal ClosingBalanceForeign { get; init; }
    public decimal GrossCreditedForeign { get; init; }
    public string Currency { get; init; } = "USD";
}

/// <summary>
/// Schedule FA — Section A3: Foreign Equity &amp; Debt Interest.
/// One row per security held during the calendar year.
/// </summary>
public sealed class ScheduleFaA3
{
    public string CountryCode { get; init; } = "US";
    public string CountryCodeItr { get; init; } = "2";
    public string EntityName { get; init; } = string.Empty;
    public string EntityAddress { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Date the interest (first lot) was acquired.</summary>
    public DateOnly? AcquisitionDate { get; init; }

    /// <summary>Initial value of the investment (acquisition cost), in INR.</summary>
    public decimal InitialValueInr { get; init; }

    /// <summary>Peak value of the holding during the calendar year, in INR.</summary>
    public decimal PeakValueInr { get; init; }

    /// <summary>Closing value of the holding at 31 Dec, in INR.</summary>
    public decimal ClosingValueInr { get; init; }

    /// <summary>Total gross amount paid/credited (dividends) during the period, in INR.</summary>
    public decimal GrossDividendInr { get; init; }

    /// <summary>Total proceeds from sale/redemption during the period, in INR.</summary>
    public decimal ProceedsInr { get; init; }

    /// <summary>Shares held at 31 Dec (audit trail).</summary>
    public decimal ClosingShares { get; init; }
}
