namespace Taxing.Core.Models;

/// <summary>
/// The complete set of computed ITR schedule values produced by the engine for a
/// single broker statement and assessment year.
/// </summary>
public sealed class ItrResult
{
    public required TaxPeriod Period { get; init; }
    public Broker Broker { get; init; }

    /// <summary>Schedule FA Section A2 (the custodial account). Usually one row.</summary>
    public IReadOnlyList<ScheduleFaA2> ScheduleFaA2 { get; init; } = new List<ScheduleFaA2>();

    /// <summary>Schedule FA Section A3 (one row per held security).</summary>
    public IReadOnlyList<ScheduleFaA3> ScheduleFaA3 { get; init; } = new List<ScheduleFaA3>();

    /// <summary>Schedule FSI rows (foreign source income for the financial year).</summary>
    public IReadOnlyList<ScheduleFsiRow> ScheduleFsi { get; init; } = new List<ScheduleFsiRow>();

    /// <summary>Schedule TR rows (tax relief aggregated per country).</summary>
    public IReadOnlyList<ScheduleTrRow> ScheduleTr { get; init; } = new List<ScheduleTrRow>();

    /// <summary>Form 67 detail lines.</summary>
    public IReadOnlyList<Form67Row> Form67 { get; init; } = new List<Form67Row>();

    /// <summary>Schedule CG rows (matched sales during the financial year).</summary>
    public IReadOnlyList<CapitalGainRow> CapitalGains { get; init; } = new List<CapitalGainRow>();

    /// <summary>Non-fatal warnings raised during computation (reconciliation, missing data).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = new List<string>();
}
