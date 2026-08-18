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
///
/// The second half of these tests comes from the harder version of the same bug, found later: on a
/// freshly started process there is nothing to fall back on, so an aborted fetch cached *nothing*
/// and the whole site read as empty until the period expired.
/// </summary>
public class ListingAggregatorTests
{
    private static CancellationToken Ct => CancellationToken.None;

    /// <summary>A clock the test moves by hand, so cache windows are testable without sleeping.</summary>
    private sealed class TestClock
    {
        public DateTimeOffset Now { get; set; } = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);
        public Func<DateTimeOffset> Func => () => Now;
        public void Advance(TimeSpan by) => Now += by;
    }

    private sealed class StubProvider(string name, params Listing[] listings) : IListingProvider
    {
        public string SourceName { get; } = name;
        public bool IsEnabled { get; set; } = true;
        public bool ShouldThrow { get; set; }
        public int Calls { get; private set; }

        /// <summary>The token the aggregator handed us, so a test can assert whose it was.</summary>
        public CancellationToken LastToken { get; private set; }

        public Task<IReadOnlyList<Listing>> FetchListingsAsync(CancellationToken cancellationToken)
        {
            Calls++;
            LastToken = cancellationToken;
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

    private static ListingAggregator NewAggregator(TestClock clock, params IListingProvider[] providers) =>
        new(providers, NullLogger<ListingAggregator>.Instance, clock.Func);

    [Fact]
    public async Task AFailedProviderServesItsPreviousListingsRatherThanNone()
    {
        var clock = new TestClock();
        var flaky = new StubProvider("Flaky", Dog("a", "Flaky"), Dog("b", "Flaky"));
        var steady = new StubProvider("Steady", Dog("c", "Steady"));
        var aggregator = NewAggregator(clock, flaky, steady);

        var first = await aggregator.GetListingsAsync(Ct);
        Assert.Equal(3, first.Count);

        // Same failure the live provider hit, once the cache window has passed.
        flaky.ShouldThrow = true;
        clock.Advance(TimeSpan.FromHours(1));

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

    // ---- one caller's disconnect must not empty the site ----

    [Fact]
    public async Task TheRefreshIsNotCancellableByTheCallerWhoTriggeredIt()
    {
        // The token these endpoints receive belongs to one HTTP request, and this cache is shared
        // by everyone. Handing it to the providers meant a browser navigating away cancelled the
        // fetch mid-flight, and the cancellation was then written into the shared cache.
        var provider = new StubProvider("Steady", Dog("c", "Steady"));
        var aggregator = NewAggregator(provider);
        using var caller = new CancellationTokenSource();

        await aggregator.GetListingsAsync(caller.Token);

        Assert.False(provider.LastToken.CanBeCanceled);
    }

    [Fact]
    public async Task ATotalFailureIsNotCachedForTheFullPeriod()
    {
        // The shape that emptied the live site: a cold process, every provider failing, and
        // nothing in the fallback to serve. Sitting on that for ten minutes shows no dogs, no
        // coverage and a live count of zero long after the sources recover.
        var clock = new TestClock();
        var a = new StubProvider("A", Dog("a", "A")) { ShouldThrow = true };
        var b = new StubProvider("B", Dog("b", "B")) { ShouldThrow = true };
        var aggregator = NewAggregator(clock, a, b);

        Assert.Empty(await aggregator.GetListingsAsync(Ct));

        // Well inside the normal cache period, but past the failure retry.
        clock.Advance(TimeSpan.FromSeconds(31));
        a.ShouldThrow = false;
        b.ShouldThrow = false;

        var recovered = await aggregator.GetListingsAsync(Ct);
        Assert.Equal(2, recovered.Count);
    }

    [Fact]
    public async Task AFailedRefreshStillDoesNotRetryOnEveryRequest()
    {
        // Short, but not zero: a source outage must not turn into a flood.
        var broken = new StubProvider("Broken") { ShouldThrow = true };
        var aggregator = NewAggregator(new TestClock(), broken);

        for (var i = 0; i < 5; i++)
        {
            await aggregator.GetListingsAsync(Ct);
        }

        Assert.Equal(1, broken.Calls);
    }

    [Fact]
    public async Task OneProviderStillWorkingKeepsTheNormalCachePeriod()
    {
        // Only a *total* failure gets the short retry. One source down while another answers is
        // the case the fallback already covers, and re-fetching it every 30 seconds would hammer
        // the healthy one to no purpose.
        var clock = new TestClock();
        var broken = new StubProvider("Broken") { ShouldThrow = true };
        var steady = new StubProvider("Steady", Dog("c", "Steady"));
        var aggregator = NewAggregator(clock, broken, steady);

        await aggregator.GetListingsAsync(Ct);
        clock.Advance(TimeSpan.FromSeconds(31));
        await aggregator.GetListingsAsync(Ct);

        Assert.Equal(1, steady.Calls);
    }

    [Fact]
    public async Task ASuccessfulButEmptyFetchIsAnAnswerAndIsCachedNormally()
    {
        // "The shelters have no dogs right now" is a real result. Treating it as a failure would
        // re-fetch every 30 seconds forever in the one case where there is nothing to gain.
        var clock = new TestClock();
        var empty = new StubProvider("Empty");
        var aggregator = NewAggregator(clock, empty);

        Assert.Empty(await aggregator.GetListingsAsync(Ct));
        clock.Advance(TimeSpan.FromSeconds(31));
        await aggregator.GetListingsAsync(Ct);

        Assert.Equal(1, empty.Calls);
    }

    [Fact]
    public async Task NeverFetchedIsDistinguishableFromFetchedAndEmpty()
    {
        // Both used to render as "0 listings, no error", which sent debugging at the source
        // rather than at the cache.
        var empty = new StubProvider("Empty");
        var aggregator = NewAggregator(empty);

        Assert.Null(aggregator.GetSourceStatuses().Single().LastFetchedAt);

        await aggregator.GetListingsAsync(Ct);

        var status = aggregator.GetSourceStatuses().Single();
        Assert.NotNull(status.LastFetchedAt);
        Assert.Equal(0, status.ListingCount);
        Assert.Null(status.LastError);
    }

    [Fact]
    public async Task ADisabledProviderIsNeverCountedAsAFailure()
    {
        // Otherwise "every enabled provider failed" would be true whenever the only enabled one
        // is switched off, and a configuration state would look like an outage.
        var off = new StubProvider("Off") { IsEnabled = false, ShouldThrow = true };
        var steady = new StubProvider("Steady", Dog("c", "Steady"));
        var clock = new TestClock();
        var aggregator = NewAggregator(clock, off, steady);

        await aggregator.GetListingsAsync(Ct);
        clock.Advance(TimeSpan.FromSeconds(31));
        await aggregator.GetListingsAsync(Ct);

        Assert.Equal(0, off.Calls);
        Assert.Equal(1, steady.Calls);
        Assert.Null(aggregator.GetSourceStatuses().Single(s => s.Name == "Off").LastFetchedAt);
    }
}
