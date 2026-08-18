using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <param name="LastFetchedAt">
/// When this provider was last actually asked, or null if it never has been. Without it a
/// process that has not fetched yet is indistinguishable from one whose source returned nothing,
/// and both render as "0 listings, no error" — which sends anyone debugging an empty page
/// looking at the source instead of at the cache.
/// </param>
public record SourceStatus(
    string Name,
    bool Enabled,
    int ListingCount,
    string? LastError,
    DateTimeOffset? LastFetchedAt = null);

/// <summary>
/// Fans out to all registered listing providers, merges the results, and
/// caches them in memory for a short TTL so the UI doesn't hammer source APIs.
/// </summary>
public sealed class ListingAggregator
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How long to sit on a refresh in which <b>every</b> provider failed.
    ///
    /// <para>
    /// Not the full <see cref="CacheTtl"/>. A total failure is not an answer worth keeping for
    /// ten minutes: on a cold process there is nothing in <c>_lastGood</c> to fall back on, so
    /// caching it serves an empty app — no dogs, no coverage, the hero's live count at zero —
    /// long after the sources came back. Observed in exactly that shape: one aborted fetch on a
    /// freshly started process emptied the site until the period expired.
    /// </para>
    ///
    /// <para>
    /// Short, but not zero: retrying on every request would turn a source outage into a flood,
    /// and at least one source's terms ask us not to do that. Thirty seconds with the providers'
    /// own HTTP timeouts on top is a few attempts a minute at worst.
    /// </para>
    /// </summary>
    private static readonly TimeSpan FailedRefreshRetryDelay = TimeSpan.FromSeconds(30);

    private readonly IEnumerable<IListingProvider> _providers;
    private readonly ILogger<ListingAggregator> _logger;
    private readonly Func<DateTimeOffset> _now;

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly Dictionary<string, string?> _lastErrors = [];
    private readonly Dictionary<string, int> _lastCounts = [];
    private readonly Dictionary<string, DateTimeOffset> _lastFetchedAt = [];

    /// <summary>
    /// The last successful result per provider, so a failed fetch degrades to stale data rather
    /// than to nothing. A 15-second timeout on one provider used to remove 297 of 345 dogs for a
    /// full cache period, and the page gave no sign of it.
    /// </summary>
    private readonly Dictionary<string, IReadOnlyList<Listing>> _lastGood = [];
    private IReadOnlyList<Listing> _cached = [];
    private DateTimeOffset _cacheExpiresAt = DateTimeOffset.MinValue;

    /// <param name="now">
    /// Injectable clock, so the cache windows can be tested without sleeping or reaching into
    /// private fields by reflection.
    /// </param>
    public ListingAggregator(
        IEnumerable<IListingProvider> providers,
        ILogger<ListingAggregator> logger,
        Func<DateTimeOffset>? now = null)
    {
        _providers = providers;
        _logger = logger;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<Listing>> GetListingsAsync(CancellationToken cancellationToken)
    {
        if (_now() < _cacheExpiresAt)
        {
            return _cached;
        }

        // A caller waiting on someone else's refresh may give up — that is theirs to cancel.
        // What happens *inside* the refresh is not, see below.
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_now() < _cacheExpiresAt)
            {
                return _cached;
            }

            var merged = new List<Listing>();
            var attempted = 0;
            var failed = 0;

            foreach (var provider in _providers.Where(p => p.IsEnabled))
            {
                attempted++;
                _lastFetchedAt[provider.SourceName] = _now();
                try
                {
                    // Deliberately NOT the caller's token.
                    //
                    // This cache is shared by every visitor, and the token these endpoints receive
                    // is scoped to one HTTP request. A browser navigating away, a closed tab or a
                    // test runner moving on would cancel the fetch mid-flight — and the aggregator
                    // would then write that cancellation into the shared cache, so one person's
                    // disconnect emptied the site for everyone. That is precisely how it was found.
                    //
                    // Nothing here can hang: every provider's HttpClient carries its own timeout
                    // (15s for the county feeds, 40s for RescueGroups), which is the right bound
                    // for a background refresh nobody is waiting on.
                    var listings = await provider.FetchListingsAsync(CancellationToken.None);
                    merged.AddRange(listings);
                    _lastCounts[provider.SourceName] = listings.Count;
                    _lastErrors[provider.SourceName] = null;
                    if (listings.Count > 0)
                    {
                        _lastGood[provider.SourceName] = listings;
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    // Reuse what this provider last returned. Stale by minutes beats absent: the
                    // detail view already handles a dog that has since gone, whereas an empty
                    // source just looks like the app has fewer dogs than it does.
                    var stale = _lastGood.GetValueOrDefault(provider.SourceName, []);
                    merged.AddRange(stale);
                    _lastCounts[provider.SourceName] = stale.Count;
                    _lastErrors[provider.SourceName] = stale.Count > 0
                        ? $"{ex.Message} — serving {stale.Count} listings from the previous fetch"
                        : ex.Message;
                    _logger.LogWarning(
                        "Provider {Source} failed: {Message}. Serving {Count} listings from the "
                        + "previous fetch", provider.SourceName, ex.Message, stale.Count);
                }
            }

            // Every enabled provider threw. That is a different event from "the shelters have no
            // dogs today", and it must not be cached like one — a provider that succeeds with an
            // empty list has answered, and gets the normal period.
            var everyProviderFailed = attempted > 0 && failed == attempted;
            if (everyProviderFailed)
            {
                _logger.LogError(
                    "All {Count} listing providers failed. Serving {Served} listings and retrying "
                    + "in {Seconds}s rather than holding this for {Minutes} minutes",
                    attempted, merged.Count, FailedRefreshRetryDelay.TotalSeconds, CacheTtl.TotalMinutes);
            }

            _cached = merged;
            _cacheExpiresAt = _now() + (everyProviderFailed ? FailedRefreshRetryDelay : CacheTtl);
            return _cached;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public IReadOnlyList<SourceStatus> GetSourceStatuses() =>
        _providers
            .Select(p => new SourceStatus(
                p.SourceName,
                p.IsEnabled,
                _lastCounts.GetValueOrDefault(p.SourceName),
                _lastErrors.GetValueOrDefault(p.SourceName),
                _lastFetchedAt.TryGetValue(p.SourceName, out var at) ? at : null))
            .ToList();
}
