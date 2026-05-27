using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace GamesInfoSys.Services;

public sealed class CheapSharkClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;

    public CheapSharkClient(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public async Task<IReadOnlyList<CheapSharkDeal>> GetDealsBySteamAppIdAsync(string steamAppId, int pageSize = 20, bool onSaleOnly = false)
    {
        if (string.IsNullOrWhiteSpace(steamAppId))
            return [];
        if (!int.TryParse(steamAppId, out _))
            return [];

        pageSize = Math.Clamp(pageSize, 1, 60);

        var cacheKey = $"cheapshark:deals:steam={steamAppId}:ps={pageSize}:sale={onSaleOnly}";
        var cached = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

            var url = $"api/1.0/deals?steamAppID={Uri.EscapeDataString(steamAppId)}&pageNumber=0&pageSize={pageSize}&sortBy=Price&desc=0";
            if (onSaleOnly)
                url += "&onSale=1";

            using var res = await _http.GetAsync(url);
            res.EnsureSuccessStatusCode();

            await using var stream = await res.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<List<DealDto>>(stream, JsonOptions) ?? [];
        });

        return (cached ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d.DealId))
            .Select(d => new CheapSharkDeal(
                DealId: d.DealId!,
                StoreId: d.StoreId ?? "",
                Title: d.Title ?? "",
                SalePriceUsd: ParseMoney(d.SalePrice),
                NormalPriceUsd: ParseMoney(d.NormalPrice),
                SavingsPercent: ParseMoney(d.Savings),
                SteamAppId: d.SteamAppId ?? steamAppId
            ))
            .ToList();
    }

    public static string RedirectUrl(string dealId)
    {
        return $"https://www.cheapshark.com/redirect?dealID={Uri.EscapeDataString(dealId)}";
    }

    private static decimal? ParseMoney(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;
        if (decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var x))
            return x;
        return null;
    }

    private sealed class DealDto
    {
        [JsonPropertyName("dealID")]
        public string? DealId { get; set; }

        public string? StoreId { get; set; }
        public string? Title { get; set; }

        public string? SalePrice { get; set; }
        public string? NormalPrice { get; set; }
        public string? Savings { get; set; }

        public string? SteamAppId { get; set; }
    }
}

public sealed record CheapSharkDeal(
    string DealId,
    string StoreId,
    string Title,
    decimal? SalePriceUsd,
    decimal? NormalPriceUsd,
    decimal? SavingsPercent,
    string SteamAppId
);

