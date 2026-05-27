using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GamesInfoSys.Services;

public sealed class NbuRatesClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;

    public NbuRatesClient(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public Task<Dictionary<string, decimal>> GetRatesToUahAsync()
    {
        // Map: ISO currency -> UAH per 1 unit of currency.
        return GetRatesToUahCoreAsync();
    }

    private async Task<Dictionary<string, decimal>> GetRatesToUahCoreAsync()
    {
        var rates = await _cache.GetOrCreateAsync("rates:nbu:to_uah", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);

            // NBU endpoint (JSON): /NBUStatService/v1/statdirectory/exchange?json
            using var res = await _http.GetAsync("NBUStatService/v1/statdirectory/exchange?json");
            res.EnsureSuccessStatusCode();

            await using var stream = await res.Content.ReadAsStreamAsync();
            var list = await JsonSerializer.DeserializeAsync<List<NbuRate>>(stream, JsonOptions) ?? [];

            var dict = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["UAH"] = 1m
            };

            foreach (var r in list)
            {
                if (string.IsNullOrWhiteSpace(r.Cc))
                    continue;
                if (r.Rate <= 0)
                    continue;
                dict[r.Cc] = r.Rate;
            }

            return dict;
        });

        return rates ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["UAH"] = 1m };
    }

    private sealed class NbuRate
    {
        [JsonPropertyName("cc")]
        public string? Cc { get; set; }

        [JsonPropertyName("rate")]
        public decimal Rate { get; set; }
    }
}
