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
