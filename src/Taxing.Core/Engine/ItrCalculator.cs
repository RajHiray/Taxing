using Taxing.Core.Fx;
using Taxing.Core.Models;

namespace Taxing.Core.Engine;

/// <summary>
/// Orchestrates conversion of a normalized <see cref="BrokerStatement"/> into the
/// full set of ITR schedule outputs (FA A2/A3, FSI, TR, Form 67 and CG).
///
/// Reporting-period rules:
/// * Schedule FA (A2/A3) values use the calendar year (1 Jan .. 31 Dec).
/// * Income schedules (FSI/CG/TR) use the financial year (1 Apr .. 31 Mar).
/// FA balances are valued at the SBI TTBR on the valuation date; income is
/// converted using the Rule 128 rate date (last day of the preceding month).
/// </summary>
public sealed class ItrCalculator
{
    private readonly IFxRateProvider _fx;
    private readonly CalculatorOptions _options;

    public ItrCalculator(IFxRateProvider fx, CalculatorOptions? options = null)
    {
        _fx = fx ?? throw new ArgumentNullException(nameof(fx));
        _options = options ?? new CalculatorOptions();
    }

    public ItrResult Compute(BrokerStatement statement, TaxPeriod period)
    {
        ArgumentNullException.ThrowIfNull(statement);
        ArgumentNullException.ThrowIfNull(period);

        var warnings = new List<string>();
        var currency = statement.Currency;

        var a3 = BuildA3(statement, period, currency, warnings);
        var a2 = BuildA2(statement, period, currency, a3, warnings);
        var fsi = BuildFsi(statement, period, currency, warnings);
        var (tr, form67) = BuildTrAndForm67(fsi, statement);
        var cg = BuildCapitalGains(statement, period, currency, warnings);

        return new ItrResult
        {
            Period = period,
            Broker = statement.Broker,
            ScheduleFaA2 = a2,
            ScheduleFaA3 = a3,
            ScheduleFsi = fsi,
            ScheduleTr = tr,
            Form67 = form67,
            CapitalGains = cg,
            Warnings = warnings
        };
    }

    private decimal RoundInr(decimal value) =>
        Math.Round(value, _options.InrRoundingDecimals, MidpointRounding.AwayFromZero);

    // ---------------- Schedule FA A3 (per security) ----------------

    private List<ScheduleFaA3> BuildA3(
        BrokerStatement s, TaxPeriod p, string currency, List<string> warnings)
    {
        var rows = new List<ScheduleFaA3>();

        var symbols = s.Transactions
            .Where(t => !string.IsNullOrWhiteSpace(t.Symbol))
            .Select(t => t.Symbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

        foreach (var symbol in symbols)
        {
            var txns = s.Transactions
                .Where(t => string.Equals(t.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.Date)
                .ToList();

            // Determine acquisition date/cost from the first vest at or before the CY end.
            var vests = txns.Where(t => t.Type == TransactionType.Vest).ToList();
            DateOnly? acqDate = vests.Count > 0 ? vests.Min(t => t.Date) : null;

            decimal initialValueInr = 0m;
            foreach (var v in vests.Where(v => v.Date <= p.CalendarYearEnd))
            {
                var rate = _fx.GetRate(currency, v.Date);
                initialValueInr += v.Quantity * v.PricePerShare * rate;
            }

            // Peak value: approximate across event dates within the calendar year using
            // running share count and the price observed on each event date.
            decimal peakForeign = 0m;
            decimal peakInr = 0m;
            decimal runningShares = 0m;
            decimal lastPriceForeign = 0m;
            decimal closingShares = 0m;

            // Seed running share count with shares acquired before the calendar year.
            foreach (var t in txns)
            {
                if (t.Type == TransactionType.Vest || t.Type == TransactionType.Sale)
                {
                    if (t.PricePerShare > 0) lastPriceForeign = t.PricePerShare;

                    if (t.Date < p.CalendarYearStart)
                    {
                        runningShares += t.Type == TransactionType.Vest ? t.Quantity : -t.Quantity;
                        continue;
                    }
                    if (t.Date > p.CalendarYearEnd) continue;

                    runningShares += t.Type == TransactionType.Vest ? t.Quantity : -t.Quantity;

                    var price = t.PricePerShare > 0 ? t.PricePerShare : lastPriceForeign;
                    var valForeign = runningShares * price;
                    if (valForeign > peakForeign)
                    {
                        peakForeign = valForeign;
                        peakInr = valForeign * _fx.GetRate(currency, t.Date);
                    }
                }
            }
            closingShares = runningShares;

            // Closing value at 31 Dec.
            var closingPrice = ResolveClosingPrice(symbol, lastPriceForeign);
            var closingForeign = closingShares * closingPrice;
            var closingInr = closingForeign > 0
                ? closingForeign * _fx.GetRate(currency, p.CalendarYearEnd)
                : 0m;

            if (closingForeign > peakForeign)
            {
                peakForeign = closingForeign;
                peakInr = closingInr;
            }

            // Dividends & proceeds credited during the calendar year (FA basis: TTBR on date).
            decimal grossDivInr = 0m;
            decimal proceedsInr = 0m;
            foreach (var t in txns.Where(t => p.IsInCalendarYear(t.Date)))
            {
                if (t.Type == TransactionType.Dividend)
                    grossDivInr += t.Amount * _fx.GetRate(currency, t.Date);
                else if (t.Type == TransactionType.Sale)
                    proceedsInr += t.Amount * _fx.GetRate(currency, t.Date);
            }

            if (_options.ClosingPricesForeign is null ||
                !_options.ClosingPricesForeign.ContainsKey(symbol))
            {
                if (closingShares > 0)
                    warnings.Add(
                        $"A3[{symbol}]: no explicit 31-Dec closing price supplied; " +
                        $"used last observed price {lastPriceForeign:0.####} {currency}. " +
                        "Provide the year-end market price for accuracy.");
            }

            rows.Add(new ScheduleFaA3
            {
                CountryCode = s.CountryCode,
                CountryCodeItr = s.CountryCodeItr,
                EntityName = symbol,
                EntityAddress = s.InstitutionAddress,
                Symbol = symbol,
                AcquisitionDate = acqDate,
                InitialValueInr = RoundInr(initialValueInr),
                PeakValueInr = RoundInr(peakInr),
                ClosingValueInr = RoundInr(closingInr),
                GrossDividendInr = RoundInr(grossDivInr),
                ProceedsInr = RoundInr(proceedsInr),
                ClosingShares = closingShares
            });
        }

        return rows;
    }

    private decimal ResolveClosingPrice(string symbol, decimal fallback)
    {
        if (_options.ClosingPricesForeign is not null &&
            _options.ClosingPricesForeign.TryGetValue(symbol, out var price) && price > 0)
            return price;
        return fallback;
    }

    // ---------------- Schedule FA A2 (custodial account) ----------------

    private List<ScheduleFaA2> BuildA2(
        BrokerStatement s, TaxPeriod p, string currency,
        List<ScheduleFaA3> a3, List<string> warnings)
    {
        // Account value approximated as securities value + closing cash.
        var peakInr = a3.Sum(r => r.PeakValueInr);
        var closingSecuritiesInr = a3.Sum(r => r.ClosingValueInr);
        var closingSecuritiesForeign = 0m; // not tracked separately; documented approximation

        var closingCashInr = s.ClosingCashBalance > 0
            ? s.ClosingCashBalance * _fx.GetRate(currency, p.CalendarYearEnd)
            : 0m;

        var grossCreditedInr = a3.Sum(r => r.GrossDividendInr + r.ProceedsInr);
        var grossCreditedForeign = s.Transactions
            .Where(t => p.IsInCalendarYear(t.Date) &&
                        (t.Type == TransactionType.Dividend || t.Type == TransactionType.Sale))
            .Sum(t => t.Amount);

        warnings.Add(
            "A2: peak/closing balances are approximated as the sum of held securities' " +
            "values plus closing cash. Verify against the account's own peak-value statement.");

        return new List<ScheduleFaA2>
        {
            new()
            {
                CountryCode = s.CountryCode,
                CountryCodeItr = s.CountryCodeItr,
                InstitutionName = s.InstitutionName,
                InstitutionAddress = s.InstitutionAddress,
                AccountNumber = s.AccountNumber,
                Status = "Owner",
                AccountOpenedDate = s.AccountOpenedDate,
                PeakBalanceInr = RoundInr(peakInr + closingCashInr),
                ClosingBalanceInr = RoundInr(closingSecuritiesInr + closingCashInr),
                GrossCreditedInr = RoundInr(grossCreditedInr),
                PeakBalanceForeign = 0m,
                ClosingBalanceForeign = closingSecuritiesForeign + s.ClosingCashBalance,
                GrossCreditedForeign = grossCreditedForeign,
                Currency = currency
            }
        };
    }

    // ---------------- Schedule FSI (foreign source income, FY basis) ----------------

    private List<ScheduleFsiRow> BuildFsi(
        BrokerStatement s, TaxPeriod p, string currency, List<string> warnings)
    {
        var rows = new List<ScheduleFsiRow>();

        // Aggregate dividends and withholding per (symbol, date) within the financial year.
        var dividends = s.Transactions
            .Where(t => t.Type == TransactionType.Dividend && p.IsInFinancialYear(t.Date))
            .GroupBy(t => (t.Symbol, t.Date));

        foreach (var g in dividends.OrderBy(g => g.Key.Date))
        {
            var (symbol, date) = g.Key;
            var grossForeign = g.Sum(t => t.Amount);

            var taxForeign = s.Transactions
                .Where(t => t.Type == TransactionType.TaxWithheld &&
                            t.Date == date &&
                            string.Equals(t.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                .Sum(t => Math.Abs(t.Amount));

            var rateDate = Rule128Converter.Rule128RateDate(date);
            var rate = _fx.GetRate(currency, rateDate);

            rows.Add(new ScheduleFsiRow
            {
                CountryCode = s.CountryCode,
                CountryCodeItr = s.CountryCodeItr,
                IncomeHead = "Dividend (Other Sources)",
                IncomeDate = date,
                IncomeInr = RoundInr(grossForeign * rate),
                ForeignTaxInr = RoundInr(taxForeign * rate),
                TreatyArticle = "10",
                IncomeForeign = grossForeign,
                ForeignTaxForeign = taxForeign,
                FxRate = rate,
                FxRateDate = rateDate
            });
        }

        return rows;
    }

    // ---------------- Schedule TR + Form 67 ----------------

    private (List<ScheduleTrRow> Tr, List<Form67Row> Form67) BuildTrAndForm67(
        List<ScheduleFsiRow> fsi, BrokerStatement s)
    {
        var form67 = fsi.Select(r => new Form67Row
        {
            CountryCode = r.CountryCode,
            CountryCodeItr = r.CountryCodeItr,
            SourceOfIncome = r.IncomeHead,
            IncomeInr = r.IncomeInr,
            ForeignTaxInr = r.ForeignTaxInr,
            TreatyArticle = r.TreatyArticle,
            FxRate = r.FxRate,
            FxRateDate = r.FxRateDate
        }).ToList();

        var tr = fsi
            .GroupBy(r => (r.CountryCode, r.CountryCodeItr))
            .Select(g => new ScheduleTrRow
            {
                CountryCode = g.Key.CountryCode,
                CountryCodeItr = g.Key.CountryCodeItr,
                ForeignTaxPaidInr = g.Sum(r => r.ForeignTaxInr),
                // Relief is the lower of foreign tax paid and Indian tax on that income;
                // the Indian-tax cap is applied by the filer, so we expose foreign tax paid here.
                ReliefClaimedInr = g.Sum(r => r.ForeignTaxInr),
                ReliefSection = "90"
            })
            .ToList();

        return (tr, form67);
    }

    // ---------------- Schedule CG (FIFO capital gains, FY basis) ----------------

    private List<CapitalGainRow> BuildCapitalGains(
        BrokerStatement s, TaxPeriod p, string currency, List<string> warnings)
    {
        var rows = new List<CapitalGainRow>();

        // Per-symbol FIFO lot queues built from all vests (any date).
        var bySymbol = s.Transactions
            .Where(t => !string.IsNullOrWhiteSpace(t.Symbol) &&
                        (t.Type == TransactionType.Vest || t.Type == TransactionType.Sale))
            .GroupBy(t => t.Symbol, StringComparer.OrdinalIgnoreCase);

        foreach (var group in bySymbol)
        {
            var lots = new Queue<Lot>();
            foreach (var t in group.OrderBy(t => t.Date))
            {
                if (t.Type == TransactionType.Vest)
                {
                    lots.Enqueue(new Lot(t.Date, t.Quantity, t.PricePerShare));
                    continue;
                }

                // Sale: match FIFO. Only report sales that fall within the financial year.
                var remaining = t.Quantity;
                var salePrice = t.PricePerShare > 0 && t.Quantity != 0
                    ? t.PricePerShare
                    : (t.Quantity != 0 ? t.Amount / t.Quantity : 0m);

                while (remaining > 0 && lots.Count > 0)
                {
                    var lot = lots.Peek();
                    var matched = Math.Min(remaining, lot.Quantity);

                    if (p.IsInFinancialYear(t.Date))
                    {
                        var proceedsForeign = matched * salePrice;
                        var costForeign = matched * lot.CostPerShare;
                        var proceedsInr = proceedsForeign * _fx.GetRate(currency, t.Date);
                        var costInr = costForeign * _fx.GetRate(currency, lot.AcquisitionDate);
                        var months = MonthsBetween(lot.AcquisitionDate, t.Date);

                        rows.Add(new CapitalGainRow
                        {
                            Symbol = group.Key,
                            AcquisitionDate = lot.AcquisitionDate,
                            SaleDate = t.Date,
                            Quantity = matched,
                            ProceedsInr = RoundInr(proceedsInr),
                            CostInr = RoundInr(costInr),
                            Term = months > _options.LongTermHoldingMonths
                                ? GainTerm.LongTerm
                                : GainTerm.ShortTerm,
                            ProceedsForeign = proceedsForeign,
                            CostForeign = costForeign,
                            HoldingMonths = months
                        });
                    }

                    lot.Quantity -= matched;
                    remaining -= matched;
                    if (lot.Quantity <= 0) lots.Dequeue();
                }

                if (remaining > 0 && p.IsInFinancialYear(t.Date))
                    warnings.Add(
                        $"CG[{group.Key}]: sale on {t.Date:yyyy-MM-dd} exceeds known acquired lots " +
                        $"by {remaining} shares; missing cost basis. Add the acquisition lot.");
            }
        }

        return rows;
    }

    private static int MonthsBetween(DateOnly from, DateOnly to)
    {
        var months = ((to.Year - from.Year) * 12) + (to.Month - from.Month);
        if (to.Day < from.Day) months--;
        return months;
    }

    private sealed class Lot
    {
        public Lot(DateOnly acquisitionDate, decimal quantity, decimal costPerShare)
        {
            AcquisitionDate = acquisitionDate;
            Quantity = quantity;
            CostPerShare = costPerShare;
        }

        public DateOnly AcquisitionDate { get; }
        public decimal Quantity { get; set; }
        public decimal CostPerShare { get; }
    }
}
