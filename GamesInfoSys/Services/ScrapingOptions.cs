namespace GamesInfoSys.Services;

public sealed class ScrapingOptions
{
    public bool Enabled { get; init; } = true;
    public int MaxResultsPerStore { get; init; } = 8;
    public int CacheMinutes { get; init; } = 30;
}

