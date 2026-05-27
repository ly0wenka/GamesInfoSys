namespace GamesInfoSys.Data.Entities;

public sealed class StoreOffer
{
    public long Id { get; set; }

    public long TrackedGameId { get; set; }
    public TrackedGame? TrackedGame { get; set; }

    public string Store { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Region { get; set; } = "";

    public string ExternalId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";

    public string? Currency { get; set; }
    public long? PriceMinor { get; set; }
    public long? OriginalPriceMinor { get; set; }

    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}

