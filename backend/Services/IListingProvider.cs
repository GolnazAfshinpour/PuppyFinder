using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <summary>
/// One source website that PuppyFinder aggregates listings from.
/// </summary>
public interface IListingProvider
{
    string SourceName { get; }

    /// <summary>False when the provider can't run (e.g. missing API credentials).</summary>
    bool IsEnabled { get; }

    Task<IReadOnlyList<Listing>> FetchListingsAsync(CancellationToken cancellationToken);
}
