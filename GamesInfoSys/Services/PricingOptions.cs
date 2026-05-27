namespace GamesInfoSys.Services;

public sealed class PricingOptions
{
    public string DefaultRegion { get; init; } = "UA";
    public Dictionary<string, string> PlatformRegions { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string PreferredCurrency { get; init; } = "UAH";
}
