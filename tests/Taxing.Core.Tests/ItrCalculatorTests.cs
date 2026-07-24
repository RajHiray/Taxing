using Taxing.Core.Engine;
using Taxing.Core.Fx;
using Taxing.Core.Models;

namespace Taxing.Core.Tests;

public class ItrCalculatorTests
{
    private static BrokerStatement DividendScenario()
    {
        return new BrokerStatement
        {
            Broker = Broker.Fidelity,
            InstitutionName = "Fidelity",
            CountryCode = "US",
            CountryCodeItr = "2",
            AccountNumber = "****5678",
            Currency = "USD",
            ClosingCashBalance = 0m,
            Transactions = new List<BrokerTransaction>
            {
                new() { Date = new(2023, 1, 15), Type = TransactionType.Vest,
                        Symbol = "MSFT", Quantity = 10m, PricePerShare = 250m, Amount = 2500m },
                new() { Date = new(2023, 6, 15), Type = TransactionType.Dividend,
                        Symbol = "MSFT", Amount = 6.80m },
                new() { Date = new(2023, 6, 15), Type = TransactionType.TaxWithheld,
                        Symbol = "MSFT", Amount = 1.70m },
            }
        };
    }

    private static ItrResult ComputeDividendScenario()
    {
        var fx = FxFixture.UsdRates();
        var options = new CalculatorOptions
        {
            ClosingPricesForeign = new Dictionary<string, decimal> { ["MSFT"] = 375m }
        };
        var calc = new ItrCalculator(fx, options);
        return calc.Compute(DividendScenario(), new TaxPeriod(2023));
    }

    [Fact]
    public void A3_ComputesInitialPeakClosingAndDividend()
    {
        var result = ComputeDividendScenario();
        var row = Assert.Single(result.ScheduleFaA3);

        Assert.Equal("MSFT", row.Symbol);
        Assert.Equal(new DateOnly(2023, 1, 15), row.AcquisitionDate);
        Assert.Equal(202500m, row.InitialValueInr);   // 10 * 250 * 81
        Assert.Equal(311250m, row.ClosingValueInr);    // 10 * 375 * 83 (29 Dec fallback)
        Assert.Equal(311250m, row.PeakValueInr);
        Assert.Equal(558m, row.GrossDividendInr);      // 6.80 * 82 = 557.6 -> 558
        Assert.Equal(0m, row.ProceedsInr);
        Assert.Equal(10m, row.ClosingShares);
    }

    [Fact]
    public void A3_CreatesDateWiseRows_PerAcquisitionDate()
    {
        var fx = FxFixture.UsdRates();
        var options = new CalculatorOptions
        {
            ClosingPricesForeign = new Dictionary<string, decimal> { ["MMKT"] = 1m }
        };
        var statement = new BrokerStatement
        {
            Broker = Broker.Fidelity,
            CountryCode = "US",
            CountryCodeItr = "2",
            Currency = "USD",
            Transactions = new List<BrokerTransaction>
            {
                new() { Date = new(2026, 1, 30), Type = TransactionType.Vest,
                        Symbol = "MMKT", Quantity = 0.13m, PricePerShare = 0m, Amount = 0.13m },
                new() { Date = new(2026, 2, 27), Type = TransactionType.Vest,
                        Symbol = "MMKT", Quantity = 0.11m, PricePerShare = 0m, Amount = 0.11m },
                new() { Date = new(2026, 3, 31), Type = TransactionType.Dividend,
                        Symbol = "MMKT", Amount = 0.24m },
            }
        };

        var result = new ItrCalculator(fx, options).Compute(statement, new TaxPeriod(2026));
        var rows = result.ScheduleFaA3
            .Where(r => r.Symbol == "MMKT")
            .OrderBy(r => r.AcquisitionDate)
            .ToList();

        Assert.Equal(2, rows.Count);
        Assert.Equal(new DateOnly(2026, 1, 30), rows[0].AcquisitionDate);
        Assert.Equal(new DateOnly(2026, 2, 27), rows[1].AcquisitionDate);
        Assert.True(rows[0].InitialValueInr > 0);
        Assert.True(rows[1].InitialValueInr > 0);
    }

    [Fact]
    public void A3_ExcludesIncomeOnlySecurity_ButKeepsItInA2Credits()
    {
        // Reproduces the reported issue: a dividend/interest-only core cash-sweep fund
        // (FID TREASURY ONLY MMKT FUND) must not appear as a standalone A3 holding, while
        // the actually vested stock lots are all listed. The fund's dividend still counts
        // toward the custodial account (A2) gross-credited total.
        var fx = new InMemoryFxRateProvider();
        fx.AddRate("USD", new DateOnly(2025, 9, 2), 88m);
        fx.AddRate("USD", new DateOnly(2025, 12, 1), 90m);
        fx.AddRate("USD", new DateOnly(2025, 12, 15), 90m);
        fx.AddRate("USD", new DateOnly(2025, 12, 31), 90m);

        var statement = new BrokerStatement
        {
            Broker = Broker.Fidelity,
            CountryCode = "US",
            CountryCodeItr = "2",
            Currency = "USD",
            Transactions = new List<BrokerTransaction>
            {
                new() { Date = new(2025, 9, 2), Type = TransactionType.Vest,
                        Symbol = "MICROSOFT CORP", Quantity = 3m, PricePerShare = 300m, Amount = 900m },
                new() { Date = new(2025, 12, 1), Type = TransactionType.Vest,
                        Symbol = "MICROSOFT CORP", Quantity = 1m, PricePerShare = 300m, Amount = 300m },
                new() { Date = new(2025, 12, 15), Type = TransactionType.Dividend,
                        Symbol = "MICROSOFT CORP", Amount = 4m },
                // Core cash-sweep money-market fund: dividend/interest only, never held as shares.
                new() { Date = new(2025, 12, 31), Type = TransactionType.Dividend,
                        Symbol = "FID TREASURY ONLY MMKT FUND CL OUS", Amount = 0.57m },
            }
        };

        var options = new CalculatorOptions
        {
            ClosingPricesForeign = new Dictionary<string, decimal> { ["MICROSOFT CORP"] = 300m }
        };
        var result = new ItrCalculator(fx, options).Compute(statement, new TaxPeriod(2025));

        // No A3 row for the income-only fund; only the vested-stock lots are present.
        Assert.DoesNotContain(result.ScheduleFaA3, r => r.Symbol.Contains("MMKT"));
        Assert.All(result.ScheduleFaA3, r => Assert.Equal("MICROSOFT CORP", r.Symbol));
        Assert.Equal(2, result.ScheduleFaA3.Count);

        // The fund's dividend is not lost: it is still credited to the account (A2).
        var a2 = Assert.Single(result.ScheduleFaA2);
        Assert.Equal(0.57m + 4m, a2.GrossCreditedForeign);
        Assert.True(a2.GrossCreditedInr > 0);
    }

    [Fact]
    public void A3_DividendGoesToLotsVestedOnOrBeforeDividendDate()
    {
        // A dividend must be allocated only to lots vested on/before its date, weighted by
        // shares — it cannot belong to a lot that vested after the dividend was paid.
        var fx = new InMemoryFxRateProvider();
        fx.AddRate("USD", new DateOnly(2025, 3, 1), 90m);
        fx.AddRate("USD", new DateOnly(2025, 6, 1), 90m);   // dividend date
        fx.AddRate("USD", new DateOnly(2025, 9, 1), 90m);   // later lot
        fx.AddRate("USD", new DateOnly(2025, 12, 31), 90m);

        var statement = new BrokerStatement
        {
            Broker = Broker.Fidelity,
            CountryCode = "US",
            CountryCodeItr = "2",
            Currency = "USD",
            Transactions = new List<BrokerTransaction>
            {
                new() { Date = new(2025, 3, 1), Type = TransactionType.Vest,
                        Symbol = "MSFT", Quantity = 2m, PricePerShare = 300m, Amount = 600m },
                new() { Date = new(2025, 6, 1), Type = TransactionType.Dividend,
                        Symbol = "MSFT", Amount = 9m },
                new() { Date = new(2025, 9, 1), Type = TransactionType.Vest,
                        Symbol = "MSFT", Quantity = 1m, PricePerShare = 300m, Amount = 300m },
            }
        };

        var options = new CalculatorOptions
        {
            ClosingPricesForeign = new Dictionary<string, decimal> { ["MSFT"] = 300m }
        };
        var result = new ItrCalculator(fx, options).Compute(statement, new TaxPeriod(2025));

        var rows = result.ScheduleFaA3.OrderBy(r => r.AcquisitionDate).ToList();
        Assert.Equal(2, rows.Count);
        // Entire dividend (9 * 90 = 810) belongs to the March lot; the September lot gets none.
        Assert.Equal(810m, rows[0].GrossDividendInr);
        Assert.Equal(0m, rows[1].GrossDividendInr);
    }

    [Fact]
    public void A3_WhenNoSymbols_AddsDiagnosticWarning()
    {
        var fx = FxFixture.UsdRates();
        var statement = new BrokerStatement
        {
            Broker = Broker.Fidelity,
            InstitutionName = "Fidelity",
            CountryCode = "US",
            CountryCodeItr = "2",
            AccountNumber = "****5678",
            Currency = "USD",
            Transactions = new List<BrokerTransaction>
            {
                new() { Date = new(2023, 6, 15), Type = TransactionType.Dividend,
                        Symbol = string.Empty, Amount = 6.80m },
            }
        };

        var result = new ItrCalculator(fx).Compute(statement, new TaxPeriod(2023));

        Assert.Empty(result.ScheduleFaA3);
        Assert.Contains(result.Warnings, w => w.Contains("Schedule FA A3 is empty"));
    }

    [Fact]
    public void A2_AggregatesSecuritiesAndCredits()
    {
        var result = ComputeDividendScenario();
        var a2 = Assert.Single(result.ScheduleFaA2);

        Assert.Equal(311250m, a2.ClosingBalanceInr);
        Assert.Equal(311250m, a2.PeakBalanceInr);
        Assert.Equal(558m, a2.GrossCreditedInr);
    }

    [Fact]
    public void Fsi_UsesRule128RateAndMatchesWithholding()
    {
        var result = ComputeDividendScenario();
        var fsi = Assert.Single(result.ScheduleFsi);

        Assert.Equal(558m, fsi.IncomeInr);             // 6.80 * 82
        Assert.Equal(139m, fsi.ForeignTaxInr);         // 1.70 * 82 = 139.4 -> 139
        Assert.Equal("10", fsi.TreatyArticle);
        Assert.Equal(new DateOnly(2023, 5, 31), fsi.FxRateDate);
        Assert.Equal(82m, fsi.FxRate);
    }

    [Fact]
    public void Tr_And_Form67_ReflectForeignTax()
    {
        var result = ComputeDividendScenario();
        var tr = Assert.Single(result.ScheduleTr);
        Assert.Equal(139m, tr.ForeignTaxPaidInr);
        Assert.Equal(139m, tr.ReliefClaimedInr);
        Assert.Equal("90", tr.ReliefSection);

        var f67 = Assert.Single(result.Form67);
        Assert.Equal(139m, f67.ForeignTaxInr);
        Assert.Equal(558m, f67.IncomeInr);
    }

    [Fact]
    public void CapitalGains_Fifo_ClassifiesLongTerm()
    {
        var fx = new InMemoryFxRateProvider();
        fx.AddRate("USD", new DateOnly(2021, 5, 10), 73m);
        fx.AddRate("USD", new DateOnly(2023, 6, 15), 82m);
        fx.AddRate("USD", new DateOnly(2023, 12, 29), 83m);

        var statement = new BrokerStatement
        {
            Broker = Broker.Fidelity,
            Currency = "USD",
            Transactions = new List<BrokerTransaction>
            {
                new() { Date = new(2021, 5, 10), Type = TransactionType.Vest,
                        Symbol = "MSFT", Quantity = 10m, PricePerShare = 250m, Amount = 2500m },
                new() { Date = new(2023, 6, 15), Type = TransactionType.Sale,
                        Symbol = "MSFT", Quantity = 4m, PricePerShare = 300m, Amount = 1200m },
            }
        };

        var calc = new ItrCalculator(fx);
        var result = calc.Compute(statement, new TaxPeriod(2023));

        var cg = Assert.Single(result.CapitalGains);
        Assert.Equal(4m, cg.Quantity);
        Assert.Equal(98400m, cg.ProceedsInr);   // 4 * 300 * 82
        Assert.Equal(73000m, cg.CostInr);        // 4 * 250 * 73
        Assert.Equal(25400m, cg.GainInr);
        Assert.Equal(GainTerm.LongTerm, cg.Term);
        Assert.True(cg.HoldingMonths > 24);
    }

    [Fact]
    public void CapitalGains_MissingLot_RaisesWarning()
    {
        var fx = new InMemoryFxRateProvider();
        fx.AddRate("USD", new DateOnly(2023, 6, 15), 82m);
        fx.AddRate("USD", new DateOnly(2023, 12, 29), 83m);

        var statement = new BrokerStatement
        {
            Broker = Broker.Fidelity,
            Currency = "USD",
            Transactions = new List<BrokerTransaction>
            {
                new() { Date = new(2023, 6, 15), Type = TransactionType.Sale,
                        Symbol = "MSFT", Quantity = 4m, PricePerShare = 300m, Amount = 1200m },
            }
        };

        var calc = new ItrCalculator(fx);
        var result = calc.Compute(statement, new TaxPeriod(2023));

        Assert.Empty(result.CapitalGains);
        Assert.Contains(result.Warnings, w => w.Contains("missing cost basis"));
    }

    [Fact]
    public void A3_UsesAcquisitionPriceFallback_WhenVestHasNoPriceOrAmount()
    {
        // RSU/ESPP share-deposit rows often carry a quantity but no price or cash amount, which
        // would leave the FA A3 Initial value at zero. A user-supplied vest-day price fills it in.
        var statement = new BrokerStatement
        {
            Broker = Broker.Fidelity,
            Currency = "USD",
            Transactions = new List<BrokerTransaction>
            {
                new() { Date = new(2023, 1, 15), Type = TransactionType.Vest,
                        Symbol = "MSFT", Quantity = 10m, PricePerShare = 0m, Amount = 0m },
            }
        };

        var options = new CalculatorOptions
        {
            AcquisitionPricesForeign = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["MSFT"] = 250m
            }
        };

        var result = new ItrCalculator(FxFixture.UsdRates(), options)
            .Compute(statement, new TaxPeriod(2023));
        var row = Assert.Single(result.ScheduleFaA3);

        Assert.Equal(202500m, row.InitialValueInr); // 10 * 250 * 81
        Assert.DoesNotContain(result.Warnings, w => w.Contains("Initial value is understated"));
    }

    [Fact]
    public void A3_DateSpecificAcquisitionPrice_TakesPrecedence()
    {
        var statement = new BrokerStatement
        {
            Broker = Broker.Fidelity,
            Currency = "USD",
            Transactions = new List<BrokerTransaction>
            {
                new() { Date = new(2023, 1, 15), Type = TransactionType.Vest,
                        Symbol = "MSFT", Quantity = 10m },
            }
        };

        var options = new CalculatorOptions
        {
            AcquisitionPricesForeign = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["MSFT"] = 100m,
                ["MSFT@2023-01-15"] = 250m
            }
        };

        var row = Assert.Single(new ItrCalculator(FxFixture.UsdRates(), options)
            .Compute(statement, new TaxPeriod(2023)).ScheduleFaA3);

        Assert.Equal(202500m, row.InitialValueInr); // date-specific 250 wins over 100
    }

    [Fact]
    public void A3_WarnsWhenVestPriceMissingAndNoAcquisitionPrice()
    {
        var statement = new BrokerStatement
        {
            Broker = Broker.Fidelity,
            Currency = "USD",
            Transactions = new List<BrokerTransaction>
            {
                new() { Date = new(2023, 1, 15), Type = TransactionType.Vest,
                        Symbol = "MSFT", Quantity = 10m },
            }
        };

        var result = new ItrCalculator(FxFixture.UsdRates())
            .Compute(statement, new TaxPeriod(2023));
        var row = Assert.Single(result.ScheduleFaA3);

        Assert.Equal(0m, row.InitialValueInr);
        Assert.Contains(result.Warnings, w => w.Contains("A3[MSFT]") && w.Contains("no price or amount"));
    }

    [Fact]
    public void TaxPeriod_LabelsAndWindows()
    {
        var p = new TaxPeriod(2023);
        Assert.Equal("2024-25", p.AssessmentYearLabel);
        Assert.Equal("2023-24", p.FinancialYearLabel);
        Assert.True(p.IsInCalendarYear(new DateOnly(2023, 12, 31)));
        Assert.False(p.IsInCalendarYear(new DateOnly(2024, 1, 1)));
        Assert.True(p.IsInFinancialYear(new DateOnly(2024, 3, 31)));
        Assert.False(p.IsInFinancialYear(new DateOnly(2023, 3, 31)));
    }
}
