using Taxing.Core.Models;

namespace Taxing.Parsing;

/// <summary>
/// Parses a broker's CSV export into a normalized <see cref="BrokerStatement"/>.
/// </summary>
public interface IStatementParser
{
    /// <summary>The broker this parser understands.</summary>
    Broker Broker { get; }

    /// <summary>Human-friendly broker name for UI selection.</summary>
    string DisplayName { get; }

    /// <summary>
    /// Parses the CSV content into a normalized statement.
    /// </summary>
    /// <param name="csv">Raw CSV text of the broker export.</param>
    /// <param name="meta">Account/institution metadata supplied by the user.</param>
    BrokerStatement Parse(string csv, StatementMetadata meta);
}

/// <summary>
/// User-supplied metadata that broker CSV exports typically do not contain but which
/// Schedule FA requires (institution details, account open date, closing cash).
/// </summary>
public sealed class StatementMetadata
{
    public string InstitutionName { get; init; } = string.Empty;
    public string InstitutionAddress { get; init; } = string.Empty;
    public string AccountNumber { get; init; } = string.Empty;
    public DateOnly? AccountOpenedDate { get; init; }
    public decimal ClosingCashBalance { get; init; }
    public string Currency { get; init; } = "USD";
    public string CountryCode { get; init; } = "US";
    public string CountryCodeItr { get; init; } = "2";
}
