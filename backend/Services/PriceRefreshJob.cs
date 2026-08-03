using PuppyFinder.Api.Data;
using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <summary>Summary of one refresh pass, for the run record and the admin response.</summary>
public record PriceRefreshSummary(
    string RunId,
    int BreedsChecked,
    int Accepted,
    int Pending,
    int Rejected,
    int Unverifiable,
    IReadOnlyList<string> Errors)
{
    public static PriceRefreshSummary Empty(string reason) =>
        new("", 0, 0, 0, 0, 0, [reason]);
}

/// <summary>
/// Refreshes breed prices on a slow cadence, and re-derives confidence from stored
/// observations.
///
/// Follows <see cref="AlertChecker"/> closely, including its swallow-and-log posture: a
/// research run failing must never take the API down. Three guardrails matter more than
/// the schedule itself — it skips entirely without an API key, refuses to overlap a run
/// that never finished, and caps breeds per pass so a bug can't spend unbounded.
/// </summary>
public sealed class PriceRefreshJob(
    PriceStore store,
    PriceResearchService research,
    BreedCatalogService catalog,
    IConfiguration configuration,
    ILogger<PriceRefreshJob> logger) : BackgroundService
{
    private readonly SemaphoreSlim _runLock = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!research.IsEnabled)
        {
            // Same posture as RescueGroupsProvider: absent key means dormant, not broken.
            logger.LogInformation(
                "Price refresh is dormant — no Anthropic API key configured. Prices stay as they are.");
            return;
        }

        // Scheduled runs are opt-in, and default to off even with a key present.
        //
        // This job is not like AlertChecker, whose loop it was first copied from: that one
        // diffs local data for free, so running it at startup costs nothing. This one makes
        // a paid API call per breed — 179 of them — and writes to the data a fraud check
        // depends on. Setting a key should never, by itself, start spending money against
        // an untuned prompt; the plan's calibration gate exists precisely to be walked
        // through first, one breed at a time, via POST /api/admin/price-research.
        if (!configuration.GetValue("Prices:AutoRefresh", false))
        {
            logger.LogInformation(
                "Price refresh is idle — scheduled runs are off (set Prices:AutoRefresh=true to enable). "
                + "Research single breeds via POST /api/admin/price-research while calibrating.");
            return;
        }

        // Breed prices move on the scale of years. Monthly keeps the data fresh without
        // churning the number the fraud check measures against.
        var interval = TimeSpan.FromDays(configuration.GetValue("Prices:RefreshDays", 30));
        using var timer = new PeriodicTimer(interval);
        logger.LogInformation(
            "Price refresh is scheduled every {Days} day(s); first run is one interval away, not at startup.",
            interval.TotalDays);
        try
        {
            // Wait for the first tick before the first run. Running at startup would mean
            // every service restart triggers a full paid sweep — and restarts happen for
            // reasons that have nothing to do with prices being stale.
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
    }

    /// <summary>
    /// Researches breeds that need it, then re-aggregates. Safe to call from the admin
    /// endpoint; serialized against the scheduled pass so the two can't interleave.
    /// </summary>
    public async Task<PriceRefreshSummary> RunAsync(CancellationToken ct, string? onlyBreedSlug = null)
    {
        if (!research.IsEnabled)
        {
            return PriceRefreshSummary.Empty(
                "No Anthropic API key configured (set Anthropic:ApiKey or ANTHROPIC_API_KEY).");
        }

        if (!await _runLock.WaitAsync(TimeSpan.Zero, ct))
        {
            return PriceRefreshSummary.Empty("A price research run is already in progress.");
        }

        try
        {
            // An unfinished run means a previous pass died mid-flight. Starting another
            // would double-spend and make the audit trail ambiguous about which run
            // produced what.
            if (onlyBreedSlug is null && await store.HasUnfinishedRunAsync(ct))
            {
                return PriceRefreshSummary.Empty(
                    "A previous run never finished — inspect price_run before starting another.");
            }

            var runId = $"run-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
            var run = new PriceRun(runId, DateTimeOffset.UtcNow);
            await store.StartRunAsync(run, ct);

            var thresholds = PriceThresholds.FromConfiguration(configuration);
            var maxBreeds = configuration.GetValue("Prices:MaxBreedsPerRun", 200);
            var breeds = (await catalog.GetBreedsAsync(ct))
                .Where(b => onlyBreedSlug is null || b.Slug.Equals(onlyBreedSlug, StringComparison.OrdinalIgnoreCase))
                .Take(maxBreeds)
                .ToList();

            int accepted = 0, pending = 0, rejected = 0, unverifiable = 0;
            List<string> errors = [];

            foreach (var breed in breeds)
            {
                ct.ThrowIfCancellationRequested();
                var result = await research.ResearchAsync(breed, runId, ct);

                if (!result.Succeeded)
                {
                    errors.Add($"{breed.Slug}: {result.Error}");
                    continue;
                }

                if (result.Unverifiable)
                {
                    unverifiable++;
                }

                // Everything is recorded, including rejections — the audit trail is the
                // point, and a rejection is evidence about a source.
                var rejectedRows = result.Rejected
                    .Select(r => r.Observation with
                    {
                        Status = ObservationStatus.Rejected,
                        RejectReason = r.Reason,
                    })
                    .ToList();
                await store.AddObservationsAsync([.. result.Accepted, .. rejectedRows], ct);
                rejected += rejectedRows.Count;

                var outcome = await ReaggregateBreedAsync(breed.Slug, thresholds, ct);
                if (outcome?.Price?.Confidence == PriceConfidence.Verified)
                {
                    accepted++;
                }
                else if (outcome?.Price is not null)
                {
                    pending++;
                }
            }

            await store.FinishRunAsync(
                run with
                {
                    FinishedAt = DateTimeOffset.UtcNow,
                    BreedsChecked = breeds.Count,
                    Accepted = accepted,
                    Pending = pending,
                    Rejected = rejected,
                    Error = errors.Count > 0 ? string.Join("; ", errors.Take(5)) : null,
                }, ct);

            catalog.InvalidatePrices();
            logger.LogInformation(
                "Price run {RunId}: {Checked} breeds, {Accepted} verified, {Pending} needing review, {Rejected} rejected rows",
                runId, breeds.Count, accepted, pending, rejected);

            return new PriceRefreshSummary(runId, breeds.Count, accepted, pending, rejected, unverifiable, errors);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Price refresh failed: {Message}", ex.Message);
            return PriceRefreshSummary.Empty(ex.Message);
        }
        finally
        {
            _runLock.Release();
        }
    }

    /// <summary>
    /// Re-derives one breed's live range from everything stored for it. No network, so this
    /// is how threshold changes are applied — free and idempotent.
    ///
    /// <para>
    /// Considers <b>both</b> kinds of evidence, because there are two writers to
    /// <c>breed_price</c> and only one row. This method used to look at observations alone
    /// and upsert the result, which meant a single call to <c>/api/admin/price-reaggregate</c>
    /// silently reverted every listing-derived range back to its editorial value — the free
    /// "re-tune the threshold" operation quietly threw away better data. Precedence is
    /// decided in one place instead: a listing range that clears its own bar wins, otherwise
    /// the editorial range stands.
    /// </para>
    /// </summary>
    public async Task<PriceAggregation?> ReaggregateBreedAsync(
        string breedSlug, PriceThresholds thresholds, CancellationToken ct)
    {
        var observations = await store.GetObservationsAsync(breedSlug, status: null, ct);
        var listings = await store.GetListingPricesAsync(breedSlug, thresholds.ListingWindowDays, ct);
        if (observations.Count == 0 && listings.Count == 0)
        {
            return null;
        }

        var current = await store.FindAsync(breedSlug, ct);
        var aggregation = PriceObservationValidator.Aggregate(breedSlug, observations, thresholds, current);

        // Listings are the primary source when they qualify. The floor guard compares against
        // the editorial range just derived, falling back to the unsourced seed as a smell
        // test — see ListingPriceAggregator.
        var guard = aggregation.Price;
        if (guard is null && SiteCatalog.SeedPrice(breedSlug) is { } seed)
        {
            guard = new BreedPrice(breedSlug, seed.Low, seed.High,
                PriceConfidence.Unverified, 0, DateTimeOffset.UtcNow);
        }

        var fromListings = ListingPriceAggregator.Aggregate(breedSlug, listings, thresholds, guard);
        if (fromListings.Price is { Confidence: PriceConfidence.Verified } published)
        {
            aggregation = aggregation with
            {
                Price = published with { Basis = PriceBasis.Listings },
                Rationale = fromListings.Rationale,
            };
        }

        // When nothing can back a range, fall all the way back to the seed rather than
        // leaving whatever numbers happened to be in the row. A refused listing range used to
        // linger as the visible band, relabelled editorial/unverified — hidden from the UI,
        // but still a stored figure that no evidence supported.
        if (aggregation.Price is { Confidence: PriceConfidence.Unverified }
            && SiteCatalog.SeedPrice(breedSlug) is { } fallback
            && (aggregation.Price.PriceLow != fallback.Low || aggregation.Price.PriceHigh != fallback.High))
        {
            aggregation = aggregation with
            {
                Price = aggregation.Price with { PriceLow = fallback.Low, PriceHigh = fallback.High },
                Rationale = aggregation.Rationale + "; reverted to the unsourced seed range",
            };
        }

        if (aggregation.Price is not null)
        {
            await store.UpsertAsync(aggregation.Price, ct);

            // Invalidate here rather than in each caller. Only RunAsync and
            // ReaggregateAllAsync used to do it, so the two paths that re-derive a single
            // breed — hand-entered observations, and accepting a review decision — left
            // BreedCatalogService serving the old price with the new confidence.
            //
            // That combination is the exact failure this project exists to prevent: it
            // showed German Shepherd as "verified" at the unsourced legacy $1,000-$3,000
            // rather than the researched $2,000-$4,000, and PriceCheck screened quotes
            // against the wrong band while claiming to be sourced.
            catalog.InvalidatePrices();
        }

        return aggregation;
    }

    /// <summary>Re-derives every breed. Seconds, free, and safe to run repeatedly.</summary>
    public async Task<Dictionary<string, PriceAggregation>> ReaggregateAllAsync(
        PriceThresholds thresholds, CancellationToken ct)
    {
        Dictionary<string, PriceAggregation> results = [];
        foreach (var slug in (await store.GetAllAsync(ct)).Keys.ToList())
        {
            if (await ReaggregateBreedAsync(slug, thresholds, ct) is { } aggregation)
            {
                results[slug] = aggregation;
            }
        }

        catalog.InvalidatePrices();
        return results;
    }
}
