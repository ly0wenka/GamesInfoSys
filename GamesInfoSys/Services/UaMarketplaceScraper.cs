using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GamesInfoSys.Services;

public sealed record UaMarketOffer(
    string Store,
    string Platform,
    string Title,
    string Url,
    string Currency,
    long PriceMinor
);

public sealed class UaMarketplaceScraper
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ScrapingOptions _options;

    public UaMarketplaceScraper(HttpClient http, IMemoryCache cache, IOptions<ScrapingOptions> options)
    {
        _http = http;
        _cache = cache;
        _options = options.Value;
    }

    public bool Enabled => _options.Enabled;

    public async Task<IReadOnlyList<UaMarketOffer>> SearchAsync(string store, string platform, string query)
    {
        if (!Enabled)
            return [];
        if (string.IsNullOrWhiteSpace(store) || string.IsNullOrWhiteSpace(platform) || string.IsNullOrWhiteSpace(query))
            return [];

        store = store.Trim().ToLowerInvariant();
        platform = platform.Trim();
        query = query.Trim();

        var cacheKey = $"ua:scrape:{store}:{platform}:{query}";
        var minutes = Math.Clamp(_options.CacheMinutes, 1, 24 * 60);

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(minutes);

            return store switch
            {
                "prom" => await SearchPromAsync(platform, query),
                "olx" => await SearchOlxAsync(platform, query),
                // Rozetka search is often Cloudflare-protected; keep disabled unless you add a real browser worker.
                _ => []
            };
        }) ?? [];
    }

    private async Task<IReadOnlyList<UaMarketOffer>> SearchPromAsync(string platform, string query)
    {
        var url = $"https://prom.ua/ua/search?search_term={Uri.EscapeDataString(query)}";
        var html = await _http.GetStringAsync(url);

        var stateJson = ExtractJsonAssignment(html, "window.__STATE__");
        if (stateJson is null)
            return [];

        using var doc = JsonDocument.Parse(stateJson);
        var found = new List<UaMarketOffer>();
        ExtractOffersFromUnknownJson(
            root: doc.RootElement,
            acceptUrl: u => u.Contains("prom.ua", StringComparison.OrdinalIgnoreCase),
            normalizeUrl: u => u.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? u : $"https://prom.ua{u}",
            storeName: "Prom.ua",
            platform: platform,
            found: found
        );

        return found
            .GroupBy(o => o.Url, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(o => o.PriceMinor)
            .Take(Math.Clamp(_options.MaxResultsPerStore, 1, 30))
            .ToList();
    }

    private async Task<IReadOnlyList<UaMarketOffer>> SearchOlxAsync(string platform, string query)
    {
        var slug = SlugForOlx(query);
        var url = $"https://www.olx.ua/uk/list/q-{slug}/";
        var html = await _http.GetStringAsync(url);

        var stateJson = ExtractJsonAssignment(html, "window.__PRERENDERED_STATE__");
        if (stateJson is null)
            return [];

        using var doc = JsonDocument.Parse(stateJson);
        var found = new List<UaMarketOffer>();
        ExtractOffersFromUnknownJson(
            root: doc.RootElement,
            acceptUrl: u => u.Contains("olx.ua", StringComparison.OrdinalIgnoreCase),
            normalizeUrl: u =>
            {
                if (u.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    return u;
                if (!u.StartsWith("/"))
                    u = "/" + u;
                return $"https://www.olx.ua{u}";
            },
            storeName: "OLX",
            platform: platform,
            found: found
        );

        return found
            .GroupBy(o => o.Url, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(o => o.PriceMinor)
            .Take(Math.Clamp(_options.MaxResultsPerStore, 1, 30))
            .ToList();
    }

    private static string? ExtractJsonAssignment(string html, string varName)
    {
        // Matches: window.__STATE__ = {...};
        var pattern = Regex.Escape(varName) + @"\s*=\s*";
        var start = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
        if (!start.Success)
            return null;

        var idx = start.Index + start.Length;
        // Find JSON object/array start
        while (idx < html.Length && char.IsWhiteSpace(html[idx])) idx++;
        if (idx >= html.Length)
            return null;

        var open = html[idx];
        if (open != '{' && open != '[')
            return null;

        var endIdx = FindJsonEnd(html, idx);
        if (endIdx < 0)
            return null;

        return html.Substring(idx, endIdx - idx + 1);
    }

    private static int FindJsonEnd(string s, int startIdx)
    {
        var depth = 0;
        var inString = false;
        var escape = false;

        for (var i = startIdx; i < s.Length; i++)
        {
            var c = s[i];
            if (inString)
            {
                if (escape)
                {
                    escape = false;
                    continue;
                }
                if (c == '\\')
                {
                    escape = true;
                    continue;
                }
                if (c == '"')
                    inString = false;
                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '{' || c == '[') depth++;
            if (c == '}' || c == ']') depth--;

            if (depth == 0)
                return i;
        }

        return -1;
    }

    private static void ExtractOffersFromUnknownJson(
        JsonElement root,
        Func<string, bool> acceptUrl,
        Func<string, string> normalizeUrl,
        string storeName,
        string platform,
        List<UaMarketOffer> found)
    {
        var stack = new Stack<JsonElement>();
        stack.Push(root);

        while (stack.Count > 0 && found.Count < 2000)
        {
            var el = stack.Pop();

            if (el.ValueKind == JsonValueKind.Object)
            {
                string? title = null;
                string? url = null;
                long? priceMinor = null;

                foreach (var prop in el.EnumerateObject())
                {
                    if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                        stack.Push(prop.Value);

                    if (title is null && prop.NameEquals("name") && prop.Value.ValueKind == JsonValueKind.String)
                        title = prop.Value.GetString();
                    if (title is null && prop.NameEquals("title") && prop.Value.ValueKind == JsonValueKind.String)
                        title = prop.Value.GetString();

                    if (url is null && (prop.NameEquals("url") || prop.NameEquals("href")) && prop.Value.ValueKind == JsonValueKind.String)
                        url = prop.Value.GetString();

                    if (priceMinor is null && prop.NameEquals("price"))
                    {
                        priceMinor = TryParsePriceMinor(prop.Value);
                    }
                    if (priceMinor is null && prop.NameEquals("price_uah"))
                    {
                        priceMinor = TryParsePriceMinor(prop.Value);
                    }
                }

                if (!string.IsNullOrWhiteSpace(title) &&
                    !string.IsNullOrWhiteSpace(url) &&
                    acceptUrl(url!) &&
                    priceMinor is not null &&
                    priceMinor.Value > 0)
                {
                    found.Add(new UaMarketOffer(
                        Store: storeName,
                        Platform: platform,
                        Title: title!.Trim(),
                        Url: normalizeUrl(url!),
                        Currency: "UAH",
                        PriceMinor: priceMinor.Value
                    ));
                }
            }
            else if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var x in el.EnumerateArray())
                    stack.Push(x);
            }
        }
    }

    private static long? TryParsePriceMinor(JsonElement el)
    {
        // Accept: number (major), string like "1 999 ₴", object containing amount/value
        switch (el.ValueKind)
        {
            case JsonValueKind.Number:
                if (el.TryGetDecimal(out var dec))
                    return (long)Math.Round(dec * 100m, MidpointRounding.AwayFromZero);
                return null;
            case JsonValueKind.String:
                return ParseUahStringToMinor(el.GetString());
            case JsonValueKind.Object:
                {
                    if (el.TryGetProperty("value", out var v) || el.TryGetProperty("amount", out v))
                    {
                        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var dec2))
                            return (long)Math.Round(dec2 * 100m, MidpointRounding.AwayFromZero);
                        if (v.ValueKind == JsonValueKind.String)
                            return ParseUahStringToMinor(v.GetString());
                    }
                    if (el.TryGetProperty("uah", out var u) && u.ValueKind == JsonValueKind.String)
                        return ParseUahStringToMinor(u.GetString());
                    return null;
                }
            default:
                return null;
        }
    }

    private static long? ParseUahStringToMinor(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;

        s = s.Replace("\u00A0", " ");
        // Extract digits and optional decimal part.
        var m = Regex.Match(s, @"(\d[\d\s]*)([.,](\d{1,2}))?");
        if (!m.Success)
            return null;

        var whole = m.Groups[1].Value.Replace(" ", "");
        var frac = m.Groups[3].Success ? m.Groups[3].Value : "0";
        if (!long.TryParse(whole, out var hryvnia))
            return null;

        var kop = 0L;
        if (frac.Length == 1) frac += "0";
        if (frac.Length >= 2 && long.TryParse(frac.Substring(0, 2), out var kopParsed))
            kop = kopParsed;

        return hryvnia * 100 + kop;
    }

    private static string SlugForOlx(string query)
    {
        query = query.Trim().ToLowerInvariant();
        query = query.Replace('’', '\'').Replace('–', '-').Replace('—', '-');
        query = Regex.Replace(query, "\\s+", "-");
        query = Regex.Replace(query, "[^a-z0-9\\-]+", "");
        return query;
    }
}

