namespace GamesInfoSys.Data.Entities;

public sealed class OfferPricePoint
{
    public long Id { get; set; }

    public long StoreOfferId { get; set; }
    public StoreOffer? StoreOffer { get; set; }

    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    public string Currency { get; set; } = "";
    public long PriceMinor { get; set; }
    public long? OriginalPriceMinor { get; set; }
}

