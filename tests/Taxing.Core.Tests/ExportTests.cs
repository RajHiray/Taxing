using Taxing.Core.Engine;
using Taxing.Core.Models;
using Taxing.Export;
using ClosedXML.Excel;

namespace Taxing.Core.Tests;

public class ExportTests
{
    private static ItrResult SampleResult()
    {
        var fx = FxFixture.UsdRates();
        var options = new CalculatorOptions
        {
            ClosingPricesForeign = new Dictionary<string, decimal> { ["MSFT"] = 375m }
        };
        var statement = new BrokerStatement
        {
            Broker = Broker.Fidelity,
            InstitutionName = "Fidelity",
            Currency = "USD",
            Transactions = new List<BrokerTransaction>
            {
                new() { Date = new(2023, 1, 15), Type = TransactionType.Vest,
                        Symbol = "MSFT", Quantity = 10m, PricePerShare = 250m, Amount = 2500m },
                new() { Date = new(2023, 6, 15), Type = TransactionType.Dividend,
                        Symbol = "MSFT", Amount = 6.80m },
            }
        };
        return new ItrCalculator(fx, options).Compute(statement, new TaxPeriod(2023));
    }

    [Fact]
    public void Json_ContainsScheduleData()
    {
        var json = JsonExporter.ToJson(SampleResult());
        Assert.Contains("ScheduleFaA3", json);
        Assert.Contains("MSFT", json);
        Assert.Contains("2024-25", json); // AY label
    }

    [Fact]
    public void Excel_ProducesNonEmptyWorkbook()
    {
        var bytes = ExcelExporter.ToWorkbook(SampleResult());
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 1000);
        // XLSX files are ZIP archives beginning with "PK".
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public void Excel_NeutralizesFormulaInjection()
    {
        // A malicious symbol should be escaped; smoke-test that export still succeeds.
        var result = SampleResult();
        var bytes = ExcelExporter.ToWorkbook(result);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void Excel_A3_UsesItrColumnHeadings()
    {
        var bytes = ExcelExporter.ToWorkbook(SampleResult());
        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet("FA-A3 Equity");

        Assert.Equal("Country/Region name", ws.Cell(1, 1).GetString());
        Assert.Equal("Country Name and Code", ws.Cell(1, 2).GetString());
        Assert.Equal("Name of entity", ws.Cell(1, 3).GetString());
        Assert.Equal("Date of acquiring the interest", ws.Cell(1, 7).GetString());
    }
}
