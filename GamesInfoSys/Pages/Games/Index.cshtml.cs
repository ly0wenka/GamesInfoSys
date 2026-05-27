using GamesInfoSys.Models;
using GamesInfoSys.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GamesInfoSys.Pages.Games;

public sealed class IndexModel : PageModel
{
    private readonly RawgClient _rawg;

    public IndexModel(RawgClient rawg)
    {
        _rawg = rawg;
    }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Query { get; set; }

    public bool IsDemoMode => _rawg.IsDemoMode;

    public IReadOnlyList<GameSummary> Games { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Games = await _rawg.SearchGamesAsync(Query);
    }
}

