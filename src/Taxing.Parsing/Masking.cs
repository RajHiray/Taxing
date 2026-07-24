namespace Taxing.Parsing;

/// <summary>Helpers for safely handling sensitive identifiers.</summary>
public static class Masking
{
    /// <summary>
    /// Masks all but the last four characters of an account number, e.g.
    /// "Z12345678" -&gt; "*****5678". Returns the input unchanged if 4 chars or fewer.
    /// </summary>
    public static string MaskAccount(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber)) return string.Empty;
        var trimmed = accountNumber.Trim();
        if (trimmed.Length <= 4) return trimmed;
        var last4 = trimmed[^4..];
        return new string('*', trimmed.Length - 4) + last4;
    }
}
