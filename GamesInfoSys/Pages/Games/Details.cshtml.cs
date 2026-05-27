using GamesInfoSys.Models;
using GamesInfoSys.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GamesInfoSys.Pages.Games;

public sealed class DetailsModel : PageModel
{
    private readonly RawgClient _rawg;
    private readonly OfferAggregator _offers;

    public DetailsModel(RawgClient rawg, OfferAggregator offers)
    {
        _rawg = rawg;
        _offers = offers;
    }

    public bool IsDemoMode => _rawg.IsDemoMode;

    public GameDetails? Game { get; private set; }
    public IReadOnlyList<GameScreenshot> Screenshots { get; private set; } = [];
    public IReadOnlyList<Data.Entities.StoreOffer> Offers { get; private set; } = [];

    [BindProperty(SupportsGet = false)]
    public string? SteamAppId { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Game = await _rawg.GetGameAsync(id);
        if (Game is null)
            return NotFound();

        await _offers.SyncOffersForRawgGameAsync(id, Game.Name, Game);
        Offers = await _offers.GetOffersForRawgGameAsync(id);

        Screenshots = await _rawg.GetScreenshotsAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostSetSteamAsync(int id)
    {
        Game = await _rawg.GetGameAsync(id);
        if (Game is null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(SteamAppId))
        {
            ModelState.AddModelError(nameof(SteamAppId), "Steam App ID is required.");
            Offers = await _offers.GetOffersForRawgGameAsync(id);
            return Page();
        }

        await _offers.SetSteamAppIdAsync(id, SteamAppId.Trim());
        await _offers.SyncOffersForRawgGameAsync(id, Game.Name);

        return RedirectToPage("/Games/Details", new { id });
    }
}
