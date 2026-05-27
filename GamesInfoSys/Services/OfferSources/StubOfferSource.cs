using GamesInfoSys.Data.Entities;

namespace GamesInfoSys.Services.OfferSources;

public sealed class StubOfferSource : IOfferSource
{
    public StubOfferSource(string store, string platform)
    {
        Store = store;
        Platform = platform;
    }

    public string Store { get; }
    public string Platform { get; }

    public Task SyncAsync(TrackedGame game, string region)
    {
        // Placeholder: implement official store ingestion for this platform/store.
        return Task.CompletedTask;
    }
}

