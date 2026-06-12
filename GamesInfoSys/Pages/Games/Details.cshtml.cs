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
    private readonly UiText _text;

    public DetailsModel(RawgClient rawg, OfferAggregator offers, CurrencyConverter fx, UiText text)
    {
        _rawg = rawg;
        _offers = offers;
        _fx = fx;
        _text = text;
    }

    public bool IsDemoMode => _rawg.IsDemoMode;

    public GameDetails? Game { get; private set; }
    public IReadOnlyList<GameScreenshot> Screenshots { get; private set; } = [];
    public IReadOnlyList<Data.Entities.StoreOffer> Offers { get; private set; } = [];
    public Dictionary<long, decimal?> OfferUahMajor { get; private set; } = new();
    public string GameNameForSearch => Game?.Name ?? "";
    public Data.Entities.StoreOffer? BestMarketplaceOffer { get; private set; }

    [BindProperty(SupportsGet = false)]
    public string? SteamAppIdOrUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Game = await _rawg.GetGameAsync(id);
        if (Game is null)
            return NotFound();

        await _offers.SyncOffersForRawgGameAsync(id, Game.Name, Game);
        Offers = await _offers.GetOffersForRawgGameAsync(id);
        BestMarketplaceOffer = Offers
            .Where(o => o.Store != "Steam" && o.PriceMinor is not null)
            .OrderBy(o => o.PriceMinor)
            .FirstOrDefault();
        OfferUahMajor = await ComputeUahAsync(Offers);

        Screenshots = await _rawg.GetScreenshotsAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostSetSteamAsync(int id)
    {
        Game = await _rawg.GetGameAsync(id);
        if (Game is null)
            return NotFound();

        var parsed = SteamAppIdParser.TryParse(SteamAppIdOrUrl);
        if (string.IsNullOrWhiteSpace(parsed))
        {
            ModelState.AddModelError(nameof(SteamAppIdOrUrl), _text["SteamValidation"]);
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
}
