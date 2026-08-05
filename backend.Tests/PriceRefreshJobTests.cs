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

    [Fact]
    public async Task ASharpMoveIsLoggedAsAWarningSinceNothingElseSurfacesIt()
    {
        // The review queue that was supposed to surface this is gone: it read an observation
        // status nothing ever wrote, and the hold it existed to show lasts exactly one run —
        // the guard downgrades to Contested but still publishes, so the next aggregation sees
        // no drift and promotes it. That leaves this log line as the only trace, so it is worth
        // a test: without it the guard is decorative, which is where this started.
        var logs = new CapturingLoggerProvider();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("Prices:DbPath", DbPath)
            .UseSetting("Alerts:StorePath", Path.Combine(_dir, "alerts.json"))
            .UseSetting("Anthropic:ApiKey", "")
            .UseSetting("Prices:ListingsEnabled", "false")
            .ConfigureServices(s => s.AddSingleton<ILoggerProvider>(logs)));
        factory.CreateClient().Dispose();

        var store = factory.Services.GetRequiredService<PriceStore>();
        var job = factory.Services.GetRequiredService<PriceRefreshJob>();

        // A range we already trusted, then evidence that moves it sharply.
        await store.UpsertAsync(
            new BreedPrice("beagle", 1000, 1400, PriceConfidence.Verified, 3, DateTimeOffset.UtcNow), Ct);
        await store.AddObservationsAsync(
            [Editorial(2500, 4000, "MetLife Pet Insurance",
                 "https://www.metlifepetinsurance.com/blog/breed-spotlights/beagle/"),
             Editorial(2600, 4100, "Rover", "https://www.rover.com/blog/beagle-price/"),
             Editorial(2450, 4200, "Canine Bible", "https://www.caninebible.com/beagle-price/")],
            Ct);

        var result = await job.ReaggregateBreedAsync("beagle", new PriceThresholds(), Ct);

        Assert.Equal(PriceConfidence.Contested, result!.Price!.Confidence);
        var warning = Assert.Single(logs.Entries,
            e => e.Level == LogLevel.Warning && e.Message.Contains("Held beagle"));
        // The old numbers have to be in it: "held for review" without them can't be judged
        // later, and by then the previous range is overwritten and unrecoverable.
        Assert.Contains("1000-1400", warning.Message);
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
