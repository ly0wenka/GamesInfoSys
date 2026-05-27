using GamesInfoSys.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GamesInfoSys.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TrackedGame> TrackedGames => Set<TrackedGame>();
    public DbSet<StoreOffer> StoreOffers => Set<StoreOffer>();
    public DbSet<OfferPricePoint> OfferPricePoints => Set<OfferPricePoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TrackedGame>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(256);
            b.HasIndex(x => x.RawgGameId);
        });

        modelBuilder.Entity<StoreOffer>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Store).HasMaxLength(64);
            b.Property(x => x.Platform).HasMaxLength(32);
            b.Property(x => x.Region).HasMaxLength(8);
            b.Property(x => x.Currency).HasMaxLength(8);
            b.Property(x => x.Title).HasMaxLength(256);
            b.Property(x => x.Url).HasMaxLength(1024);
            b.HasIndex(x => new { x.Store, x.ExternalId, x.Region }).IsUnique();
            b.HasIndex(x => new { x.TrackedGameId, x.Platform, x.Region });
        });

        modelBuilder.Entity<OfferPricePoint>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Currency).HasMaxLength(8);
            b.HasIndex(x => new { x.StoreOfferId, x.AtUtc });
        });
    }
}

