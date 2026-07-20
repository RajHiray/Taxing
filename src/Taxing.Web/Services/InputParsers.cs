using System.Globalization;
using Taxing.Core.Fx;

namespace Taxing.Web.Services;

/// <summary>Parses user-entered / bundled FX rate and closing-price text into typed data.</summary>
public static class InputParsers
{
    /// <summary>
    /// Parses lines of "yyyy-MM-dd,rate" (a leading header line is skipped) into an
    /// <see cref="InMemoryFxRateProvider"/> for the given currency.
    /// </summary>
    public static InMemoryFxRateProvider ParseFxRates(string text, string currency)
    {
        var fx = new InMemoryFxRateProvider();
        if (string.IsNullOrWhiteSpace(text)) return fx;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var parts = line.Split(',');
            if (parts.Length < 2) continue;
            if (!DateOnly.TryParse(parts[0].Trim(), CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
                continue; // skip header / malformed rows
            if (!decimal.TryParse(parts[1].Trim(), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var rate) || rate <= 0)
                continue;
            fx.AddRate(currency, date, rate);
        }
        return fx;
    }

    /// <summary>Parses lines of "SYMBOL,price" into a closing-price dictionary.</summary>
    public static Dictionary<string, decimal> ParseClosingPrices(string text)
    {
        var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return map;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var parts = line.Split(',', '=');
            if (parts.Length < 2) continue;
            var symbol = parts[0].Trim().ToUpperInvariant();
            if (symbol.Length == 0) continue;
            if (decimal.TryParse(parts[1].Trim(), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var price) && price > 0)
                map[symbol] = price;
        }
        return map;
    }
}
