using System.Net;
using System.Text;
using GamesInfoSys.Services;
using Microsoft.Extensions.Caching.Memory;

namespace GamesInfoSys.Tests;

public sealed class HttpClientIntegrationTests
{
    [Fact]
    public async Task SteamStoreClient_ReadsHeaderImageAndPrice_FromApiResponse()
    {
        const string payload = """
        {
          "1245620": {
            "success": true,
            "data": {
              "name": "ELDEN RING",
              "header_image": "https://shared.example/header.jpg",
              "price_overview": {
                "currency": "UAH",
                "final": 159900,
                "initial": 199900,
                "discount_percent": 20
              }
            }
          }
        }
        """;

        var client = new SteamStoreClient(CreateHttpClient(payload, "https://store.steampowered.com/"));

        var metadata = await client.GetAppMetadataAsync("1245620");
        var price = await client.GetAppPriceAsync("1245620", "UA");

        Assert.Equal("https://shared.example/header.jpg", metadata?.HeaderImage);
        Assert.Equal("ELDEN RING", price?.Name);
        Assert.Equal(159900, price?.FinalMinor);
    }

    [Fact]
    public async Task CheapSharkClient_MapsDealPayload()
    {
        const string payload = """
        [
          {
            "dealID": "abc123",
            "storeId": "1",
            "title": "Hollow Knight",
            "salePrice": "7.49",
            "normalPrice": "14.99",
            "savings": "50.03",
            "steamAppId": "367520"
          }
        ]
        """;

        var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new CheapSharkClient(CreateHttpClient(payload, "https://www.cheapshark.com/"), cache);

        var deals = await client.GetDealsBySteamAppIdAsync("367520");

        var deal = Assert.Single(deals);
        Assert.Equal("abc123", deal.DealId);
        Assert.Equal(7.49m, deal.SalePriceUsd);
        Assert.Equal("https://www.cheapshark.com/redirect?dealID=abc123", CheapSharkClient.RedirectUrl(deal.DealId));
    }

    [Fact]
    public async Task NbuRatesClient_ReadsExchangeRates()
    {
        const string payload = """
        [
          { "cc": "USD", "rate": 41.25 },
          { "cc": "EUR", "rate": 45.10 }
        ]
        """;

        var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new NbuRatesClient(CreateHttpClient(payload, "https://bank.gov.ua/"), cache);

        var rates = await client.GetRatesToUahAsync();

        Assert.Equal(1m, rates["UAH"]);
        Assert.Equal(41.25m, rates["USD"]);
        Assert.Equal(45.10m, rates["EUR"]);
    }

    private static HttpClient CreateHttpClient(string content, string baseAddress)
    {
        return new HttpClient(new FakeHttpMessageHandler(content))
        {
            BaseAddress = new Uri(baseAddress)
        };
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _content;

        public FakeHttpMessageHandler(string content)
        {
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
