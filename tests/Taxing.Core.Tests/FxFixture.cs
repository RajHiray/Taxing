using Taxing.Core.Fx;

namespace Taxing.Core.Tests;

/// <summary>Shared FX fixture with a few hand-picked SBI TTBR-style USD rates.</summary>
internal static class FxFixture
{
    public static InMemoryFxRateProvider UsdRates()
    {
        var fx = new InMemoryFxRateProvider();
        // Rates chosen to be easy to reason about in assertions.
        fx.AddRate("USD", new DateOnly(2022, 12, 31), 82m); // preceding-month rate for Jan income
        fx.AddRate("USD", new DateOnly(2023, 1, 15), 81m);
        fx.AddRate("USD", new DateOnly(2023, 1, 31), 82.5m); // preceding-month rate for Feb income
        fx.AddRate("USD", new DateOnly(2023, 2, 15), 83m);
        fx.AddRate("USD", new DateOnly(2023, 3, 15), 82m);
        fx.AddRate("USD", new DateOnly(2023, 5, 31), 82m);  // preceding-month rate for Jun income
        fx.AddRate("USD", new DateOnly(2023, 6, 15), 82m);
        fx.AddRate("USD", new DateOnly(2023, 12, 29), 83m); // last working day before 31 Dec (holiday)
        return fx;
    }
}
