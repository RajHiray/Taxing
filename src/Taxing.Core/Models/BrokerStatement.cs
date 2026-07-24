namespace Taxing.Core.Models;

/// <summary>
/// A normalized broker statement: the canonical input to the calculation engine.
/// </summary>
public sealed class BrokerStatement
{
    /// <summary>Source broker.</summary>
    public Broker Broker { get; init; }

    /// <summary>Foreign institution name (for Schedule FA disclosure).</summary>
    public string InstitutionName { get; init; } = string.Empty;

    /// <summary>Institution address (for Schedule FA disclosure).</summary>
    public string InstitutionAddress { get; init; } = string.Empty;

    /// <summary>Two-letter ISO country code where the account is held (e.g. "US").</summary>
    public string CountryCode { get; init; } = "US";

    /// <summary>ITR country code (India uses numeric codes; US = 2).</summary>
    public string CountryCodeItr { get; init; } = "2";

    /// <summary>Masked or full account number (masking is applied by parsers).</summary>
    public string AccountNumber { get; init; } = string.Empty;

    /// <summary>Date the custodial account was opened, if known.</summary>
    public DateOnly? AccountOpenedDate { get; init; }

    /// <summary>Foreign currency of the account (assumed USD).</summary>
    public string Currency { get; init; } = "USD";

    /// <summary>
    /// Closing cash balance in the account in foreign currency at the reporting
    /// period end (Dec 31 of the calendar year). Optional; defaults to 0.
    /// </summary>
    public decimal ClosingCashBalance { get; init; }

    /// <summary>All normalized transactions.</summary>
    public IReadOnlyList<BrokerTransaction> Transactions { get; init; } = new List<BrokerTransaction>();
}
