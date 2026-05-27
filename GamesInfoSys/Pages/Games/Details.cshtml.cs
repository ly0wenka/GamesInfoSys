using GamesInfoSys.Models;
using GamesInfoSys.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GamesInfoSys.Pages.Games;

public sealed class DetailsModel : PageModel
{
    private readonly RawgClient _rawg;
    private readonly OfferAggregator _offers;
    private readonly CurrencyConverter _fx;

    public DetailsModel(RawgClient rawg, OfferAggregator offers, CurrencyConverter fx)
    {
        _rawg = rawg;
        _offers = offers;
        _fx = fx;
    }

    public bool IsDemoMode => _rawg.IsDemoMode;

    public GameDetails? Game { get; private set; }
    public IReadOnlyList<GameScreenshot> Screenshots { get; private set; } = [];
    public IReadOnlyList<Data.Entities.StoreOffer> Offers { get; private set; } = [];
    public Dictionary<long, decimal?> OfferUahMajor { get; private set; } = new();
    public string GameNameForSearch => Game?.Name ?? "";

    [BindProperty(SupportsGet = false)]
    public string? SteamAppIdOrUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Game = await _rawg.GetGameAsync(id);
        if (Game is null)
            return NotFound();

        await _offers.SyncOffersForRawgGameAsync(id, Game.Name, Game);
        Offers = await _offers.GetOffersForRawgGameAsync(id);
        OfferUahMajor = await ComputeUahAsync(Offers);

        Screenshots = await _rawg.GetScreenshotsAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostSetSteamAsync(int id)
    {
        Game = await _rawg.GetGameAsync(id);
        if (Game is null)
            return NotFound();

        var parsed = TryParseSteamAppId(SteamAppIdOrUrl);
        if (string.IsNullOrWhiteSpace(parsed))
        {
            ModelState.AddModelError(nameof(SteamAppIdOrUrl), "Paste a Steam app link or an App ID.");
            Offers = await _offers.GetOffersForRawgGameAsync(id);
            return Page();
        }

        await _offers.SetSteamAppIdAsync(id, parsed);
        await _offers.SyncOffersForRawgGameAsync(id, Game.Name);

        return RedirectToPage("/Games/Details", new { id });
    }

    private async Task<Dictionary<long, decimal?>> ComputeUahAsync(IReadOnlyList<Data.Entities.StoreOffer> offers)
    {
        var dict = new Dictionary<long, decimal?>();
        foreach (var o in offers)
        {
            if (o.PriceMinor is null || string.IsNullOrWhiteSpace(o.Currency))
            {
                dict[o.Id] = null;
                continue;
            }

            var major = o.PriceMinor.Value / 100m;
            dict[o.Id] = await _fx.ToUahAsync(major, o.Currency);
        }
        return dict;
    }

    private static string? TryParseSteamAppId(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        input = input.Trim();
        if (int.TryParse(input, out _))
            return input;

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || uri is null)
            return null;

        if (!uri.Host.Contains("steampowered.com", StringComparison.OrdinalIgnoreCase))
            return null;

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (!segments[i].Equals("app", StringComparison.OrdinalIgnoreCase))
                continue;
            var id = segments[i + 1];
            return int.TryParse(id, out _) ? id : null;
        }

        return null;
    }
}
