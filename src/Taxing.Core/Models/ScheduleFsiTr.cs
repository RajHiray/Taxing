namespace Taxing.Core.Models;

/// <summary>
/// Schedule FSI — Foreign Source Income. One row per income item accrued during
/// the financial year, converted to INR using the Rule 128 rate date.
/// </summary>
public sealed class ScheduleFsiRow
{
    public string CountryCode { get; init; } = "US";
    public string CountryCodeItr { get; init; } = "2";

    /// <summary>ITR head of income, e.g. "Dividend (OS)" or "Capital Gains".</summary>
    public string IncomeHead { get; init; } = string.Empty;

    /// <summary>Date the income accrued (used for the Rule 128 conversion).</summary>
    public DateOnly IncomeDate { get; init; }

    /// <summary>Income amount in INR (Schedule FSI column: income from outside India).</summary>
    public decimal IncomeInr { get; init; }

    /// <summary>Foreign tax paid/withheld in INR (FSI column: tax paid outside India).</summary>
    public decimal ForeignTaxInr { get; init; }

    /// <summary>Article of the DTAA under which relief is claimed (e.g. "10" for dividends).</summary>
    public string TreatyArticle { get; init; } = string.Empty;

    // Audit trail (foreign currency + rate).
    public decimal IncomeForeign { get; init; }
    public decimal ForeignTaxForeign { get; init; }
    public decimal FxRate { get; init; }
    public DateOnly FxRateDate { get; init; }
}

/// <summary>
/// Schedule TR — Summary of tax relief claimed for taxes paid outside India,
/// aggregated per country. Backs Form 67.
/// </summary>
public sealed class ScheduleTrRow
{
    public string CountryCode { get; init; } = "US";
    public string CountryCodeItr { get; init; } = "2";

    /// <summary>Total foreign tax paid (INR) for the country.</summary>
    public decimal ForeignTaxPaidInr { get; init; }

    /// <summary>
    /// Relief claimed under section 90/90A (INR). Computed as the lower of the
    /// foreign tax paid and the Indian tax attributable to that income
    /// (the latter is supplied by the caller when known).
    /// </summary>
    public decimal ReliefClaimedInr { get; init; }

    /// <summary>Section under which relief is claimed (default 90 for DTAA countries).</summary>
    public string ReliefSection { get; init; } = "90";
}

/// <summary>
/// A single Form 67 detail line (per foreign income item), used to substantiate
/// the foreign tax credit claim.
/// </summary>
public sealed class Form67Row
{
    public string CountryCode { get; init; } = "US";
    public string CountryCodeItr { get; init; } = "2";
    public string SourceOfIncome { get; init; } = string.Empty;
    public decimal IncomeInr { get; init; }
    public decimal ForeignTaxInr { get; init; }
    public string TreatyArticle { get; init; } = string.Empty;
    public decimal FxRate { get; init; }
    public DateOnly FxRateDate { get; init; }
}
