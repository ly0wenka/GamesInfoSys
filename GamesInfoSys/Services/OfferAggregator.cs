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

    public OfferAggregator(AppDbContext db, RegionResolver regions, SteamStoreClient steam)
    {
        _db = db;
        _regions = regions;
        _steam = steam;
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
