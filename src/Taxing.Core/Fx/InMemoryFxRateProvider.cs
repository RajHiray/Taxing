using System.Collections.Concurrent;

namespace Taxing.Core.Fx;

/// <summary>
/// In-memory <see cref="IFxRateProvider"/> backed by a table of dated rates per currency.
/// Applies the "last preceding working day" rule automatically: when the exact date is
/// missing, the most recent earlier rate is used.
/// </summary>
public sealed class InMemoryFxRateProvider : IFxRateProvider
{
    // currency -> sorted (by date) list of (date, rate)
    private readonly ConcurrentDictionary<string, SortedList<DateOnly, decimal>> _rates =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Adds or overwrites a rate for a currency on a specific date.</summary>
    public void AddRate(string currency, DateOnly date, decimal inrPerUnit)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required.", nameof(currency));
        if (inrPerUnit <= 0)
            throw new ArgumentOutOfRangeException(nameof(inrPerUnit), "Rate must be positive.");

        var list = _rates.GetOrAdd(currency, _ => new SortedList<DateOnly, decimal>());
        lock (list)
        {
            list[date] = inrPerUnit;
        }
    }

    /// <inheritdoc />
    public decimal GetRate(string currency, DateOnly date)
    {
        if (!_rates.TryGetValue(currency, out var list))
            throw new InvalidOperationException($"No FX rates loaded for currency '{currency}'.");

        lock (list)
        {
            if (list.TryGetValue(date, out var exact))
                return exact;

            // Find most recent rate strictly at or before the requested date.
            decimal? best = null;
            foreach (var kvp in list)
            {
                if (kvp.Key <= date)
                    best = kvp.Value;
                else
                    break; // SortedList enumerates in ascending key order.
            }

            if (best is null)
                throw new InvalidOperationException(
                    $"No FX rate for '{currency}' on or before {date:yyyy-MM-dd}.");

            return best.Value;
        }
    }

    /// <summary>Returns true if any rate is loaded for the given currency.</summary>
    public bool HasCurrency(string currency) => _rates.ContainsKey(currency);
}
