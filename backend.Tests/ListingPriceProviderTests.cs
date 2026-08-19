using PuppyFinder.Api.Data;
using PuppyFinder.Api.Models;
using PuppyFinder.Api.Services;

namespace PuppyFinder.Api.Tests;

/// <summary>
/// The fan-out provider that merges per-source samples. The properties that matter:
/// a disabled source is invisible, one host failing must not discard another's sample,
/// and only every-host-failed reports as an error — naming each host's reason.
/// </summary>
public class ListingPriceProviderTests
{
    private static readonly Breed Beagle = new(
        Slug: "beagle", DisplayName: "Beagle", AkcSlug: "beagle", Size: "Small",
        Energy: 3, Grooming: 1, Shedding: 3, KidFriendly: 5, ApartmentFriendly: 3,
        PriceLow: 500, PriceHigh: 1200, Blurb: "test");

    private sealed class FakeSource(
        string host, bool enabled, ListingFetchResult? result = null, bool carries = true)
        : IListingPriceSource
    {
        public string Host => host;
        public bool IsEnabled => enabled;
        public bool Carries(string breedSlug) => carries;
        public int Calls { get; private set; }

        public Task<ListingFetchResult> FetchAsync(Breed breed, string runId, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(result ?? new ListingFetchResult(breed.Slug, [], 0, 0, null));
        }
    }

    private static ListingPrice Price(string host, int amount, string reference) => new(
        BreedSlug: "beagle", Price: amount, SourceHost: host, ListingRef: reference,
        ListingName: "Beagle", RetrievedAt: DateTimeOffset.UtcNow, RunId: "test-run");

    [Fact]
    public async Task MergesSamplesAcrossEnabledSources()
    {
        var a = new FakeSource("a.example", enabled: true,
            new ListingFetchResult("beagle", [Price("a.example", 600, "a/1")], 3, 1, null));
        var b = new FakeSource("b.example", enabled: true,
            new ListingFetchResult("beagle", [Price("b.example", 800, "b/1")], 2, 0, null));

        var merged = await new ListingPriceProvider([a, b]).FetchAsync(Beagle, "run", default);

        Assert.True(merged.Succeeded);
        Assert.Equal(2, merged.Prices.Count);
        Assert.Equal(5, merged.SeenTotal);
        Assert.Equal(1, merged.DroppedMixes);
    }

    [Fact]
    public async Task ADisabledSourceIsNeverAsked()
    {
        var off = new FakeSource("off.example", enabled: false);
        var on = new FakeSource("on.example", enabled: true,
            new ListingFetchResult("beagle", [Price("on.example", 700, "on/1")], 1, 0, null));

        var merged = await new ListingPriceProvider([off, on]).FetchAsync(Beagle, "run", default);

        Assert.Equal(0, off.Calls);
        Assert.Single(merged.Prices);
    }

    [Fact]
    public async Task OneHostFailingDoesNotDiscardTheOthersSample()
    {
        var failing = new FakeSource("down.example", enabled: true,
            new ListingFetchResult("beagle", [], 0, 0, "503 from down.example"));
        var working = new FakeSource("up.example", enabled: true,
            new ListingFetchResult("beagle", [Price("up.example", 900, "up/1")], 1, 0, null));

        var merged = await new ListingPriceProvider([failing, working]).FetchAsync(Beagle, "run", default);

        // Partial success serves — a run where only every host failed is an error.
        Assert.True(merged.Succeeded);
        Assert.Single(merged.Prices);
    }

    [Fact]
    public async Task EveryHostFailingReportsEachHostsReason()
    {
        var a = new FakeSource("a.example", enabled: true,
            new ListingFetchResult("beagle", [], 0, 0, "503"));
        var b = new FakeSource("b.example", enabled: true,
            new ListingFetchResult("beagle", [], 0, 0, "timeout"));

        var merged = await new ListingPriceProvider([a, b]).FetchAsync(Beagle, "run", default);

        Assert.False(merged.Succeeded);
        Assert.Contains("a.example: 503", merged.Error);
        Assert.Contains("b.example: timeout", merged.Error);
    }

    [Fact]
    public async Task SaysSoWhenNoEnabledSourceCarriesTheBreed()
    {
        var narrow = new FakeSource("narrow.example", enabled: true, carries: false);

        var merged = await new ListingPriceProvider([narrow]).FetchAsync(Beagle, "run", default);

        Assert.False(merged.Succeeded);
        Assert.Equal(0, narrow.Calls);
        Assert.Contains("carries", merged.Error);
    }

    [Fact]
    public void IsEnabledAndCarriesFollowTheSources()
    {
        var off = new FakeSource("off.example", enabled: false);
        Assert.False(new ListingPriceProvider([off]).IsEnabled);

        var on = new FakeSource("on.example", enabled: true, carries: false);
        var provider = new ListingPriceProvider([off, on]);
        Assert.True(provider.IsEnabled);
        // Enabled but carrying nothing: the breed is not reachable through this provider.
        Assert.False(provider.Carries("beagle"));
    }
}
