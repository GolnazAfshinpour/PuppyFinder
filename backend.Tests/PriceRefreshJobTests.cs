using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
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
