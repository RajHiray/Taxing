namespace Taxing.Core.Models;

/// <summary>
/// Defines the two reporting windows required for Indian ITR foreign-asset/income
/// reporting for a given Assessment Year (AY).
///
/// * Income (FSI, CG, OS) is reported on the Indian Financial Year basis
///   (1 Apr .. 31 Mar).
/// * Schedule FA is reported on the "accounting period" — the calendar year
///   (1 Jan .. 31 Dec) ending during that Financial Year.
///
/// Example: AY 2024-25 => FY 2023-24 (01-Apr-2023 .. 31-Mar-2024),
/// and the FA calendar year is 01-Jan-2023 .. 31-Dec-2023.
/// </summary>
public sealed class TaxPeriod
{
    /// <summary>The starting calendar year of the financial year (e.g. 2023 for FY 2023-24).</summary>
    public int FinancialYearStartYear { get; }

    public TaxPeriod(int financialYearStartYear)
    {
        FinancialYearStartYear = financialYearStartYear;
    }

    /// <summary>Financial year start: 1 April of the start year.</summary>
    public DateOnly FinancialYearStart => new(FinancialYearStartYear, 4, 1);

    /// <summary>Financial year end: 31 March of the following year.</summary>
    public DateOnly FinancialYearEnd => new(FinancialYearStartYear + 1, 3, 31);

    /// <summary>FA calendar-year start: 1 January of the FY start year.</summary>
    public DateOnly CalendarYearStart => new(FinancialYearStartYear, 1, 1);

    /// <summary>FA calendar-year end: 31 December of the FY start year.</summary>
    public DateOnly CalendarYearEnd => new(FinancialYearStartYear, 12, 31);

    /// <summary>Assessment Year label, e.g. "2024-25" for FY 2023-24.</summary>
    public string AssessmentYearLabel =>
        $"{FinancialYearStartYear + 1}-{(FinancialYearStartYear + 2) % 100:D2}";

    /// <summary>Financial Year label, e.g. "2023-24".</summary>
    public string FinancialYearLabel =>
        $"{FinancialYearStartYear}-{(FinancialYearStartYear + 1) % 100:D2}";

    /// <summary>True if <paramref name="date"/> falls within the financial year.</summary>
    public bool IsInFinancialYear(DateOnly date) =>
        date >= FinancialYearStart && date <= FinancialYearEnd;

    /// <summary>True if <paramref name="date"/> falls within the FA calendar year.</summary>
    public bool IsInCalendarYear(DateOnly date) =>
        date >= CalendarYearStart && date <= CalendarYearEnd;

    /// <summary>Create a <see cref="TaxPeriod"/> from an Assessment Year start (e.g. 2024 for AY 2024-25).</summary>
    public static TaxPeriod FromAssessmentYear(int assessmentYearStartYear) =>
        new(assessmentYearStartYear - 1);
}
