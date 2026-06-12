using GamesInfoSys.Services;

namespace GamesInfoSys.Tests;

public sealed class SteamAppIdParserTests
{
    [Theory]
    [InlineData("1245620", "1245620")]
    [InlineData(" https://store.steampowered.com/app/1245620/ELDEN_RING/ ", "1245620")]
    [InlineData("https://store.steampowered.com/app/620/", "620")]
    public void TryParse_ReturnsSteamAppId_ForSupportedInputs(string input, string expected)
    {
        var actual = SteamAppIdParser.TryParse(input);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("https://example.com/app/1245620/")]
    [InlineData("https://store.steampowered.com/sub/12345/")]
    public void TryParse_ReturnsNull_ForUnsupportedInputs(string? input)
    {
        var actual = SteamAppIdParser.TryParse(input);

        Assert.Null(actual);
    }
}

