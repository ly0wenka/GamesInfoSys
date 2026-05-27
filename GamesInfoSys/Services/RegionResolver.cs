using Microsoft.Extensions.Options;

namespace GamesInfoSys.Services;

public enum GamePlatform
{
    Pc,
    PlayStation,
    Xbox,
    Switch,
    Mobile
}

public sealed class RegionResolver
{
    private readonly PricingOptions _options;

    public RegionResolver(IOptions<PricingOptions> options)
    {
        _options = options.Value;
    }

    public string DefaultRegion => NormalizeRegion(_options.DefaultRegion) ?? "UA";

    public string ForPlatform(GamePlatform platform)
    {
        var key = platform.ToString();
        if (_options.PlatformRegions.TryGetValue(key, out var region))
            return NormalizeRegion(region) ?? DefaultRegion;

        return DefaultRegion;
    }

    private static string? NormalizeRegion(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
            return null;
        return region.Trim().ToUpperInvariant();
    }
}

