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

        // Breed prices move on the scale of years. Monthly keeps the data fresh without
        // churning the number the fraud check measures against.
        var interval = TimeSpan.FromDays(configuration.GetValue("Prices:RefreshDays", 30));
        using var timer = new PeriodicTimer(interval);
        try
        {
            do
            {
                await RunAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
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
    /// Re-derives one breed's live range from its stored observations. No network, so
    /// this is how threshold changes are applied — free and idempotent.
    /// </summary>
    public async Task<PriceAggregation?> ReaggregateBreedAsync(
        string breedSlug, PriceThresholds thresholds, CancellationToken ct)
    {
        var observations = await store.GetObservationsAsync(breedSlug, status: null, ct);
        if (observations.Count == 0)
        {
            return null;
        }

        var current = await store.FindAsync(breedSlug, ct);
        var aggregation = PriceObservationValidator.Aggregate(breedSlug, observations, thresholds, current);
        if (aggregation.Price is not null)
        {
            await store.UpsertAsync(aggregation.Price, ct);
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
