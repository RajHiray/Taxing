namespace Taxing.Core.Models;

/// <summary>Supported foreign brokers for statement ingestion.</summary>
public enum Broker
{
    Fidelity,
    MorganStanley,
    ETrade
}

/// <summary>The type of a broker transaction relevant to Indian tax computation.</summary>
public enum TransactionType
{
    /// <summary>RSU/ESPP shares vesting into the account (acquisition).</summary>
    Vest,

    /// <summary>Dividend credited on held shares.</summary>
    Dividend,

    /// <summary>Foreign tax withheld at source (e.g. US dividend withholding).</summary>
    TaxWithheld,

    /// <summary>Sale / redemption of shares.</summary>
    Sale,

    /// <summary>Fees, commissions or other adjustments.</summary>
    Fee
}
