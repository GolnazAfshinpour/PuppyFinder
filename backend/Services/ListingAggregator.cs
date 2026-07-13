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
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Provider {Source} failed: {Message}", provider.SourceName, ex.Message);
                    _lastCounts[provider.SourceName] = 0;
                    _lastErrors[provider.SourceName] = ex.Message;
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
