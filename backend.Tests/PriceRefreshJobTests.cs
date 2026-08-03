using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

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

    private WebApplicationFactory<Program> NewApp(bool autoRefresh)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("Prices:DbPath", DbPath)
            .UseSetting("Alerts:StorePath", Path.Combine(_dir, "alerts.json"))
            // A key that is present but not real: IsEnabled is true, so we are testing the
            // schedule rather than the dormant-without-a-key path.
            .UseSetting("Anthropic:ApiKey", "sk-ant-not-a-real-key")
            // Belt and braces: if the startup-run regression ever comes back, this test
            // makes one failing API call rather than 179.
            .UseSetting("Prices:MaxBreedsPerRun", "1")
            .UseSetting("Prices:AutoRefresh", autoRefresh ? "true" : "false"));

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

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
