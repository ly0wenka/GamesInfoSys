namespace GamesInfoSys.Services;

public static class SteamAppIdParser
{
    public static string? TryParse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        input = input.Trim();
        if (int.TryParse(input, out _))
            return input;

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || uri is null)
            return null;

        if (!uri.Host.Contains("steampowered.com", StringComparison.OrdinalIgnoreCase))
            return null;

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (!segments[i].Equals("app", StringComparison.OrdinalIgnoreCase))
                continue;

            var id = segments[i + 1];
            return int.TryParse(id, out _) ? id : null;
        }

        return null;
    }
}

