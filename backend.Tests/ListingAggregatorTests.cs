using Microsoft.Extensions.Logging.Abstractions;
using PuppyFinder.Api.Models;
using PuppyFinder.Api.Services;

namespace PuppyFinder.Api.Tests;

/// <summary>
/// A provider that fails must not take its dogs with it.
///
/// Found chasing a flaky end-to-end check: the log read "Provider RescueGroups failed: A task was
/// canceled" — a 15-second HTTP timeout, set when that provider made one request and kept after it
/// began walking three paginated ones. The aggregator recorded the failure and cached the merged
/// result *without* those 297 dogs for the full ten-minute period, so the page quietly served 48
/// of 345 and looked healthy while doing it.
/// </summary>
public class ListingAggregatorTests
{
    private static CancellationToken Ct => CancellationToken.None;

    private sealed class StubProvider(string name, params Listing[] listings) : IListingProvider
    {
        public string SourceName { get; } = name;
        public bool IsEnabled { get; set; } = true;
        public bool ShouldThrow { get; set; }
        public int Calls { get; private set; }

        public Task<IReadOnlyList<Listing>> FetchListingsAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return ShouldThrow
                ? throw new TaskCanceledException("A task was canceled.")
                : Task.FromResult<IReadOnlyList<Listing>>(listings);
        }
    }

    private static Listing Dog(string id, string source) => new(
        Id: id,
        Name: id,
        Breed: "Mixed Breed",
        Age: "Adult",
        Sex: "Female",
        Description: "",
        City: "Somewhere",
        State: "MD",
        ImageUrl: null,
        ListingUrl: "https://example.test/dog",
        Source: source,
        SourceUrl: "https://example.test");

    private static ListingAggregator NewAggregator(params IListingProvider[] providers) =>
        new(providers, NullLogger<ListingAggregator>.Instance);

    [Fact]
    public async Task AFailedProviderServesItsPreviousListingsRatherThanNone()
    {
        var flaky = new StubProvider("Flaky", Dog("a", "Flaky"), Dog("b", "Flaky"));
        var steady = new StubProvider("Steady", Dog("c", "Steady"));
        var aggregator = NewAggregator(flaky, steady);

        var first = await aggregator.GetListingsAsync(Ct);
        Assert.Equal(3, first.Count);

        // Same failure the live provider hit, and the cache window has to be stepped over for the
        // aggregator to try again at all.
        flaky.ShouldThrow = true;
        typeof(ListingAggregator)
            .GetField("_cachedAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(aggregator, DateTimeOffset.UtcNow - TimeSpan.FromHours(1));

        var second = await aggregator.GetListingsAsync(Ct);

        // Both dogs are still there, from the previous fetch.
        Assert.Equal(3, second.Count);
        Assert.Equal(2, second.Count(l => l.Source == "Flaky"));

        // And the failure is visible rather than silent.
        var status = aggregator.GetSourceStatuses().Single(s => s.Name == "Flaky");
        Assert.Equal(2, status.ListingCount);
        Assert.Contains("previous fetch", status.LastError);
    }

    [Fact]
    public async Task AProviderThatHasNeverSucceededReportsItsErrorPlainly()
    {
        // Nothing to fall back on, so the honest answer is zero and the reason.
        var broken = new StubProvider("Broken") { ShouldThrow = true };
        var aggregator = NewAggregator(broken, new StubProvider("Steady", Dog("c", "Steady")));

        var listings = await aggregator.GetListingsAsync(Ct);

        Assert.Single(listings);
        var status = aggregator.GetSourceStatuses().Single(s => s.Name == "Broken");
        Assert.Equal(0, status.ListingCount);
        Assert.Contains("A task was canceled", status.LastError);
        Assert.DoesNotContain("previous fetch", status.LastError);
    }

    [Fact]
    public async Task ResultsAreCachedSoTheSourceApisAreNotHammered()
    {
        // The terms of at least one source ask callers not to flood the API, so this is a
        // contractual property and not only an optimisation.
        var provider = new StubProvider("Steady", Dog("c", "Steady"));
        var aggregator = NewAggregator(provider);

        for (var i = 0; i < 5; i++)
        {
            await aggregator.GetListingsAsync(Ct);
        }

        Assert.Equal(1, provider.Calls);
    }
}
