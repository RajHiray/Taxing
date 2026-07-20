using Taxing.Core.Models;
using Taxing.Parsing.Profiles;

namespace Taxing.Parsing;

/// <summary>Resolves the correct <see cref="IStatementParser"/> for a selected broker.</summary>
public static class StatementParserFactory
{
    private static readonly IReadOnlyList<IStatementParser> Parsers = new IStatementParser[]
    {
        new FidelityParser(),
        new MorganStanleyParser(),
        new ETradeParser()
    };

    /// <summary>All available parsers (for populating a broker-selection dropdown).</summary>
    public static IReadOnlyList<IStatementParser> All => Parsers;

    /// <summary>Returns the parser for the given broker.</summary>
    public static IStatementParser For(Broker broker) =>
        Parsers.FirstOrDefault(p => p.Broker == broker)
        ?? throw new NotSupportedException($"No parser registered for broker '{broker}'.");
}
