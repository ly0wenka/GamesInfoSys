using GamesInfoSys.Data.Entities;

namespace GamesInfoSys.Services.OfferSources;

public interface IOfferSource
{
    string Store { get; }
    string Platform { get; }

    Task SyncAsync(TrackedGame game, string region);
}

