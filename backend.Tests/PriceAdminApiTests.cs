using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PuppyFinder.Api.Models;
using PuppyFinder.Api.Services;

namespace PuppyFinder.Api.Tests;

/// <summary>
/// The admin surface mutates the source of truth behind a fraud check, so its guard is
/// worth testing as carefully as the rules themselves. Also covers the no-API-key path:
/// the whole job must be dormant rather than broken when the key is absent, which is the
/// state the app actually ships in today.
/// </summary>
public sealed class PriceAdminApiTests : IDisposable
{
    private const string Secret = "test-admin-secret";

    private readonly string _dir = Directory.CreateTempSubdirectory("puppyfinder-admin-tests").FullName;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PriceAdminApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("Prices:DbPath", Path.Combine(_dir, "prices.db"))
            .UseSetting("Prices:AdminSecret", Secret)
            .UseSetting("Alerts:StorePath", Path.Combine(_dir, "alerts.json"))
            // Deliberately no Anthropic:ApiKey — the shipped state.
            .UseSetting("Anthropic:ApiKey", ""));
        _client = _factory.CreateClient();
    }

    private HttpRequestMessage Authorised(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Admin-Secret", Secret);
        return request;
    }

    // ---------------------------------------------------------------- the guard

    [Theory]
    [InlineData("/api/admin/price-report")]
    [InlineData("/api/admin/price-review")]
    public async Task ReadEndpointsRefuseWithoutTheSecret(string url)
    {
        var response = await _client.GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/price-research")]
    [InlineData("/api/admin/price-reaggregate")]
    public async Task WriteEndpointsRefuseWithoutTheSecret(string url)
    {
        var response = await _client.PostAsync(url, content: null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AWrongSecretIsRefused()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/price-report");
        request.Headers.Add("X-Admin-Secret", "not-the-secret");

        var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ASecretOfADifferentLengthIsRefusedWithoutThrowing()
    {
        // FixedTimeEquals throws on mismatched lengths if compared carelessly.
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/price-report");
        request.Headers.Add("X-Admin-Secret", "x");

        var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------------------- no key configured

    [Fact]
    public async Task ResearchSaysWhyItCannotRunRatherThanThrowing()
    {
        var response = await _client.SendAsync(Authorised(HttpMethod.Post, "/api/admin/price-research"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains("No Anthropic API key configured", body);
    }

    [Fact]
    public void TheResearchServiceReportsItselfDisabledWithoutAKey()
    {
        var research = _factory.Services.GetRequiredService<PriceResearchService>();

        Assert.False(research.IsEnabled);
    }

    [Fact]
    public async Task TheAppServesNormallyWithNoKeyConfigured()
    {
        // The whole point of the dormant path: nothing about the app changes.
        var breeds = await _client.GetAsync("/api/breeds", Ct);
        var check = await _client.GetAsync("/api/price-check?breed=beagle&price=800", Ct);

        Assert.Equal(HttpStatusCode.OK, breeds.StatusCode);
        Assert.Equal(HttpStatusCode.OK, check.StatusCode);
    }

    // ---------------------------------------------------------------- the report

    [Fact]
    public async Task ReportShowsTheLiveDistributionAndWhatIfThresholds()
    {
        var response = await _client.SendAsync(Authorised(HttpMethod.Get, "/api/admin/price-report"), Ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

        // Seeded legacy ranges are all unverified, so that's what the live view shows.
        var live = body.GetProperty("liveDistribution");
        Assert.True(live.TryGetProperty(PriceConfidence.Unverified, out var unverified));
        Assert.True(unverified.GetInt32() > 0);

        // Several candidate bars, so the threshold is picked from evidence.
        var whatIf = body.GetProperty("whatIf").EnumerateArray().ToList();
        Assert.True(whatIf.Count >= 4);
        Assert.All(whatIf, candidate => Assert.True(candidate.TryGetProperty("thresholds", out _)));
    }

    [Fact]
    public async Task ReportIsReadOnly()
    {
        var before = await CurrentConfidenceAsync("beagle");

        await _client.SendAsync(Authorised(HttpMethod.Get, "/api/admin/price-report"), Ct);

        Assert.Equal(before, await CurrentConfidenceAsync("beagle"));
    }

    // ---------------------------------------------------------------- re-aggregation

    [Fact]
    public async Task ReaggregationIsFreeAndLeavesLegacyRangesUnverified()
    {
        var response = await _client.SendAsync(
            Authorised(HttpMethod.Post, "/api/admin/price-reaggregate"), Ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(body.GetProperty("breeds").GetInt32() > 0);

        // The seeded rows are unscoped, so nothing can reach verified from them — the
        // gate holds even after an explicit re-aggregation.
        Assert.Equal(PriceConfidence.Unverified, await CurrentConfidenceAsync("beagle"));
    }

    [Fact]
    public async Task ReviewQueueIsEmptyBeforeAnyResearchRuns()
    {
        var response = await _client.SendAsync(Authorised(HttpMethod.Get, "/api/admin/price-review"), Ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Empty(body.EnumerateArray());
    }

    [Fact]
    public async Task ReviewingAnUnknownObservationIs404()
    {
        var response = await _client.SendAsync(
            Authorised(HttpMethod.Post, "/api/admin/price-review/999999?decision=accept"), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReviewRejectsAnUnknownDecision()
    {
        var response = await _client.SendAsync(
            Authorised(HttpMethod.Post, "/api/admin/price-review/1?decision=maybe"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<string?> CurrentConfidenceAsync(string slug)
    {
        var breeds = await _client.GetFromJsonAsync<JsonElement>("/api/breeds", Ct);
        return breeds.EnumerateArray()
            .FirstOrDefault(b => b.GetProperty("slug").GetString() == slug)
            .TryGetProperty("confidence", out var confidence) ? confidence.GetString() : null;
    }

    private static CancellationToken Ct => CancellationToken.None;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // best-effort temp cleanup
        }
    }
}
