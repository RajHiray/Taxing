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
