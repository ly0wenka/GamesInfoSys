namespace GamesInfoSys.Data.Entities;

public sealed class TrackedGame
{
    public long Id { get; set; }
    public int? RawgGameId { get; set; }
    public string? Name { get; set; }

    public string? SteamAppId { get; set; }
    public string? PsnProductId { get; set; }
    public string? XboxProductId { get; set; }
    public string? NintendoProductId { get; set; }
    public string? EpicOfferId { get; set; }
    public string? GogProductId { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

