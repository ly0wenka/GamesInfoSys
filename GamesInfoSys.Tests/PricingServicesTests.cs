using GamesInfoSys.Services;
using Microsoft.Extensions.Options;

namespace GamesInfoSys.Tests;

public sealed class PricingServicesTests
{
    [Fact]
    public void RegionResolver_UsesPlatformOverride_WhenConfigured()
    {
        var options = Options.Create(new PricingOptions
        {
            DefaultRegion = "ua",
            PlatformRegions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Switch"] = "za"
            }
        });

        var resolver = new RegionResolver(options);

        Assert.Equal("UA", resolver.DefaultRegion);
        Assert.Equal("ZA", resolver.ForPlatform(GamePlatform.Switch));
        Assert.Equal("UA", resolver.ForPlatform(GamePlatform.Xbox));
    }

    [Fact]
    public async Task CurrencyConverter_ConvertsKnownCurrency_ToUah()
    {
        var converter = new CurrencyConverter(new FakeExchangeRateProvider(new Dictionary<string, decimal>
        {
            ["UAH"] = 1m,
            ["USD"] = 41.50m
        }));

        var actual = await converter.ToUahAsync(10m, "usd");

        Assert.Equal(415m, actual);
    }

    [Fact]
    public async Task CurrencyConverter_ReturnsNull_ForMissingRate()
    {
        var converter = new CurrencyConverter(new FakeExchangeRateProvider(new Dictionary<string, decimal>()));

        var actual = await converter.ToUahAsync(10m, "EUR");

        Assert.Null(actual);
    }

    private sealed class FakeExchangeRateProvider : IExchangeRateProvider
    {
        private readonly Dictionary<string, decimal> _rates;

        public FakeExchangeRateProvider(Dictionary<string, decimal> rates)
        {
            _rates = rates;
        }

        public Task<Dictionary<string, decimal>> GetRatesToUahAsync()
        {
            return Task.FromResult(_rates);
        }
    }
}

