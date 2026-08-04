using PuppyFinder.Api.Data;
using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <summary>What one breed's listing collection produced, for the admin response.</summary>
public record ListingBreedOutcome(
    string Breed,
    int SeenTotal,
    int DroppedMixes,
    int Kept,
    int SampleSize,
    int Median,
    BreedPrice? ListingRange,
    string Rationale,
    BreedPrice? Published,
    string? Error = null);

/// <summary>Summary of one listing-collection pass.</summary>
public record ListingCollectionSummary(
    string RunId,
    int BreedsChecked,
    int Published,
    int Refused,
    int CrossbreedsDropped,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ListingBreedOutcome> Breeds)
{
    public static ListingCollectionSummary Empty(string reason) =>
        new("", 0, 0, 0, 0, [reason], []);
}

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
    ListingPriceProvider listings,
    BreedCatalogService catalog,
    IConfiguration configuration,
    ILogger<PriceRefreshJob> logger) : BackgroundService
{
    private readonly SemaphoreSlim _runLock = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Two sources, two independent gates. This used to test research.IsEnabled alone, so
        // with no Anthropic key nothing ran at all — including listing collection, which needs
        // no model and produces almost every live range. The only automatable job was the one
        // that couldn't run, and the one that could wasn't automatable.
        if (!research.IsEnabled && !listings.IsEnabled)
        {
            // Same posture as RescueGroupsProvider: nothing configured means dormant, not broken.
            logger.LogInformation(
                "Price refresh is dormant — no Anthropic API key and listing collection is off. "
                + "Prices stay as they are.");
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
            "Price refresh is scheduled every {Days} day(s) — listings: {Listings}, research: {Research}. "
            + "First run is one interval away, not at startup.",
            interval.TotalDays,
            listings.IsEnabled ? "on" : "off",
            research.IsEnabled ? "on" : "off (no API key)");
        try
        {
            // Wait for the first tick before the first run. Running at startup would mean
            // every service restart triggers a full paid sweep — and restarts happen for
            // reasons that have nothing to do with prices being stale.
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                // Each pass does whatever it currently can. Listings first: they are the
                // primary source, they cost nothing but requests, and their 90-day window is
                // what actually expires.
                if (listings.IsEnabled)
                {
                    await CollectListingsAsync(stoppingToken);
                }

                if (research.IsEnabled)
                {
                    await RunAsync(stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
    }

    /// <summary>
    /// Collects live asking prices and re-derives the affected breeds.
    ///
    /// <para>
    /// Lives here rather than in the admin endpoint because the scheduler needs it too, and
    /// two copies of the vendor-dedup, run-recording and precedence rules would drift. Holds
    /// the same lock as the research pass: both write breed_price, so they must not interleave.
    /// </para>
    /// </summary>
    public async Task<ListingCollectionSummary> CollectListingsAsync(
        CancellationToken ct, string? onlyBreedSlug = null)
    {
        if (!listings.IsEnabled)
        {
            return ListingCollectionSummary.Empty(
                "Listing collection is disabled. Set Prices:ListingsEnabled=true to enable it — "
                + "note the source's terms restrict automated collection (see docs/SOURCES.md).");
        }

        if (!await _runLock.WaitAsync(TimeSpan.Zero, ct))
        {
            return ListingCollectionSummary.Empty("A price run is already in progress.");
        }

        try
        {
            var thresholds = PriceThresholds.FromConfiguration(configuration);
            var runId = $"listings-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";

            // Every breed the vendor is known to carry, not just the curated 25. Aliases like
            // "Teacup Poodle" aren't breeds anywhere but here and 404 on the vendor, which read
            // as five errors on the first run; LinkSlugOverride is the marker.
            var targets = (await catalog.GetBreedsAsync(ct))
                .Where(b => onlyBreedSlug is null
                    ? b.LinkSlugOverride is null
                        && (SiteCatalog.IsCurated(b.Slug) || ListingSources.IsKnownToVendor(b.Slug))
                    : b.Slug.Equals(onlyBreedSlug, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (targets.Count == 0)
            {
                return ListingCollectionSummary.Empty($"No such breed '{onlyBreedSlug}'.");
            }

            await store.StartRunAsync(new PriceRun(runId, DateTimeOffset.UtcNow), ct);

            // Several catalog entries are the same breed on the vendor's side —
            // bulldog/english-bulldog, poodle/standard-poodle, australian-shepherd/
            // shepherd-australian, pembroke-welsh-corgi/pembroke — because the curated list and
            // the dog.ceo list overlap. Fetch each vendor breed once and reuse it, so we don't
            // make the same request twice against a site whose terms we are already stretching.
            Dictionary<string, ListingFetchResult> byVendorSlug = new(StringComparer.OrdinalIgnoreCase);
            List<ListingBreedOutcome> outcomes = [];
            int published = 0, refused = 0, mixes = 0;
            List<string> errors = [];

            foreach (var target in targets)
            {
                ct.ThrowIfCancellationRequested();

                var vendorSlug = ListingSources.VendorSlug(target.Slug);
                if (!byVendorSlug.TryGetValue(vendorSlug, out var fetched))
                {
                    fetched = await listings.FetchAsync(target, runId, ct);
                    byVendorSlug[vendorSlug] = fetched;
                }
                else
                {
                    // Same listings, recorded under this breed's slug so each catalog entry has
                    // its own sample rather than sharing rows.
                    fetched = fetched with
                    {
                        Prices = [.. fetched.Prices.Select(p => p with { BreedSlug = target.Slug })],
                    };
                }

                if (!fetched.Succeeded)
                {
                    errors.Add($"{target.Slug}: {fetched.Error}");
                    outcomes.Add(new ListingBreedOutcome(
                        target.Slug, 0, 0, 0, 0, 0, null, fetched.Error ?? "", null, fetched.Error));
                    continue;
                }

                mixes += fetched.DroppedMixes;
                await store.AddListingPricesAsync(fetched.Prices, ct);

                // Publishing goes through the shared precedence path, not a second copy of the
                // rules — two writers deciding independently is what let a re-aggregation
                // silently revert listing ranges to editorial ones.
                var aggregation = await ReaggregateBreedAsync(target.Slug, thresholds, ct);

                // Reported separately from what got published, so a refusal is legible: the
                // sample that was gathered *and* the reason it didn't win.
                var sample = await store.GetListingPricesAsync(target.Slug, thresholds.ListingWindowDays, ct);
                var guard = await store.FindAsync(target.Slug, ct);
                var view = ListingPriceAggregator.Aggregate(target.Slug, sample, thresholds, guard);

                if (aggregation?.Price is { Basis: PriceBasis.Listings, Confidence: PriceConfidence.Verified })
                {
                    published++;
                }
                else
                {
                    refused++;
                }

                outcomes.Add(new ListingBreedOutcome(
                    target.Slug, fetched.SeenTotal, fetched.DroppedMixes, fetched.Prices.Count,
                    view.SampleSize, view.Median, view.Price, view.Rationale, aggregation?.Price));
            }

            catalog.InvalidatePrices();

            // Field names are the editorial job's: Accepted = breeds published, Pending =
            // breeds refused, Rejected = crossbreed listings dropped.
            await store.FinishRunAsync(
                new PriceRun(runId, DateTimeOffset.UtcNow)
                {
                    FinishedAt = DateTimeOffset.UtcNow,
                    BreedsChecked = targets.Count,
                    Accepted = published,
                    Pending = refused,
                    Rejected = mixes,
                    Error = errors.Count > 0 ? string.Join("; ", errors.Take(5)) : null,
                }, ct);

            logger.LogInformation(
                "Listing run {RunId}: {Checked} breeds, {Published} published, {Refused} refused, "
                + "{Mixes} crossbreeds dropped",
                runId, targets.Count, published, refused, mixes);

            return new ListingCollectionSummary(
                runId, targets.Count, published, refused, mixes, errors, outcomes);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Same swallow-and-log posture as the research pass: a bad run must not take the
            // API down.
            logger.LogWarning("Listing collection failed: {Message}", ex.Message);
            return ListingCollectionSummary.Empty(ex.Message);
        }
        finally
        {
            _runLock.Release();
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
        var timestampNow = DateTimeOffset.UtcNow;
        var observations = await store.GetObservationsAsync(breedSlug, status: null, ct);
        var listings = await store.GetListingPricesAsync(breedSlug, thresholds.ListingWindowDays, ct);
        if (observations.Count == 0 && listings.Count == 0)
        {
            return null;
        }

        var current = await store.FindAsync(breedSlug, ct);
        var aggregation = PriceObservationValidator.Aggregate(breedSlug, observations, thresholds, current);

        // The floor guard must be evidence *independent of the listings*, or a marketplace
        // ends up validating itself one run removed. Only two things qualify: a researched
        // editorial range, or the unsourced seed as a smell test. Never the current row —
        // that may itself be listings-derived.
        var seed = SiteCatalog.SeedPrice(breedSlug);
        var guard = aggregation.Price
            ?? (seed is { } s
                ? new BreedPrice(breedSlug, s.Low, s.High, PriceConfidence.Unverified, 0, timestampNow)
                : null);

        var fromListings = ListingPriceAggregator.Aggregate(breedSlug, listings, thresholds, guard);

        // Precedence, in one place: a listing range that clears its own bar wins; otherwise a
        // derivable editorial range; otherwise the seed, marked unverified so nothing screens
        // against it; otherwise nothing at all.
        if (fromListings.Price is { Confidence: PriceConfidence.Verified } published)
        {
            aggregation = aggregation with
            {
                Price = published with { Basis = PriceBasis.Listings },
                Rationale = fromListings.Rationale,
            };
        }
        else if (listings.Count > 0)
        {
            // A listing sample existed and was refused: report *that* reason, not the editorial
            // path's "no usable pet-quality figures found". The withdrawal log would otherwise
            // name the wrong cause — saying nothing was published because no article covered
            // the breed, when in truth 30 of 54 listings were one seller's litter.
            aggregation = aggregation with { Rationale = fromListings.Rationale };
        }

        if (aggregation.Price is null && seed is { } fallback)
        {
            aggregation = aggregation with
            {
                Price = new BreedPrice(breedSlug, fallback.Low, fallback.High,
                    PriceConfidence.Unverified, 0, timestampNow, Basis: PriceBasis.Editorial),
                Rationale = aggregation.Rationale + "; showing the unsourced seed range",
            };
        }

        // Nothing can back a range: withdraw the old one rather than leaving it live. Without
        // this, re-aggregation could only ever raise confidence — Irish Wolfhound went on
        // serving a $2,000-$2,100 band after its sample was refused as one seller's litter.
        if (aggregation.Price is null)
        {
            if (await store.RemoveAsync(breedSlug, ct))
            {
                catalog.InvalidatePrices();
                logger.LogInformation(
                    "Withdrew the published range for {Breed}: {Reason}", breedSlug, aggregation.Rationale);
            }

            return aggregation;
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
