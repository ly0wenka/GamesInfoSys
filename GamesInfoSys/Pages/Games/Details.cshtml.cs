using GamesInfoSys.Models;
using GamesInfoSys.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GamesInfoSys.Pages.Games;

public sealed class DetailsModel : PageModel
{
    private readonly RawgClient _rawg;

    public DetailsModel(RawgClient rawg)
    {
        _rawg = rawg;
    }

    public bool IsDemoMode => _rawg.IsDemoMode;

    public GameDetails? Game { get; private set; }
    public IReadOnlyList<GameScreenshot> Screenshots { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Game = await _rawg.GetGameAsync(id);
        if (Game is null)
            return NotFound();

        Screenshots = await _rawg.GetScreenshotsAsync(id);
        return Page();
    }
}

