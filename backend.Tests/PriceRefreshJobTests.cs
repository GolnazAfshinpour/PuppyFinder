using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PuppyFinder.Api.Models;
using PuppyFinder.Api.Services;

namespace PuppyFinder.Api.Tests;

/// <summary>
/// Guards the one property of the schedule that costs real money to get wrong: merely
/// configuring an API key must not start a run.
///
/// This job was first written by copying <c>AlertChecker</c>'s <c>do { } while (tick)</c>
/// loop, which runs immediately and then on every interval. That is right for AlertChecker
/// — it diffs local data for free. It is wrong here: the first restart after a key was
/// configured fired a full paid sweep of every breed against an untuned prompt, before
/// anyone had looked at a single observation. Scheduled runs are now opt-in
/// (<c>Prices:AutoRefresh</c>, default false) and never fire at startup even when enabled.
/// </summary>
public sealed class PriceRefreshJobTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("puppyfinder-refresh-tests").FullName;

    private static CancellationToken Ct => CancellationToken.None;

    // withKey defaults true because the original tests need a present-but-fake key to exercise
    // the *schedule*; the listings tests need it absent, which is the state the app ships in.
    private WebApplicationFactory<Program> NewApp(
        bool autoRefresh, bool listingsEnabled = false, bool withKey = true)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("Prices:DbPath", DbPath)
            .UseSetting("Alerts:StorePath", Path.Combine(_dir, "alerts.json"))
            // A key that is present but not real: IsEnabled is true, so we are testing the
            // schedule rather than the dormant-without-a-key path.
            .UseSetting("Anthropic:ApiKey", withKey ? "sk-ant-not-a-real-key" : "")
            // Belt and braces: if the startup-run regression ever comes back, this test
            // makes one failing API call rather than 179.
            .UseSetting("Prices:MaxBreedsPerRun", "1")
            .UseSetting("Prices:AutoRefresh", autoRefresh ? "true" : "false")
            // Never let a test reach a third-party site by accident. The host loads
            // user-secrets, so this is set explicitly rather than by hoping it wasn't enabled
            // locally. Tests that need it on set it deliberately and cap the work.
            .UseSetting("Prices:ListingsEnabled", listingsEnabled ? "true" : "false")
            // Same reason ListingsEnabled is pinned: the test host loads user-secrets, so a
            // real RescueGroups key on the developer machine would let these tests fetch a
            // third-party API for real. Explicit beats hoping it is unset.
            .UseSetting("RescueGroups:ApiKey", "")
            .UseSetting("Prices:ListingPages", "1"));

        // CreateClient boots the host, which is what starts the BackgroundService.
        factory.CreateClient().Dispose();
        return factory;
    }

    private string DbPath => Path.Combine(_dir, "prices.db");

    private async Task<int> RunRowsAsync()
    {
        // A run writes its price_run row before making any API call, so a row appearing at
        // all is the signal — no need to wait for network work to finish.
        await using var connection = new SqliteConnection($"Data Source={DbPath};Mode=ReadOnly;Cache=Private");
        await connection.OpenAsync(Ct);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM price_run WHERE id LIKE 'run-%';";
        return Convert.ToInt32(await command.ExecuteScalarAsync(Ct));
    }

    // ------------------------------------------------------- the approval gate

    private sealed record Fixture(
        WebApplicationFactory<Program> Factory, PriceStore Store, PriceRefreshJob Job,
        CapturingLoggerProvider Logs) : IDisposable
    {
        public void Dispose() => Factory.Dispose();
    }

    private Fixture NewGateFixture()
    {
        var logs = new CapturingLoggerProvider();
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("Prices:DbPath", DbPath)
            .UseSetting("Alerts:StorePath", Path.Combine(_dir, "alerts.json"))
            .UseSetting("Anthropic:ApiKey", "")
            .UseSetting("Prices:ListingsEnabled", "false")
            .UseSetting("RescueGroups:ApiKey", "")
            .ConfigureServices(s => s.AddSingleton<ILoggerProvider>(logs)));
        factory.CreateClient().Dispose();
        return new Fixture(
            factory,
            factory.Services.GetRequiredService<PriceStore>(),
            factory.Services.GetRequiredService<PriceRefreshJob>(),
            logs);
    }

    /// <summary>A trusted range, plus editorial evidence that moves it sharply upward.</summary>
    private static async Task SetUpASharpMoveAsync(PriceStore store)
    {
        await store.UpsertAsync(
            new BreedPrice("beagle", 1000, 1400, PriceConfidence.Verified, 3, DateTimeOffset.UtcNow), Ct);
        await store.AddObservationsAsync(
            [Editorial(2500, 4000, "MetLife Pet Insurance",
                 "https://www.metlifepetinsurance.com/blog/breed-spotlights/beagle/"),
             Editorial(2600, 4100, "Rover", "https://www.rover.com/blog/beagle-price/"),
             Editorial(2450, 4200, "Canine Bible", "https://www.caninebible.com/beagle-price/")],
            Ct);
    }

    [Fact]
    public async Task ASharpMoveWaitsForApprovalAndDoesNotPublishItself()
    {
        // The point of the whole exercise. The old guard downgraded the range to Contested and
        // published it anyway, so the next run compared the new figures against the row it had
        // just written, found no movement, and promoted them — a one-run delay dressed up as
        // oversight. Now nothing is published until someone decides.
        using var f = NewGateFixture();
        await SetUpASharpMoveAsync(f.Store);

        await f.Job.ReaggregateBreedAsync("beagle", new PriceThresholds(), Ct);

        // The previous range is still live, so the scam check keeps working off numbers we
        // already trusted rather than going quiet on this breed.
        var live = await f.Store.FindAsync("beagle", Ct);
        Assert.Equal(1000, live!.PriceLow);
        Assert.Equal(1400, live.PriceHigh);
        Assert.Equal(PriceConfidence.Verified, live.Confidence);

        var hold = await f.Store.FindOpenHoldAsync("beagle", Ct);
        Assert.NotNull(hold);
        Assert.Equal(2500, hold!.ProposedLow);
        Assert.Equal(1000, hold.FromLow);
        Assert.True(hold.DriftPercent > 40);

        Assert.Contains(f.Logs.Entries,
            e => e.Level == LogLevel.Warning && e.Message.Contains("Holding beagle for approval"));
    }

    [Fact]
    public async Task TheHoldSurvivesRepeatedRunsInsteadOfPromotingItself()
    {
        // The exact defect the gate replaces, asserted directly: re-running must not quietly
        // accept the change, and must not stack up duplicate holds either.
        using var f = NewGateFixture();
        await SetUpASharpMoveAsync(f.Store);

        for (var run = 0; run < 3; run++)
        {
            await f.Job.ReaggregateBreedAsync("beagle", new PriceThresholds(), Ct);
        }

        var live = await f.Store.FindAsync("beagle", Ct);
        Assert.Equal(1000, live!.PriceLow);
        Assert.NotNull(await f.Store.FindOpenHoldAsync("beagle", Ct));
        // One conversation, not one per run — the schema's partial unique index enforces this.
        Assert.Single(await f.Store.GetOpenHoldsAsync(Ct), h => h.BreedSlug == "beagle");
    }

    [Fact]
    public async Task ApprovingAHoldPublishesTheProposalAndStaysPublished()
    {
        using var f = NewGateFixture();
        await SetUpASharpMoveAsync(f.Store);
        await f.Job.ReaggregateBreedAsync("beagle", new PriceThresholds(), Ct);

        var decided = await f.Store.DecideHoldAsync("beagle", HoldDecision.Approved, "checked by hand", Ct);

        Assert.NotNull(decided);
        var live = await f.Store.FindAsync("beagle", Ct);
        Assert.Equal(2500, live!.PriceLow);
        Assert.Equal(4100, live.PriceHigh);
        Assert.Empty(await f.Store.GetOpenHoldsAsync(Ct));

        // And it stays: the next run must not treat the approved range as a fresh sharp move
        // against... itself. Approval publishes the stored proposal verbatim precisely so this
        // comparison comes out at zero.
        await f.Job.ReaggregateBreedAsync("beagle", new PriceThresholds(), Ct);
        Assert.Equal(2500, (await f.Store.FindAsync("beagle", Ct))!.PriceLow);
        Assert.Empty(await f.Store.GetOpenHoldsAsync(Ct));
    }

    [Fact]
    public async Task DismissingAHoldKeepsTheLiveRangeAndStopsAsking()
    {
        // Dismissing means "I've seen it and I'm keeping what we have". Without the memory of
        // that answer the gate becomes a nag: the evidence is still stored, so every run would
        // raise the identical proposal again.
        using var f = NewGateFixture();
        await SetUpASharpMoveAsync(f.Store);
        await f.Job.ReaggregateBreedAsync("beagle", new PriceThresholds(), Ct);

        await f.Store.DecideHoldAsync("beagle", HoldDecision.Dismissed, "one publisher looked wrong", Ct);
        await f.Job.ReaggregateBreedAsync("beagle", new PriceThresholds(), Ct);

        var live = await f.Store.FindAsync("beagle", Ct);
        Assert.Equal(1000, live!.PriceLow);
        Assert.Empty(await f.Store.GetOpenHoldsAsync(Ct));
    }

    [Fact]
    public async Task AHoldClosesItselfWhenTheEvidenceWalksTheChangeBack()
    {
        // Otherwise the hold sits there asking about a change nothing supports any more, and
        // approving it would publish figures the evidence has already abandoned.
        using var f = NewGateFixture();
        await SetUpASharpMoveAsync(f.Store);
        await f.Job.ReaggregateBreedAsync("beagle", new PriceThresholds(), Ct);
        Assert.NotNull(await f.Store.FindOpenHoldAsync("beagle", Ct));

        // Reject the evidence behind the move, so the proposal is no longer a sharp one.
        foreach (var o in await f.Store.GetObservationsAsync("beagle", ObservationStatus.Accepted, Ct))
        {
            await f.Store.SetObservationStatusAsync(o.Id, ObservationStatus.Rejected, "test", Ct);
        }

        await f.Job.ReaggregateBreedAsync("beagle", new PriceThresholds(), Ct);

        Assert.Null(await f.Store.FindOpenHoldAsync("beagle", Ct));
    }

    [Fact]
    public async Task MovingOffAnUnsourcedSeedNeedsNoApproval()
    {
        // Moving off the hardcoded legacy ranges is the entire purpose of this system, not a
        // warning sign. Gating it would mean every breed needed sign-off once, which is how a
        // safety mechanism becomes something people click through without reading.
        using var f = NewGateFixture();
        await f.Store.UpsertAsync(
            new BreedPrice("beagle", 1000, 1400, PriceConfidence.Unverified, 0, DateTimeOffset.UtcNow), Ct);
        await f.Store.AddObservationsAsync(
            [Editorial(2500, 4000, "MetLife Pet Insurance",
                 "https://www.metlifepetinsurance.com/blog/breed-spotlights/beagle/"),
             Editorial(2600, 4100, "Rover", "https://www.rover.com/blog/beagle-price/"),
             Editorial(2450, 4200, "Canine Bible", "https://www.caninebible.com/beagle-price/")],
            Ct);

        await f.Job.ReaggregateBreedAsync("beagle", new PriceThresholds(), Ct);

        Assert.Equal(2500, (await f.Store.FindAsync("beagle", Ct))!.PriceLow);
        Assert.Empty(await f.Store.GetOpenHoldsAsync(Ct));
    }

    [Fact]
    public async Task ARoutineReconfirmationIsNotGated()
    {
        // If ordinary monthly movement needed approval the queue would fill with noise and the
        // one entry that mattered would be lost in it.
        using var f = NewGateFixture();
        await f.Store.UpsertAsync(
            new BreedPrice("beagle", 2400, 4000, PriceConfidence.Verified, 3, DateTimeOffset.UtcNow), Ct);
        await f.Store.AddObservationsAsync(
            [Editorial(2500, 4000, "MetLife Pet Insurance",
                 "https://www.metlifepetinsurance.com/blog/breed-spotlights/beagle/"),
             Editorial(2600, 4100, "Rover", "https://www.rover.com/blog/beagle-price/"),
             Editorial(2450, 4200, "Canine Bible", "https://www.caninebible.com/beagle-price/")],
            Ct);

        await f.Job.ReaggregateBreedAsync("beagle", new PriceThresholds(), Ct);

        Assert.Equal(2500, (await f.Store.FindAsync("beagle", Ct))!.PriceLow);
        Assert.Empty(await f.Store.GetOpenHoldsAsync(Ct));
    }

    private static PriceObservation Editorial(int low, int high, string publisher, string url) => new(
        BreedSlug: "beagle",
        PriceLow: low,
        PriceHigh: high,
        Scope: PriceScope.PetStandard,
        Kind: FigureKind.Range,
        SourceUrl: url,
        Publisher: publisher,
        // Re-derived from the URL at aggregation time, so these must be real entries on the
        // reviewed source list or the range never reaches the bar and drift never fires.
        PublisherTier: PublisherTier.A,
        Quote: $"Expect to pay ${low:N0} to ${high:N0} for a puppy from a reputable breeder.",
        RetrievedAt: DateTimeOffset.UtcNow,
        RunId: "run-test",
        Model: "manual",
        Status: ObservationStatus.Accepted);

    private sealed record LogEntry(LogLevel Level, string Message);

    /// <summary>Records what was logged, so a log line that is load-bearing can be asserted.</summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<LogEntry> _entries = [];

        public IReadOnlyList<LogEntry> Entries
        {
            get { lock (_entries) { return _entries.ToList(); } }
        }

        public ILogger CreateLogger(string categoryName) => new Recorder(_entries);

        public void Dispose() { }

        private sealed class Recorder(List<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (entries)
                {
                    entries.Add(new LogEntry(logLevel, formatter(state, exception)));
                }
            }
        }
    }

    [Fact]
    public async Task ConfiguringAKeyDoesNotStartARun()
    {
        using var app = NewApp(autoRefresh: false);

        // Generous enough that a startup run would have registered itself by now.
        await Task.Delay(TimeSpan.FromSeconds(2), Ct);

        Assert.Equal(0, await RunRowsAsync());
    }

    [Fact]
    public async Task EnablingTheScheduleStillDoesNotRunAtStartup()
    {
        // The interval is a month; even opted in, the first pass is an interval away. A
        // restart is not evidence that prices went stale.
        using var app = NewApp(autoRefresh: true);

        await Task.Delay(TimeSpan.FromSeconds(2), Ct);

        Assert.Equal(0, await RunRowsAsync());
    }

    [Fact]
    public async Task WithNeitherSourceConfiguredTheJobIsDormant()
    {
        using var app = NewApp(autoRefresh: true, listingsEnabled: false, withKey: false);

        await Task.Delay(TimeSpan.FromSeconds(2), Ct);

        // No key and no listings: nothing to do, and nothing written.
        Assert.Equal(0, await RunRowsAsync());
    }

    [Fact]
    public async Task ListingCollectionIsNotGatedOnTheModelKey()
    {
        // The bug this covers: ExecuteAsync tested research.IsEnabled alone, so with no
        // Anthropic key *nothing* ran — including listing collection, which needs no model and
        // produces 49 of the 50 live ranges. The only automatable job was the one that couldn't
        // run, and the one that could wasn't automatable. Left alone the 90-day listing window
        // expires and the next re-aggregation withdraws every listing range at once.
        var app = NewApp(autoRefresh: true, listingsEnabled: true, withKey: false);
        using (app)
        {
            var job = app.Services.GetRequiredService<PriceRefreshJob>();
            var research = app.Services.GetRequiredService<PriceResearchService>();
            var listings = app.Services.GetRequiredService<ListingPriceProvider>();

            Assert.False(research.IsEnabled);  // no key, as shipped
            Assert.True(listings.IsEnabled);

            // The collector reports its own gate rather than the model's: with listings on it
            // does not refuse for a missing API key.
            var summary = await job.CollectListingsAsync(Ct, onlyBreedSlug: "not-a-real-breed");
            Assert.DoesNotContain(
                summary.Errors,
                e => e.Contains("API key", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task CollectionRefusesWhenListingsAreOffAndSaysWhy()
    {
        var app = NewApp(autoRefresh: false, listingsEnabled: false);
        using (app)
        {
            var job = app.Services.GetRequiredService<PriceRefreshJob>();

            var summary = await job.CollectListingsAsync(Ct, onlyBreedSlug: "beagle");

            Assert.Equal(0, summary.BreedsChecked);
            Assert.Contains(summary.Errors, e => e.Contains("Prices:ListingsEnabled"));
            // The terms caveat travels with the refusal, so nobody flips the flag without it.
            Assert.Contains(summary.Errors, e => e.Contains("terms"));
        }
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
