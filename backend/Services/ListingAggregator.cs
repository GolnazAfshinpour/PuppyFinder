using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

public record SourceStatus(string Name, bool Enabled, int ListingCount, string? LastError);

/// <summary>
/// Fans out to all registered listing providers, merges the results, and
/// caches them in memory for a short TTL so the UI doesn't hammer source APIs.
/// </summary>
public sealed class ListingAggregator(
    IEnumerable<IListingProvider> providers,
    ILogger<ListingAggregator> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly Dictionary<string, string?> _lastErrors = [];
    private readonly Dictionary<string, int> _lastCounts = [];

    /// <summary>
    /// The last successful result per provider, so a failed fetch degrades to stale data rather
    /// than to nothing. A 15-second timeout on one provider used to remove 297 of 345 dogs for a
    /// full cache period, and the page gave no sign of it.
    /// </summary>
    private readonly Dictionary<string, IReadOnlyList<Listing>> _lastGood = [];
    private IReadOnlyList<Listing> _cached = [];
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;

    public async Task<IReadOnlyList<Listing>> GetListingsAsync(CancellationToken cancellationToken)
    {
        if (DateTimeOffset.UtcNow - _cachedAt < CacheTtl)
        {
            return _cached;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (DateTimeOffset.UtcNow - _cachedAt < CacheTtl)
            {
                return _cached;
            }

            var merged = new List<Listing>();
            foreach (var provider in providers.Where(p => p.IsEnabled))
            {
                try
                {
                    var listings = await provider.FetchListingsAsync(cancellationToken);
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
                    // Reuse what this provider last returned. Stale by minutes beats absent: the
                    // detail view already handles a dog that has since gone, whereas an empty
                    // source just looks like the app has fewer dogs than it does.
                    var stale = _lastGood.GetValueOrDefault(provider.SourceName, []);
                    merged.AddRange(stale);
                    _lastCounts[provider.SourceName] = stale.Count;
                    _lastErrors[provider.SourceName] = stale.Count > 0
                        ? $"{ex.Message} — serving {stale.Count} listings from the previous fetch"
                        : ex.Message;
                    logger.LogWarning(
                        "Provider {Source} failed: {Message}. Serving {Count} listings from the "
                        + "previous fetch", provider.SourceName, ex.Message, stale.Count);
                }
            }

            _cached = merged;
            _cachedAt = DateTimeOffset.UtcNow;
            return _cached;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public IReadOnlyList<SourceStatus> GetSourceStatuses() =>
        providers
            .Select(p => new SourceStatus(
                p.SourceName,
                p.IsEnabled,
                _lastCounts.GetValueOrDefault(p.SourceName),
                _lastErrors.GetValueOrDefault(p.SourceName)))
            .ToList();
}
