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
            .UseSetting("Anthropic:ApiKey", "")
            // Pinned off, and this is not belt-and-braces. The test host loads user-secrets
            // like any Development run, so with Prices:ListingsEnabled set locally the suite
            // began fetching a third-party site for real — one test took 1m40s and made live
            // requests. A unit test must never depend on, or touch, someone else's server.
            .UseSetting("Prices:ListingsEnabled", "false"));
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

    // ------------------------------------------------- hand-gathered observations

    private HttpRequestMessage Ingest(string json)
    {
        var request = Authorised(HttpMethod.Post, "/api/admin/price-observations");
        request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return request;
    }

    [Fact]
    public async Task IngestRequiresTheSecret()
    {
        var response = await _client.PostAsync("/api/admin/price-observations", content: null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task IngestRefusesAnUnknownBreedSlugRatherThanOrphaningRows()
    {
        // Rows under a slug no breed reads would never surface — silently useless data is
        // worse than a rejected request.
        var response = await _client.SendAsync(Ingest("""
            [{ "breed": "not-a-real-breed", "observations": [] }]
            """), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Unknown breed slug", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task IngestRejectsMalformedJsonWithAnExplanation()
    {
        var response = await _client.SendAsync(Ingest("{ not json"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not valid JSON", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task HandGatheredRowsFaceTheSameRulesAsGeneratedOnes()
    {
        // One good Tier A row and one from a domain we block precisely because it sells
        // puppies. Entering data by hand must not be a way around the source policy.
        var response = await _client.SendAsync(Ingest("""
            [{
              "breed": "beagle",
              "observations": [
                {
                  "publisher": "MetLife Pet Insurance",
                  "sourceUrl": "https://www.metlifepetinsurance.com/blog/breed-spotlights/beagle/",
                  "quote": "Beagle puppies from a reputable breeder typically cost between $800 and $1,500.",
                  "scope": "pet_standard", "figureKind": "range",
                  "priceLow": 800, "priceHigh": 1500
                },
                {
                  "publisher": "Lancaster Puppies",
                  "sourceUrl": "https://www.lancasterpuppies.com/breeds/beagle",
                  "quote": "Beagle puppies for sale, starting at $400 with delivery available.",
                  "scope": "pet_standard", "figureKind": "range",
                  "priceLow": 400, "priceHigh": 900
                }
              ]
            }]
            """), Ct);
        response.EnsureSuccessStatusCode();

        var breed = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct))
            .GetProperty("breeds").EnumerateArray().Single();

        Assert.Equal(2, breed.GetProperty("submitted").GetInt32());
        Assert.Equal(1, breed.GetProperty("accepted").GetInt32());
        var rejected = breed.GetProperty("rejected").EnumerateArray().Single();
        Assert.Contains("lancasterpuppies.com", rejected.GetProperty("sourceUrl").GetString());

        // One accepted source can't reach verified, so screening stays off — the gate is
        // not something hand-entry can bypass either.
        Assert.NotEqual(PriceConfidence.Verified, await CurrentConfidenceAsync("beagle"));
    }

    [Fact]
    public async Task IngestedPricesAreVisibleImmediatelyOnTheBreedsEndpoint()
    {
        // The bug this guards: only the full-run paths invalidated the catalog cache, so
        // re-deriving one breed left /api/breeds serving the old price with the NEW
        // confidence. German Shepherd read as "verified" at the unsourced legacy
        // $1,000-$3,000 instead of the researched $2,000-$4,000 — a sourced label on an
        // unsourced number, which is the precise failure this feature exists to prevent.
        await _client.GetAsync("/api/breeds", Ct); // warm the cache first

        await _client.SendAsync(Ingest("""
            { "breed": "beagle", "observations": [{
                "publisher": "Insuranceopedia",
                "sourceUrl": "https://www.insuranceopedia.com/pet-insurance/beagle-cost",
                "quote": "The cost to purchase a Beagle puppy from a breeder typically ranges between $400 and $1,200",
                "scope": "pet_standard", "figureKind": "range",
                "priceLow": 400, "priceHigh": 1200 }] }
            """), Ct);

        var breeds = await _client.GetFromJsonAsync<JsonElement>("/api/breeds", Ct);
        var beagle = breeds.EnumerateArray().First(b => b.GetProperty("slug").GetString() == "beagle");

        // The seeded legacy Beagle range is $500-$1,200; the researched low is $400.
        Assert.Equal(400, beagle.GetProperty("priceLow").GetInt32());
    }

    [Fact]
    public async Task ListingCollectionRefusesWhenDisabledAndSaysWhy()
    {
        // The shipped default: collection is off because the source's terms restrict
        // automated access, so it must never start on its own.
        var response = await _client.SendAsync(
            Authorised(HttpMethod.Post, "/api/admin/listing-prices"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains("Prices:ListingsEnabled", body);
        Assert.Contains("terms", body);
    }

    [Fact]
    public async Task ListingCollectionRequiresTheSecret()
    {
        var response = await _client.PostAsync("/api/admin/listing-prices", content: null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReaggregationDoesNotRevertAListingDerivedRange()
    {
        // The bug: /api/admin/price-reaggregate derived from observations alone and upserted
        // the result, so the free "re-tune a threshold" operation silently threw away every
        // listing-derived range for any breed that also had observations — replacing better
        // data with worse and saying nothing.
        var store = _factory.Services.GetRequiredService<PriceStore>();
        var job = _factory.Services.GetRequiredService<PriceRefreshJob>();

        // A healthy pooled sample: 24 listings clustered around $2,500.
        await store.AddListingPricesAsync(
            [.. Enumerable.Range(0, 24).Select(i => new ListingPrice(
                BreedSlug: "beagle",
                Price: 2400 + i % 4 * 100,
                SourceHost: "puppies.com",
                ListingRef: $"ref-{i}",
                ListingName: "Beagle - F",
                RetrievedAt: DateTimeOffset.UtcNow,
                RunId: "listings-test"))],
            Ct);

        var thresholds = new PriceThresholds();
        var first = await job.ReaggregateBreedAsync("beagle", thresholds, Ct);
        Assert.Equal(PriceBasis.Listings, first!.Price!.Basis);

        // Re-aggregating must be idempotent, not destructive.
        var again = await job.ReaggregateBreedAsync("beagle", thresholds, Ct);

        Assert.Equal(PriceBasis.Listings, again!.Price!.Basis);
        Assert.Equal(first.Price.PriceLow, again.Price.PriceLow);
        Assert.Equal(first.Price.PriceHigh, again.Price.PriceHigh);
    }

    [Fact]
    public async Task ARangeIsWithdrawnWhenItsEvidenceStopsQualifying()
    {
        // Re-aggregation could only ever *raise* confidence: when a range stopped qualifying
        // and no seed existed to fall back to, nothing was written and the old row stayed
        // live. Irish Wolfhound went on serving a $2,000-$2,100 band after its sample was
        // refused as one seller's litter. A rule that can't withdraw isn't a rule.
        var store = _factory.Services.GetRequiredService<PriceStore>();
        var job = _factory.Services.GetRequiredService<PriceRefreshJob>();
        var thresholds = new PriceThresholds();

        // akita is a dog.ceo breed: no seed range, no observations. Nothing to fall back to.
        ListingPrice Listing(int index, int price) => new(
            BreedSlug: "akita",
            Price: price,
            SourceHost: "puppies.com",
            ListingRef: $"ref-{index}",
            ListingName: "Akita - F",
            RetrievedAt: DateTimeOffset.UtcNow,
            RunId: "listings-test");

        // A healthy spread first, so a range is genuinely published.
        await store.AddListingPricesAsync(
            [.. Enumerable.Range(0, 24).Select(i => Listing(i, 1000 + i * 50))], Ct);
        var published = await job.ReaggregateBreedAsync("akita", thresholds, Ct);
        Assert.Equal(PriceConfidence.Verified, published!.Price!.Confidence);
        Assert.NotNull(await store.FindAsync("akita", Ct));

        // Now flood it with one repeated price, as a litter of thirty would.
        await store.AddListingPricesAsync(
            [.. Enumerable.Range(100, 30).Select(i => Listing(i, 2000))], Ct);
        var after = await job.ReaggregateBreedAsync("akita", thresholds, Ct);

        Assert.Null(after!.Price);
        Assert.Null(await store.FindAsync("akita", Ct));
        Assert.Contains("litter", after.Rationale);

        // The evidence itself is kept — only the derived range is withdrawn.
        Assert.NotEmpty(await store.GetListingPricesAsync("akita", 90, Ct));
    }

    [Fact]
    public async Task IngestedRowsRecordThatAHumanGatheredThem()
    {
        await _client.SendAsync(Ingest("""
            { "breed": "beagle", "observations": [{
                "publisher": "PetMD",
                "sourceUrl": "https://www.petmd.com/dog/breeds/beagle",
                "quote": "Expect to pay $1,000 to $1,400 for a Beagle puppy from a responsible breeder.",
                "scope": "pet_standard", "figureKind": "range",
                "priceLow": 1000, "priceHigh": 1400 }] }
            """), Ct);

        var store = _factory.Services.GetRequiredService<PriceStore>();
        var stored = await store.GetObservationsAsync("beagle", status: null, Ct);
        var ingested = stored.Single(o => o.SourceUrl.Contains("petmd.com"));

        // Provenance is recorded, not hidden: the audit trail says who produced the row.
        Assert.Equal("manual", ingested.Model);
        Assert.StartsWith("manual-", ingested.RunId);
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
