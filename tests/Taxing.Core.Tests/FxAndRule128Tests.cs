using Taxing.Core.Engine;
using Taxing.Core.Fx;

namespace Taxing.Core.Tests;

public class FxAndRule128Tests
{
    [Fact]
    public void GetRate_ExactDate_ReturnsRate()
    {
        var fx = FxFixture.UsdRates();
        Assert.Equal(83m, fx.GetRate("USD", new DateOnly(2023, 2, 15)));
    }

    [Fact]
    public void GetRate_MissingDate_UsesPrecedingWorkingDay()
    {
        var fx = FxFixture.UsdRates();
        // 31 Dec 2023 has no rate; last available is 29 Dec (83).
        Assert.Equal(83m, fx.GetRate("USD", new DateOnly(2023, 12, 31)));
    }

    [Fact]
    public void GetRate_BeforeAnyRate_Throws()
    {
        var fx = FxFixture.UsdRates();
        Assert.Throws<InvalidOperationException>(() => fx.GetRate("USD", new DateOnly(2000, 1, 1)));
    }

    [Theory]
    [InlineData(2023, 1, 15, 2022, 12, 31)]  // Jan income -> 31 Dec
    [InlineData(2023, 2, 10, 2023, 1, 31)]   // Feb income -> 31 Jan
    [InlineData(2023, 6, 5, 2023, 5, 31)]    // Jun income -> 31 May
    public void Rule128RateDate_IsLastDayOfPrecedingMonth(
        int y, int m, int d, int ey, int em, int ed)
    {
        var date = new DateOnly(y, m, d);
        Assert.Equal(new DateOnly(ey, em, ed), Rule128Converter.Rule128RateDate(date));
    }

    [Fact]
    public void ConvertIncome_UsesPrecedingMonthRate()
    {
        var fx = FxFixture.UsdRates();
        // Jan 2023 income of $100 -> rate on 31 Dec 2022 = 82 -> INR 8200.
        var inr = Rule128Converter.ConvertIncome(fx, "USD", 100m, new DateOnly(2023, 1, 15));
        Assert.Equal(8200m, inr);
    }
}
