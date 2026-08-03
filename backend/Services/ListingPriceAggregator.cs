using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <summary>One listing's asking price, as stored.</summary>
public record ListingPrice(
    string BreedSlug,
    int Price,
    string SourceHost,
    string ListingRef,
    string ListingName,
    DateTimeOffset RetrievedAt,
    string RunId,
    long Id = 0);

/// <summary>What a listing sample says, and whether it can be published.</summary>
public record ListingAggregation(
    BreedPrice? Price,
    int SampleSize,
    int Median,
    string Rationale);

/// <summary>
/// Turns a sample of real asking prices into a publishable range.
///
/// <para>
/// Pure, like <see cref="PriceObservationValidator"/>, and for the same reason: the
/// listing rows are the durable artifact, so the bar can be re-tuned by re-aggregating
/// rather than re-fetching. That split has already paid for itself twice.
/// </para>
///
/// <para>
/// The band is the interquartile range, not min–max. Min–max on live listings is
/// meaningless — a single $400 scam listing and a single $15,000 rare-colour listing would
/// define the whole range. The middle half is what an honest buyer actually encounters.
/// </para>
/// </summary>
public static class ListingPriceAggregator
{
    /// <summary>Below this a purebred puppy price isn't credible; above it isn't mainstream.</summary>
    private const int AbsoluteFloor = 100;
    private const int AbsoluteCeiling = 25_000;

    /// <summary>
    /// Derives the range from a listing sample, sanity-checked against the editorial range.
    /// </summary>
    /// <param name="editorial">
    /// The published-source range for this breed, when one exists. This is the floor guard,
    /// and it is the reason this isn't circular: a classifieds site's cheap tail is exactly
    /// what the scam check exists to flag, so a listing range whose middle half sits far
    /// below what every publisher reports is evidence about the marketplace, not about the
    /// breed. Measured on Beagle, where listings gave $400–$900 against an editorial
    /// $400–$1,200 — calibrating to the former would teach the check that a $400 Beagle is
    /// normal.
    /// </param>
    public static ListingAggregation Aggregate(
        string breedSlug,
        IEnumerable<ListingPrice> listings,
        PriceThresholds thresholds,
        BreedPrice? editorial = null,
        DateTimeOffset? now = null)
    {
        var timestamp = now ?? DateTimeOffset.UtcNow;

        // One vote per listing: the same animal re-fetched in a later run must not count
        // twice, and the unique index only protects within a run.
        var prices = listings
            .Where(l => l.Price is >= AbsoluteFloor and <= AbsoluteCeiling)
            .GroupBy(l => l.ListingRef, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(l => l.RetrievedAt).First().Price)
            .OrderBy(p => p)
            .ToList();

        if (prices.Count < thresholds.MinListingSample)
        {
            return new ListingAggregation(
                null, prices.Count, prices.Count > 0 ? Percentile(prices, 50) : 0,
                $"{prices.Count} listings, {thresholds.MinListingSample} required");
        }

        var low = Percentile(prices, 25);
        var high = Percentile(prices, 75);
        var median = Percentile(prices, 50);

        // A degenerate sample — every listing at the same price — can't describe a range.
        if (low >= high)
        {
            return new ListingAggregation(
                null, prices.Count, median,
                $"the middle half of {prices.Count} listings is a single price (${low:n0})");
        }

        var band = (double)high / low;
        string? blocked = null;

        if (band > thresholds.MaxVerifiedBandRatio)
        {
            blocked = $"the middle half spans {band:0.00}x (${low:n0}–${high:n0}), "
                + $"limit {thresholds.MaxVerifiedBandRatio:0.00}x";
        }
        else if (editorial is { PriceLow: > 0 } published
            && low < published.PriceLow * thresholds.ListingFloorFactor)
        {
            // The drag-down case. Not "the sources are wrong" — the marketplace's cheap end
            // is real, and it's what we screen against, so it must not become the benchmark.
            blocked = $"listing 25th percentile (${low:n0}) is below "
                + $"{thresholds.ListingFloorFactor:0.00}x the published low (${published.PriceLow:n0}) — "
                + "the marketplace's cheap tail, not the breed's price";
        }

        var confidence = blocked is null ? PriceConfidence.Verified : PriceConfidence.Contested;
        var rationale = blocked
            ?? $"middle half of {prices.Count} live listings, median ${median:n0}";

        return new ListingAggregation(
            new BreedPrice(breedSlug, low, high, confidence, prices.Count, timestamp, band),
            prices.Count,
            median,
            rationale);
    }

    /// <summary>
    /// Nearest-rank percentile over a pre-sorted list. Deliberately not interpolating —
    /// every value is a real asking price, and an interpolated one is a price nobody is
    /// charging.
    /// </summary>
    public static int Percentile(IReadOnlyList<int> sorted, int percentile)
    {
        if (sorted.Count == 0)
        {
            return 0;
        }

        var rank = (int)Math.Round(percentile / 100.0 * (sorted.Count - 1), MidpointRounding.AwayFromZero);
        return sorted[Math.Clamp(rank, 0, sorted.Count - 1)];
    }
}
