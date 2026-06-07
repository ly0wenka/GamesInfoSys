using System.Text.Json;
using System.Text.Json.Serialization;

namespace GamesInfoSys.Services;

public sealed class SteamStoreClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;

    public SteamStoreClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<SteamPriceResult?> GetAppPriceAsync(string appId, string countryCode)
    {
        var metadata = await GetAppMetadataAsync(appId, countryCode);
        if (metadata?.Price is null)
            return null;

        return new SteamPriceResult(
            Name: metadata.Name ?? $"Steam app {appId}",
            Currency: metadata.Price.Currency ?? "",
            FinalMinor: metadata.Price.Final,
            InitialMinor: metadata.Price.Initial,
            DiscountPercent: metadata.Price.DiscountPercent
        );
    }

    public async Task<SteamAppMetadata?> GetAppMetadataAsync(string appId, string countryCode = "UA")
    {
        if (string.IsNullOrWhiteSpace(appId))
            return null;
        if (!int.TryParse(appId, out _))
            return null;

        countryCode = string.IsNullOrWhiteSpace(countryCode) ? "UA" : countryCode.Trim().ToLowerInvariant();

        var url = $"api/appdetails?appids={Uri.EscapeDataString(appId)}&cc={Uri.EscapeDataString(countryCode)}&l=english";
        using var res = await _http.GetAsync(url);
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync();

        var dict = JsonSerializer.Deserialize<Dictionary<string, SteamAppDetailsEnvelope>>(json, JsonOptions);
        if (dict is null || !dict.TryGetValue(appId, out var envelope))
            return null;
        if (!envelope.Success || envelope.Data is null)
            return null;

        return new SteamAppMetadata(
            envelope.Data.Name,
            envelope.Data.HeaderImage,
            envelope.Data.PriceOverview is null
                ? null
                : new SteamPriceSnapshot(
                    envelope.Data.PriceOverview.Currency ?? "",
                    envelope.Data.PriceOverview.Final,
                    envelope.Data.PriceOverview.Initial,
                    envelope.Data.PriceOverview.DiscountPercent)
        );
    }

    private sealed class SteamAppDetailsEnvelope
    {
        public bool Success { get; set; }
        public SteamAppData? Data { get; set; }
    }

    private sealed class SteamAppData
    {
        public string? Name { get; set; }

        [JsonPropertyName("header_image")]
        public string? HeaderImage { get; set; }

        [JsonPropertyName("price_overview")]
        public SteamPriceOverview? PriceOverview { get; set; }
    }

    private sealed class SteamPriceOverview
    {
        public string? Currency { get; set; }
        public long Final { get; set; }
        public long Initial { get; set; }

        [JsonPropertyName("discount_percent")]
        public int DiscountPercent { get; set; }
    }
}

public sealed record SteamAppMetadata(
    string? Name,
    string? HeaderImage,
    SteamPriceSnapshot? Price
);

public sealed record SteamPriceSnapshot(
    string Currency,
    long Final,
    long Initial,
    int DiscountPercent
);

public sealed record SteamPriceResult(
    string Name,
    string Currency,
    long FinalMinor,
    long InitialMinor,
    int DiscountPercent
);
