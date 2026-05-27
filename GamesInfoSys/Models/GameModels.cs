namespace GamesInfoSys.Models;

public sealed record GameSummary(
    int Id,
    string Name,
    string? Released,
    double? Rating,
    int? RatingsCount,
    string? BackgroundImage,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Platforms
);

public sealed record GameDetails(
    int Id,
    string Name,
    string? Released,
    double? Rating,
    int? RatingsCount,
    int? Metacritic,
    string? BackgroundImage,
    string? Website,
    string? DescriptionPlain,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Platforms,
    IReadOnlyList<string> Developers,
    IReadOnlyList<string> Publishers,
    IReadOnlyList<string> Tags
);

public sealed record GameScreenshot(string? Image);

