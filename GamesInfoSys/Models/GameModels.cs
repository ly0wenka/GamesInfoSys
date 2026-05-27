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
    IReadOnlyList<string> Tags,
    IReadOnlyList<ExternalStoreLink> ExternalStores
);

public sealed record GameScreenshot(string? Image);

public sealed record ExternalStoreLink(string Store, string Url);
