using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Globalization;
using GamesInfoSys.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GamesInfoSys.Services;

public sealed class RawgClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly RawgOptions _options;
    private readonly IWebHostEnvironment _env;
    private readonly SteamStoreClient _steamStore;

    public RawgClient(
        HttpClient http,
        IMemoryCache cache,
        IOptions<RawgOptions> options,
        IWebHostEnvironment env,
        SteamStoreClient steamStore)
    {
        _http = http;
        _cache = cache;
        _options = options.Value;
        _env = env;
        _steamStore = steamStore;
    }

    public bool IsDemoMode => string.IsNullOrWhiteSpace(_options.ApiKey) && _options.UseDemoDataWhenNoApiKey;

    public async Task<IReadOnlyList<GameSummary>> SearchGamesAsync(
        string? query,
        int page = 1,
        int pageSize = 24,
        string? ordering = "-rating")
    {
        if (IsDemoMode)
            return await SearchDemoAsync(query);

        page = Math.Clamp(page, 1, 1000);
        pageSize = Math.Clamp(pageSize, 1, 40);
        ordering ??= "-rating";

        var cacheKey = $"rawg:search:q={query}|p={page}|ps={pageSize}|o={ordering}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

            var url = new UriBuilder(new Uri(_http.BaseAddress!, "games"));
            url.Query = BuildQuery(new Dictionary<string, string?>
            {
                ["key"] = _options.ApiKey,
                ["search"] = string.IsNullOrWhiteSpace(query) ? null : query,
                ["page"] = page.ToString(),
                ["page_size"] = pageSize.ToString(),
                ["ordering"] = ordering
            });

            using var res = await _http.GetAsync(url.Uri);
            res.EnsureSuccessStatusCode();

            await using var stream = await res.Content.ReadAsStreamAsync();
            var payload = await JsonSerializer.DeserializeAsync<RawgListResponse<RawgGameSummary>>(stream, JsonOptions);
            return (IReadOnlyList<GameSummary>)(payload?.Results ?? [])
                .Select(MapSummary)
                .ToList();
        }) ?? [];
    }

    public async Task<GameDetails?> GetGameAsync(int id)
    {
        if (IsDemoMode)
            return await GetDemoDetailsAsync(id);

        var cacheKey = $"rawg:game:id={id}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

            var url = new UriBuilder(new Uri(_http.BaseAddress!, $"games/{id}"));
            url.Query = BuildQuery(new Dictionary<string, string?>
            {
                ["key"] = _options.ApiKey
            });

            using var res = await _http.GetAsync(url.Uri);
            if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            res.EnsureSuccessStatusCode();

            await using var stream = await res.Content.ReadAsStreamAsync();
            var raw = await JsonSerializer.DeserializeAsync<RawgGameDetails>(stream, JsonOptions);
            if (raw is null)
                return null;

            return MapDetails(raw);
        });
    }

    public async Task<IReadOnlyList<GameScreenshot>> GetScreenshotsAsync(int id)
    {
        if (IsDemoMode)
            return [];

        var cacheKey = $"rawg:screens:id={id}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

            var url = new UriBuilder(new Uri(_http.BaseAddress!, $"games/{id}/screenshots"));
            url.Query = BuildQuery(new Dictionary<string, string?>
            {
                ["key"] = _options.ApiKey,
                ["page_size"] = "8"
            });

            using var res = await _http.GetAsync(url.Uri);
            res.EnsureSuccessStatusCode();

            await using var stream = await res.Content.ReadAsStreamAsync();
            var payload = await JsonSerializer.DeserializeAsync<RawgListResponse<RawgScreenshot>>(stream, JsonOptions);
            return (IReadOnlyList<GameScreenshot>)(payload?.Results ?? [])
                .Select(s => new GameScreenshot(s.Image))
                .ToList();
        }) ?? [];
    }

    private static string BuildQuery(Dictionary<string, string?> values)
    {
        var encoded = values
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}");
        return string.Join("&", encoded);
    }

    private static GameSummary MapSummary(RawgGameSummary raw)
    {
        return new GameSummary(
            raw.Id,
            raw.Name ?? "(Unknown)",
            raw.Released,
            raw.Rating,
            raw.RatingsCount,
            raw.BackgroundImage,
            (raw.Genres ?? []).Select(g => g.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList(),
            (raw.Platforms ?? []).Select(p => p.Platform?.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Cast<string>().Distinct().ToList()
        );
    }

    private static GameDetails MapDetails(RawgGameDetails raw)
    {
        var description = LocalizeDescription(
            StripHtmlToPlainText(raw.Description),
            null);

        return new GameDetails(
            raw.Id,
            raw.Name ?? "(Unknown)",
            raw.Released,
            raw.Rating,
            raw.RatingsCount,
            raw.Metacritic,
            raw.BackgroundImage,
            raw.Website,
            description,
            (raw.Genres ?? []).Select(g => g.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList(),
            (raw.Platforms ?? []).Select(p => p.Platform?.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Cast<string>().Distinct().ToList(),
            (raw.Developers ?? []).Select(d => d.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList(),
            (raw.Publishers ?? []).Select(p => p.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList(),
            (raw.Tags ?? []).Select(t => t.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList(),
            (raw.Stores ?? [])
                .Select(s => new ExternalStoreLink(s.Store?.Name ?? "", s.Url ?? ""))
                .Where(x => !string.IsNullOrWhiteSpace(x.Store) && !string.IsNullOrWhiteSpace(x.Url))
                .Distinct()
                .ToList()
        );
    }

    private static string? StripHtmlToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, "\\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private async Task<IReadOnlyList<GameSummary>> SearchDemoAsync(string? query)
    {
        var games = await LoadDemoAsync();
        if (string.IsNullOrWhiteSpace(query))
            return games;

        query = query.Trim();
        return games
            .Where(g => g.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task<GameDetails?> GetDemoDetailsAsync(int id)
    {
        var games = await LoadDemoAsync();
        var game = games.FirstOrDefault(g => g.Id == id);
        if (game is null)
            return null;

        var demo = await LoadDemoRawAsync();
        var item = demo.FirstOrDefault(x => x.Id == id);

        var external = new List<ExternalStoreLink>();
        AddExternal(external, "Steam", item?.SteamUrl);
        AddExternal(external, "PlayStation", item?.PsnUrl);
        AddExternal(external, "Xbox", item?.XboxUrl);
        AddExternal(external, "Nintendo", item?.NintendoUrl);
        AddExternal(external, "Epic Games", item?.EpicUrl);
        AddExternal(external, "GOG", item?.GogUrl);

        return new GameDetails(
            game.Id,
            game.Name,
            game.Released,
            game.Rating,
            game.RatingsCount,
            null,
            await ResolveDemoBackgroundImageAsync(item, game.BackgroundImage),
            null,
            GetDemoDescription(item),
            game.Genres,
            game.Platforms,
            [],
            [],
            [],
            external
        );
    }

    private async Task<IReadOnlyList<GameSummary>> LoadDemoAsync()
    {
        return await _cache.GetOrCreateAsync("demo:games", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);

            var path = Path.Combine(_env.ContentRootPath, "Data", "demo-games.json");
            if (!File.Exists(path))
                return (IReadOnlyList<GameSummary>)[];

            var json = await File.ReadAllTextAsync(path);
            var payload = JsonSerializer.Deserialize<List<DemoGame>>(json, JsonOptions) ?? [];
            var games = await Task.WhenAll(payload.Select(async g => new GameSummary(
                    g.Id,
                    g.Name ?? "(Unknown)",
                    g.Released,
                    g.Rating,
                    g.RatingsCount,
                    await ResolveDemoBackgroundImageAsync(g, g.BackgroundImage),
                    g.Genres ?? [],
                    g.Platforms ?? []
                )));
            return games.ToList();
        }) ?? [];
    }

    private async Task<IReadOnlyList<DemoGame>> LoadDemoRawAsync()
    {
        return await _cache.GetOrCreateAsync("demo:games:raw", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);

            var path = Path.Combine(_env.ContentRootPath, "Data", "demo-games.json");
            if (!File.Exists(path))
                return (IReadOnlyList<DemoGame>)[];

            var json = await File.ReadAllTextAsync(path);
            return (IReadOnlyList<DemoGame>)(JsonSerializer.Deserialize<List<DemoGame>>(json, JsonOptions) ?? []);
        }) ?? [];
    }

    private sealed class RawgListResponse<T>
    {
        public List<T>? Results { get; set; }
    }

    private sealed class RawgName
    {
        public string Name { get; set; } = "";
    }

    private sealed class RawgPlatformWrap
    {
        public RawgName? Platform { get; set; }
    }

    private sealed class RawgGameSummary
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Released { get; set; }
        public double? Rating { get; set; }
        public int? RatingsCount { get; set; }

        [JsonPropertyName("background_image")]
        public string? BackgroundImage { get; set; }

        public List<RawgName>? Genres { get; set; }
        public List<RawgPlatformWrap>? Platforms { get; set; }
    }

    private sealed class RawgGameDetails
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Released { get; set; }
        public double? Rating { get; set; }
        public int? RatingsCount { get; set; }
        public int? Metacritic { get; set; }
        public string? Website { get; set; }
        public string? Description { get; set; }

        [JsonPropertyName("background_image")]
        public string? BackgroundImage { get; set; }

        public List<RawgName>? Genres { get; set; }
        public List<RawgPlatformWrap>? Platforms { get; set; }
        public List<RawgName>? Developers { get; set; }
        public List<RawgName>? Publishers { get; set; }
        public List<RawgName>? Tags { get; set; }

        public List<RawgStoreLink>? Stores { get; set; }
    }

    private sealed class RawgStoreLink
    {
        public RawgName? Store { get; set; }
        public string? Url { get; set; }
    }

    private sealed class RawgScreenshot
    {
        public string? Image { get; set; }
    }

    private sealed class DemoGame
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Released { get; set; }
        public double? Rating { get; set; }
        public int? RatingsCount { get; set; }
        public string? Description { get; set; }
        public string? DescriptionUk { get; set; }
        public string? BackgroundImage { get; set; }
        public List<string>? Genres { get; set; }
        public List<string>? Platforms { get; set; }

        public string? SteamUrl { get; set; }
        public string? PsnUrl { get; set; }
        public string? XboxUrl { get; set; }
        public string? NintendoUrl { get; set; }
        public string? EpicUrl { get; set; }
        public string? GogUrl { get; set; }
    }

    private static void AddExternal(List<ExternalStoreLink> list, string store, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;
        list.Add(new ExternalStoreLink(store, url.Trim()));
    }

    private static string GetDemoDescription(DemoGame? game)
    {
        return LocalizeDescription(game?.Description, game?.DescriptionUk)
            ?? "Demo mode: set Rawg:ApiKey in appsettings.json or via environment variable RAWG__APIKEY to fetch live data.";
    }

    private static string? LocalizeDescription(string? description, string? descriptionUk)
    {
        var isUkrainian = string.Equals(
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
            "uk",
            StringComparison.OrdinalIgnoreCase);

        if (isUkrainian)
        {
            if (!string.IsNullOrWhiteSpace(descriptionUk))
                return descriptionUk;

            if (!string.IsNullOrWhiteSpace(description))
                return AutoTranslateDescriptionToUkrainian(description);
        }

        return string.IsNullOrWhiteSpace(description) ? null : description;
    }

    private static string AutoTranslateDescriptionToUkrainian(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var translated = text.Trim();

        translated = Regex.Replace(translated, @"\b[Ee]xplore\b", "Досліджуйте");
        translated = Regex.Replace(translated, @"\b[Yy]ou explore\b", "Гравець досліджує");
        translated = Regex.Replace(translated, @"\b[Yy]ou fight\b", "Гравець бореться");
        translated = Regex.Replace(translated, @"\b[Yy]ou play as\b", "Гравець керує");
        translated = Regex.Replace(translated, @"\b[Yy]ou control\b", "Гравець керує");
        translated = Regex.Replace(translated, @"\b[Yy]ou build\b", "Гравець розбудовує");
        translated = Regex.Replace(translated, @"\b[Yy]ou can\b", "Можна");

        var replacements = new (string Source, string Target)[]
        {
            (" is an open-world action RPG", " — це рольова екшен-гра з відкритим світом"),
            (" is an open world action RPG", " — це рольова екшен-гра з відкритим світом"),
            (" is an action RPG", " — це рольова екшен-гра"),
            (" is an open-world RPG", " — це рольова гра з відкритим світом"),
            (" is an RPG", " — це рольова гра"),
            (" is an open-world action game", " — це екшен-гра з відкритим світом"),
            (" is an action-adventure game", " — це пригодницька екшен-гра"),
            (" is an action game", " — це екшен-гра"),
            (" is a dark-fantasy action RPG", " — це рольова екшен-гра у жанрі темного фентезі"),
            (" by FromSoftware", " від FromSoftware"),
            (" set in ", ", події якої відбуваються у "),
            (" set on ", ", події якої відбуваються на "),
            (" set across ", ", події якої розгортаються у "),
            (" fight challenging bosses", " перемагає складних босів"),
            (" battle challenging bosses", " б'ється зі складними босами"),
            (" develop your build", " розвиває свій стиль гри"),
            (" through weapons, magic and attributes", " через зброю, магію та характеристики"),
            (" through weapons, magic, and attributes", " через зброю, магію та характеристики"),
            (" uncover the shattered history", " розкриває зруйновану історію"),
            (" on the way to becoming ", " на шляху до того, щоб стати "),
            (" large dark-fantasy world", " великий світ темного фентезі"),
            (" dark-fantasy world", " світ темного фентезі"),
            (" open-world", "з відкритим світом"),
            (" open world", " відкритий світ"),
            (" action RPG", " рольова екшен-гра"),
            (" action-adventure", " пригодницький екшен"),
            (" bosses", " босів"),
            (" boss", " боса"),
            (" weapons", " зброю"),
            (" weapon", " зброю"),
            (" magic", " магію"),
            (" attributes", " характеристики"),
            (" story", " історію"),
            (" world", " світ"),
            (" journey", " подорож"),
            (" kingdom", " королівство")
        };

        foreach (var (source, target) in replacements)
            translated = translated.Replace(source, target, StringComparison.OrdinalIgnoreCase);

        translated = Regex.Replace(translated, @"\s+", " ").Trim();
        return translated;
    }

    private async Task<string?> ResolveDemoBackgroundImageAsync(DemoGame? game, string? currentImage)
    {
        if (!string.IsNullOrWhiteSpace(currentImage))
            return currentImage;

        var appId = SteamAppIdParser.TryParse(game?.SteamUrl);
        if (appId is null)
            return null;

        return await _cache.GetOrCreateAsync($"steam:header:{appId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
            var metadata = await _steamStore.GetAppMetadataAsync(appId);
            return string.IsNullOrWhiteSpace(metadata?.HeaderImage) ? null : metadata.HeaderImage;
        });
    }
}
