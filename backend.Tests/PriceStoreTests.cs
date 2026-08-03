using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using PuppyFinder.Api.Data;
using PuppyFinder.Api.Models;
using PuppyFinder.Api.Services;

namespace PuppyFinder.Api.Tests;

/// <summary>
/// Exercises the real SQLite file — an in-memory fake would skip exactly the
/// things worth testing here (migrations, upsert conflict handling, append-only
/// observations).
/// </summary>
public sealed class PriceStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("puppyfinder-price-tests").FullName;

    // xunit 2.x has no TestContext; these tests are fast and self-contained.
    private static CancellationToken Ct => CancellationToken.None;

    private (PriceDb Db, PriceStore Store) NewStore()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Prices:DbPath"] = Path.Combine(_dir, "prices.db"),
            })
            .Build();
        var db = new PriceDb(configuration, new StubEnvironment(_dir), NullLogger<PriceDb>.Instance);
        return (db, new PriceStore(db, NullLogger<PriceStore>.Instance));
    }

    [Fact]
    public async Task SchemaIsIdempotentAcrossRestarts()
    {
        // Two PriceDb instances over one file is exactly what a service restart does.
        var first = NewStore();
        await first.Db.EnsureSchemaAsync(Ct);

        var second = NewStore();
        await second.Db.EnsureSchemaAsync(Ct);

        // Still usable, and the seed still lands once rather than twice.
        await second.Store.SeedFromCatalogAsync(Ct);
        await second.Store.SeedFromCatalogAsync(Ct);

        var slug = SiteCatalog.Breeds.First(b => b.PriceHigh > 0).Slug;
        var observations = await second.Store.GetObservationsAsync(slug, null, Ct);
        Assert.Single(observations);
    }

    [Fact]
    public async Task SeedMarksEveryLegacyRangeUnverified()
    {
        var (_, store) = NewStore();

        await store.SeedFromCatalogAsync(Ct);
        var prices = await store.GetAllAsync(Ct);

        var expected = SiteCatalog.Breeds.Count(b => b.PriceLow > 0 && b.PriceHigh > 0);
        Assert.Equal(expected, prices.Count);
        // The whole point of the seed: nothing imported may claim to be verified,
        // because no source was ever recorded for these numbers.
        Assert.All(prices.Values, p => Assert.Equal(PriceConfidence.Unverified, p.Confidence));
        Assert.All(prices.Values, p => Assert.Equal(0, p.SourceCount));
    }

    [Fact]
    public async Task SeededObservationsSayTheyHaveNoSource()
    {
        var (_, store) = NewStore();
        await store.SeedFromCatalogAsync(Ct);

        var beagle = await store.GetObservationsAsync("beagle", null, Ct);

        var observation = Assert.Single(beagle);
        Assert.Equal("legacy hardcoded (unsourced)", observation.Publisher);
        Assert.Equal("", observation.SourceUrl);
        Assert.Equal(PriceScope.Unscoped, observation.Scope);
    }

    [Fact]
    public async Task SeedDoesNotOverwriteResearchedPrices()
    {
        var (_, store) = NewStore();
        await store.UpsertAsync(new BreedPrice(
            "beagle", 900, 1400, PriceConfidence.Verified, 3, DateTimeOffset.UtcNow, SpreadRatio: 1.2),
            Ct);

        await store.SeedFromCatalogAsync(Ct);

        var beagle = await store.FindAsync("beagle", Ct);
        Assert.Equal(PriceConfidence.Verified, beagle!.Confidence);
        Assert.Equal(900, beagle.PriceLow);
    }

    [Fact]
    public async Task UpsertReplacesTheLiveRangeAndInvalidatesTheCache()
    {
        var (_, store) = NewStore();
        await store.SeedFromCatalogAsync(Ct);
        // Populate the read cache first, so a stale cache would be caught.
        _ = await store.GetAllAsync(Ct);

        await store.UpsertAsync(new BreedPrice(
            "beagle", 700, 1600, PriceConfidence.Contested, 2, DateTimeOffset.UtcNow, SpreadRatio: 2.4),
            Ct);

        var beagle = await store.FindAsync("beagle", Ct);
        Assert.Equal(700, beagle!.PriceLow);
        Assert.Equal(1600, beagle.PriceHigh);
        Assert.Equal(PriceConfidence.Contested, beagle.Confidence);
        Assert.Equal(2.4, beagle.SpreadRatio!.Value, 3);
    }

    [Fact]
    public async Task ObservationsAreAppendOnly()
    {
        var (_, store) = NewStore();
        await store.SeedFromCatalogAsync(Ct);

        await store.AddObservationsAsync([Sample("beagle", 800, 1300)], Ct);
        await store.AddObservationsAsync([Sample("beagle", 950, 1500)], Ct);

        var all = await store.GetObservationsAsync("beagle", null, Ct);
        // The legacy seed row plus both new ones — nothing is replaced.
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task ReviewDecisionsAreRecordedWithoutDeletingTheObservation()
    {
        var (_, store) = NewStore();
        await store.AddObservationsAsync(
            [Sample("beagle", 800, 1300, ObservationStatus.Pending)], Ct);

        var pending = await store.GetPendingAsync(Ct);
        var target = Assert.Single(pending);

        var slug = await store.SetObservationStatusAsync(
            target.Id, ObservationStatus.Rejected, "publisher is a breeder selling the breed",
            Ct);

        // The slug comes back so the caller can re-aggregate that breed without a
        // second lookup.
        Assert.Equal("beagle", slug);
        Assert.Empty(await store.GetPendingAsync(Ct));
        var kept = Assert.Single(await store.GetObservationsAsync("beagle", ObservationStatus.Rejected, Ct));
        Assert.Equal("publisher is a breeder selling the breed", kept.RejectReason);
    }

    [Fact]
    public async Task UnfinishedRunsAreDetectableSoTheJobWontOverlap()
    {
        var (_, store) = NewStore();
        var run = new PriceRun("run-1", DateTimeOffset.UtcNow);

        await store.StartRunAsync(run, Ct);
        Assert.True(await store.HasUnfinishedRunAsync(Ct));

        await store.FinishRunAsync(run with { FinishedAt = DateTimeOffset.UtcNow, BreedsChecked = 4, Accepted = 3, Pending = 1 },
            Ct);
        Assert.False(await store.HasUnfinishedRunAsync(Ct));
    }

    private static PriceObservation Sample(
        string slug, int low, int high, string status = ObservationStatus.Accepted) => new(
        BreedSlug: slug,
        PriceLow: low,
        PriceHigh: high,
        Scope: PriceScope.PetStandard,
        Kind: FigureKind.Range,
        SourceUrl: "https://example.test/breed-price",
        Publisher: "Example Pet Insurance",
        PublisherTier: PublisherTier.A,
        Quote: "Expect to pay between $800 and $1,300 for a puppy from a reputable breeder.",
        RetrievedAt: DateTimeOffset.UtcNow,
        RunId: "run-test",
        Model: "claude-opus-5",
        Status: status);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // temp dir cleanup is best-effort; a held file handle shouldn't fail the run
        }
    }

    private sealed class StubEnvironment(string root) : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "PuppyFinder.Api.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Test";
    }
}
