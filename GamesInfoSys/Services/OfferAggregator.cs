using GamesInfoSys.Data;
using GamesInfoSys.Data.Entities;
using GamesInfoSys.Models;
using Microsoft.EntityFrameworkCore;

namespace GamesInfoSys.Services;

public sealed class OfferAggregator
{
    private readonly AppDbContext _db;
    private readonly RegionResolver _regions;
    private readonly SteamStoreClient _steam;
    private readonly CheapSharkClient _cheapShark;
    private readonly UaMarketplaceScraper _uaScraper;

    public OfferAggregator(
        AppDbContext db,
        RegionResolver regions,
        SteamStoreClient steam,
        CheapSharkClient cheapShark,
        UaMarketplaceScraper uaScraper)
    {
        _db = db;
        _regions = regions;
        _steam = steam;
        _cheapShark = cheapShark;
        _uaScraper = uaScraper;
    }

    public async Task<IReadOnlyList<StoreOffer>> GetOffersForRawgGameAsync(int rawgGameId)
    {
        var trackedId = await _db.TrackedGames
            .Where(g => g.RawgGameId == rawgGameId)
            .Select(g => (long?)g.Id)
            .FirstOrDefaultAsync();

        if (trackedId is null)
            return [];

        return await _db.StoreOffers
            .AsNoTracking()
            .Where(o => o.TrackedGameId == trackedId.Value)
            .OrderBy(o => o.Platform)
            .ThenBy(o => o.Store)
            .ToListAsync();
    }

    public async Task SyncOffersForRawgGameAsync(int rawgGameId, string? rawgName)
    {
        await SyncOffersForRawgGameAsync(rawgGameId, rawgName, null);
    }

    public async Task SyncOffersForRawgGameAsync(int rawgGameId, string? rawgName, GameDetails? details)
    {
        var tracked = await _db.TrackedGames.FirstOrDefaultAsync(g => g.RawgGameId == rawgGameId);
        if (tracked is null)
        {
            tracked = new TrackedGame
            {
                RawgGameId = rawgGameId,
                Name = rawgName
            };
            _db.TrackedGames.Add(tracked);
            await _db.SaveChangesAsync();
        }
        else if (!string.IsNullOrWhiteSpace(rawgName) && tracked.Name != rawgName)
        {
            tracked.Name = rawgName;
            await _db.SaveChangesAsync();
        }

        if (string.IsNullOrWhiteSpace(tracked.SteamAppId) && details is not null)
        {
            var inferred = TryInferSteamAppId(details);
            if (!string.IsNullOrWhiteSpace(inferred))
            {
                tracked.SteamAppId = inferred;
                await _db.SaveChangesAsync();
            }
        }

        if (!string.IsNullOrWhiteSpace(tracked.SteamAppId))
        {
            await SyncSteamAsync(tracked);
            await SyncCheapSharkAsync(tracked);
        }

        if (!string.IsNullOrWhiteSpace(tracked.Name))
        {
            await SyncUaMarketplacesAsync(tracked, platform: "Xbox", query: $"{tracked.Name} xbox");
            await SyncUaMarketplacesAsync(tracked, platform: "Switch", query: $"{tracked.Name} nintendo switch");
        }

        // TODO: add platform ingestors here (PSN/Xbox/Nintendo/Epic/GOG/etc).
    }

    public async Task SetSteamAppIdAsync(int rawgGameId, string steamAppId)
    {
        var tracked = await _db.TrackedGames.FirstOrDefaultAsync(g => g.RawgGameId == rawgGameId);
        if (tracked is null)
        {
            tracked = new TrackedGame
            {
                RawgGameId = rawgGameId,
                SteamAppId = steamAppId
            };
            _db.TrackedGames.Add(tracked);
        }
        else
        {
            tracked.SteamAppId = steamAppId;
        }

        await _db.SaveChangesAsync();
    }

    private async Task SyncSteamAsync(TrackedGame game)
    {
        var region = _regions.ForPlatform(GamePlatform.Pc);
        var result = await _steam.GetAppPriceAsync(game.SteamAppId!, region);
        if (result is null)
            return;

        var now = DateTime.UtcNow;

        var existing = await _db.StoreOffers.FirstOrDefaultAsync(o =>
            o.Store == "Steam" &&
            o.ExternalId == game.SteamAppId &&
            o.Region == region);

        if (existing is null)
        {
            existing = new StoreOffer
            {
                TrackedGameId = game.Id,
                Store = "Steam",
                Platform = "PC",
                Region = region,
                ExternalId = game.SteamAppId!,
                Title = result.Name,
                Url = $"https://store.steampowered.com/app/{game.SteamAppId}/",
                Currency = result.Currency,
                PriceMinor = result.FinalMinor,
                OriginalPriceMinor = result.InitialMinor,
                LastSeenUtc = now,
                LastUpdatedUtc = now
            };
            _db.StoreOffers.Add(existing);
            await _db.SaveChangesAsync();
        }
        else
        {
            existing.Title = result.Name;
            existing.Url = $"https://store.steampowered.com/app/{game.SteamAppId}/";
            existing.Currency = result.Currency;
            existing.PriceMinor = result.FinalMinor;
            existing.OriginalPriceMinor = result.InitialMinor;
            existing.LastSeenUtc = now;
            existing.LastUpdatedUtc = now;
            await _db.SaveChangesAsync();
        }

        if (!string.IsNullOrWhiteSpace(result.Currency) && result.FinalMinor > 0)
        {
            _db.OfferPricePoints.Add(new OfferPricePoint
            {
                StoreOfferId = existing.Id,
                AtUtc = now,
                Currency = result.Currency,
                PriceMinor = result.FinalMinor,
                OriginalPriceMinor = result.InitialMinor
            });
            await _db.SaveChangesAsync();
        }
    }

    private async Task SyncCheapSharkAsync(TrackedGame game)
    {
        var deals = await _cheapShark.GetDealsBySteamAppIdAsync(game.SteamAppId!, pageSize: 20, onSaleOnly: false);
        if (deals.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var region = "GLOBAL";

        foreach (var d in deals)
        {
            var externalId = d.DealId;
            var existing = await _db.StoreOffers.FirstOrDefaultAsync(o =>
                o.Store == "CheapShark" &&
                o.ExternalId == externalId &&
                o.Region == region);

            var priceMinor = d.SalePriceUsd is null ? (long?)null : (long)Math.Round(d.SalePriceUsd.Value * 100m, MidpointRounding.AwayFromZero);
            var originalMinor = d.NormalPriceUsd is null ? (long?)null : (long)Math.Round(d.NormalPriceUsd.Value * 100m, MidpointRounding.AwayFromZero);

            var title = string.IsNullOrWhiteSpace(d.Title) ? (game.Name ?? "Deal") : d.Title;
            var url = CheapSharkClient.RedirectUrl(d.DealId);

            if (existing is null)
            {
                existing = new StoreOffer
                {
                    TrackedGameId = game.Id,
                    Store = "CheapShark",
                    Platform = "PC",
                    Region = region,
                    ExternalId = externalId,
                    Title = title,
                    Url = url,
                    Currency = "USD",
                    PriceMinor = priceMinor,
                    OriginalPriceMinor = originalMinor,
                    LastSeenUtc = now,
                    LastUpdatedUtc = now
                };
                _db.StoreOffers.Add(existing);
            }
            else
            {
                existing.Title = title;
                existing.Url = url;
                existing.Currency = "USD";
                existing.PriceMinor = priceMinor;
                existing.OriginalPriceMinor = originalMinor;
                existing.LastSeenUtc = now;
                existing.LastUpdatedUtc = now;
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task SyncUaMarketplacesAsync(TrackedGame game, string platform, string query)
    {
        if (!_uaScraper.Enabled)
            return;

        var offers = new List<UaMarketOffer>();
        offers.AddRange(await _uaScraper.SearchAsync("prom", platform, query));
        offers.AddRange(await _uaScraper.SearchAsync("olx", platform, query));

        var now = DateTime.UtcNow;
        foreach (var o in offers)
        {
            var externalId = $"{o.Store}:{o.Url}".ToLowerInvariant();
            var existing = await _db.StoreOffers.FirstOrDefaultAsync(x =>
                x.Store == o.Store &&
                x.ExternalId == externalId &&
                x.Region == "UA");

            if (existing is null)
            {
                existing = new StoreOffer
                {
                    TrackedGameId = game.Id,
                    Store = o.Store,
                    Platform = platform,
                    Region = "UA",
                    ExternalId = externalId,
                    Title = o.Title,
                    Url = o.Url,
                    Currency = o.Currency,
                    PriceMinor = o.PriceMinor,
                    OriginalPriceMinor = null,
                    LastSeenUtc = now,
                    LastUpdatedUtc = now
                };
                _db.StoreOffers.Add(existing);
            }
            else
            {
                existing.Title = o.Title;
                existing.Url = o.Url;
                existing.Currency = o.Currency;
                existing.PriceMinor = o.PriceMinor;
                existing.LastSeenUtc = now;
                existing.LastUpdatedUtc = now;
            }
        }

        await _db.SaveChangesAsync();
    }

    private static string? TryInferSteamAppId(GameDetails details)
    {
        // RAWG often provides store links like https://store.steampowered.com/app/1245620/...
        foreach (var link in details.ExternalStores)
        {
            if (!link.Url.Contains("store.steampowered.com", StringComparison.OrdinalIgnoreCase))
                continue;

            var uriOk = Uri.TryCreate(link.Url, UriKind.Absolute, out var uri);
            if (!uriOk || uri is null)
                continue;

            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            // Expect: app/{appid}/...
            for (var i = 0; i < segments.Length - 1; i++)
            {
                if (!segments[i].Equals("app", StringComparison.OrdinalIgnoreCase))
                    continue;
                var id = segments[i + 1];
                if (int.TryParse(id, out _))
                    return id;
            }
        }

        return null;
    }
}
