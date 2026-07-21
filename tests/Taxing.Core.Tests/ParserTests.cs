using Taxing.Core.Models;
using Taxing.Parsing;

namespace Taxing.Core.Tests;

public class ParserTests
{
    private const string FidelityCsv =
        "Run Date,Action,Symbol,Quantity,Price,Amount\n" +
        "01/15/2023,YOU BOUGHT (RSU VEST),MSFT,10,250.00,2500.00\n" +
        "06/15/2023,DIVIDEND RECEIVED,MSFT,,,6.80\n" +
        "06/15/2023,NRA TAX WITHHELD,MSFT,,,-1.70\n" +
        "07/20/2023,YOU SOLD,MSFT,4,300.00,1200.00\n";

    [Fact]
    public void Fidelity_ParsesAllRelevantRows()
    {
        var parser = StatementParserFactory.For(Broker.Fidelity);
        var meta = new StatementMetadata
        {
            InstitutionName = "Fidelity",
            AccountNumber = "Z12345678",
            Currency = "USD"
        };

        var statement = parser.Parse(FidelityCsv, meta);

        Assert.Equal(4, statement.Transactions.Count);
        Assert.Equal("*****5678", statement.AccountNumber); // masked
        Assert.Contains(statement.Transactions, t => t.Type == TransactionType.Vest && t.Quantity == 10m);
        Assert.Contains(statement.Transactions, t => t.Type == TransactionType.Dividend && t.Amount == 6.80m);
        Assert.Contains(statement.Transactions, t => t.Type == TransactionType.TaxWithheld && t.Amount == 1.70m);
        Assert.Contains(statement.Transactions, t => t.Type == TransactionType.Sale && t.Quantity == 4m);
    }

    [Fact]
    public void Fidelity_BlankSymbol_FallsBackToDescription()
    {
        // Real broker exports sometimes leave the Symbol column blank and name the
        // security only in a Description column; the security identity must still be built.
        const string csv =
            "Run Date,Action,Symbol,Description,Quantity,Price,Amount\n" +
            "01/15/2023,YOU BOUGHT (RSU VEST),,MICROSOFT CORP,10,250.00,2500.00\n" +
            "06/15/2023,DIVIDEND RECEIVED,,MICROSOFT CORP,,,6.80\n";

        var statement = StatementParserFactory.For(Broker.Fidelity)
            .Parse(csv, new StatementMetadata { Currency = "USD" });

        Assert.All(statement.Transactions, t => Assert.Equal("MICROSOFT CORP", t.Symbol));
    }

    [Fact]
    public void Fidelity_FeeWithBlankSymbol_StaysSymbolLess()
    {
        // Pure cash/fee events should not be turned into bogus securities.
        const string csv =
            "Run Date,Action,Symbol,Description,Quantity,Price,Amount\n" +
            "01/31/2023,FEE CHARGED,,ADVISOR FEE,,,-5.00\n";

        var statement = StatementParserFactory.For(Broker.Fidelity)
            .Parse(csv, new StatementMetadata { Currency = "USD" });

        var fee = Assert.Single(statement.Transactions);
        Assert.Equal(TransactionType.Fee, fee.Type);
        Assert.Equal(string.Empty, fee.Symbol);
    }

    [Fact]
    public void Fidelity_InvestmentNameHeader_FallsBackToDescription()
    {
        const string csv =
            "Transaction date,Transaction type,Investment name,Shares,Amount\n" +
            "Mar-31-2026,DIVIDEND RECEIVED,FID TREASURY ONLY MMKT FUND CL OUS,-,$0.14\n";

        var statement = StatementParserFactory.For(Broker.Fidelity)
            .Parse(csv, new StatementMetadata { Currency = "USD" });

        var dividend = Assert.Single(statement.Transactions);
        Assert.Equal(TransactionType.Dividend, dividend.Type);
        Assert.Equal("FID TREASURY ONLY MMKT FUND CL OUS", dividend.Symbol);
        Assert.Equal(0.14m, dividend.Amount);
    }

    [Fact]
    public void Fidelity_Reinvestment_IsParsedAsVest()
    {
        const string csv =
            "Transaction date,Transaction type,Investment name,Shares,Amount\n" +
            "Mar-31-2026,REINVESTMENT REINVEST @ $1.000,FID TREASURY ONLY MMKT FUND CL OUS,0.14,-$0.14\n";

        var statement = StatementParserFactory.For(Broker.Fidelity)
            .Parse(csv, new StatementMetadata { Currency = "USD" });

        var txn = Assert.Single(statement.Transactions);
        Assert.Equal(TransactionType.Vest, txn.Type);
        Assert.Equal(0.14m, txn.Quantity);
        Assert.Equal(0.14m, txn.Amount);
    }

    [Fact]
    public void Fidelity_SymbolInAlternateHeader_IsRecognized()
    {
        const string csv =
            "Run Date,Action,Symbol/CUSIP,Quantity,Price,Amount\n" +
            "01/15/2023,YOU BOUGHT (RSU VEST),MSFT,10,250.00,2500.00\n";

        var statement = StatementParserFactory.For(Broker.Fidelity)
            .Parse(csv, new StatementMetadata { Currency = "USD" });

        Assert.Equal("MSFT", Assert.Single(statement.Transactions).Symbol);
    }

    [Fact]
    public void Factory_ExposesAllBrokers()
    {
        Assert.Equal(3, StatementParserFactory.All.Count);
        Assert.NotNull(StatementParserFactory.For(Broker.MorganStanley));
        Assert.NotNull(StatementParserFactory.For(Broker.ETrade));
    }

    [Fact]
    public void Parser_UnknownColumns_Throws()
    {
        var parser = StatementParserFactory.For(Broker.Fidelity);
        Assert.Throws<FormatException>(() =>
            parser.Parse("foo,bar\n1,2\n", new StatementMetadata()));
    }
}
