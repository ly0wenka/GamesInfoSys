namespace GamesInfoSys.Services;

public sealed class RawgOptions
{
    public string ApiKey { get; init; } = "";
    public string BaseUrl { get; init; } = "https://api.rawg.io/api/";
    public bool UseDemoDataWhenNoApiKey { get; init; } = true;
}

