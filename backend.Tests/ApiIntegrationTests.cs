using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PuppyFinder.Api.Tests;

/// <summary>
/// Runs the real HTTP pipeline in-memory with all outbound HTTP stubbed to fail,
/// so the breed catalog deterministically falls back to the curated list and
/// tests never depend on dog.ceo or the open-data feeds being reachable.
/// </summary>
public sealed class OfflineApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Keep alert subscriptions out of the repo's working tree during tests.
        builder.UseSetting("Alerts:StorePath",
            Path.Combine(Path.GetTempPath(), $"puppyfinder-tests-{Guid.NewGuid():N}", "alerts.json"));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton<IHttpClientFactory>(new OfflineHttpClientFactory());
        });
    }

    private sealed class OfflineHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new OfflineHandler());
    }

    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException($"Outbound HTTP is disabled in tests ({request.RequestUri}).");
    }
}

public class ApiIntegrationTests(OfflineApiFactory factory) : IClassFixture<OfflineApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<JsonElement> GetJson(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    // --- /api/sites ---

    [Fact]
    public async Task Sites_ReturnsAllFourteen_BuyFirstInTrustOrder()
    {
        var sites = await GetJson("/api/sites");
        Assert.Equal(14, sites.GetArrayLength());
        Assert.Equal("gooddog", sites[0].GetProperty("id").GetString());
        Assert.Equal("Buy from breeders", sites[0].GetProperty("kind").GetString());
        // Craigslist is last by design — weakest vetting in the catalog.
        Assert.Equal("craigslist", sites[13].GetProperty("id").GetString());
        Assert.False(string.IsNullOrWhiteSpace(sites[13].GetProperty("caution").GetString()));
    }

    [Fact]
    public async Task Sites_WithBreedStateCity_BuildsVerifiedDeepLinks()
    {
        var sites = await GetJson("/api/sites?breed=golden-retriever&state=TX&city=Houston");
        var links = sites.EnumerateArray().ToDictionary(
            s => s.GetProperty("id").GetString()!,
            s => s.GetProperty("linkUrl").GetString()!);

        Assert.Equal("https://marketplace.akc.org/puppies/golden-retriever/texas/houston", links["akc"]);
        Assert.Equal("https://www.gooddog.com/golden-retriever/houston-tx", links["gooddog"]);
        Assert.Equal("https://www.adoptapet.com/s/adopt-a-golden-retriever/texas/houston", links["adoptapet"]);
        Assert.Equal("https://www.puppies.com/find-a-puppy/golden-retriever/texas/houston", links["puppies"]);
        Assert.Equal("https://www.pawrade.com/puppies-for-sale/texas/golden-retriever/", links["pawrade"]);
    }

    [Fact]
    public async Task Sites_ExposeAppliedFilters_ForCardBadges()
    {
        var sites = await GetJson("/api/sites?breed=golden-retriever&state=TX&city=Houston");
        var applied = sites.EnumerateArray().ToDictionary(
            s => s.GetProperty("id").GetString()!,
            s => s.GetProperty("appliedFilters").EnumerateArray().Select(f => f.GetString()).ToArray());

        Assert.Equal(["breed", "state", "city"], applied["akc"]);
        Assert.Equal(["breed"], applied["greenfield"]);
        Assert.Empty(applied["aspca"]);
    }

    [Fact]
    public async Task Sites_UnknownBreed_Returns400()
    {
        var response = await _client.GetAsync("/api/sites?breed=not-a-real-breed");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Sites_RemovedFilterParams_AreIgnoredNotErrors()
    {
        // sex/maxPrice were removed with the filter simplification; stale clients
        // sending them must get normal links, never gender/price query strings.
        var sites = await GetJson("/api/sites?breed=golden-retriever&sex=female&maxPrice=500");
        foreach (var site in sites.EnumerateArray())
        {
            var url = site.GetProperty("linkUrl").GetString()!;
            Assert.DoesNotContain("gender=", url);
            Assert.DoesNotContain("sex=", url);
            Assert.DoesNotContain("price=", url);
        }
    }

    [Fact]
    public async Task Sites_EverySiteHasLabelAndAbsoluteLink()
    {
        var sites = await GetJson("/api/sites?breed=french-bulldog&state=NY");
        foreach (var site in sites.EnumerateArray())
        {
            Assert.StartsWith("https://", site.GetProperty("linkUrl").GetString());
            Assert.False(string.IsNullOrWhiteSpace(site.GetProperty("linkLabel").GetString()));
        }
    }

    // --- /api/breeds ---

    [Fact]
    public async Task Breeds_CuratedBreed_ExposesSizeAndTraits()
    {
        var breeds = await GetJson("/api/breeds");
        var golden = breeds.EnumerateArray().Single(b => b.GetProperty("slug").GetString() == "golden-retriever");
        Assert.Equal("Large", golden.GetProperty("size").GetString());
        Assert.Equal(5, golden.GetProperty("kidFriendly").GetInt32());
        Assert.Equal(2, golden.GetProperty("apartmentFriendly").GetInt32());
        Assert.Equal(4, golden.GetProperty("shedding").GetInt32());
        Assert.Equal("retriever/golden", golden.GetProperty("imagePath").GetString());
    }

    [Fact]
    public async Task Breeds_EveryCuratedBreed_HasADogCeoImagePath()
    {
        var breeds = await GetJson("/api/breeds");
        foreach (var breed in breeds.EnumerateArray())
        {
            var path = breed.GetProperty("imagePath").GetString();
            Assert.False(string.IsNullOrWhiteSpace(path), $"{breed.GetProperty("slug")} has no imagePath");
            Assert.Matches("^[a-z]+(/[a-z]+)?$", path);
        }
    }

    [Fact]
    public async Task Breeds_SortedByDisplayName_AndOfflineFallbackStillServesCurated()
    {
        var breeds = await GetJson("/api/breeds");
        var names = breeds.EnumerateArray().Select(b => b.GetProperty("displayName").GetString()).ToList();
        // dog.ceo stubbed offline → the endpoint must still serve the full curated list.
        Assert.Equal(PuppyFinder.Api.Data.SiteCatalog.Breeds.Count, names.Count);
        Assert.Equal(names.OrderBy(n => n).ToList(), names);
    }

    // --- /api/listings & /api/sources ---

    [Fact]
    public async Task Listings_WithAllFeedsDown_ReturnsEmptyListNotError()
    {
        var listings = await GetJson("/api/listings?breed=golden-retriever&state=TX");
        Assert.Equal(0, listings.GetArrayLength());
    }

    [Fact]
    public async Task Coverage_WithAllFeedsDown_ReturnsEmptyStateList()
    {
        var coverage = await GetJson("/api/coverage");
        Assert.Equal(JsonValueKind.Array, coverage.ValueKind);
        Assert.Equal(0, coverage.GetArrayLength());
    }

    [Fact]
    public async Task Sources_ReportEveryProvider_WithFailureRecorded()
    {
        await GetJson("/api/listings"); // trigger a fetch so failures are recorded
        var sources = await GetJson("/api/sources");
        var byName = sources.EnumerateArray().ToDictionary(s => s.GetProperty("name").GetString()!);

        Assert.Contains("Montgomery County Animal Services", byName.Keys);
        Assert.Contains("King County Pet Adoption", byName.Keys);
        var montgomery = byName["Montgomery County Animal Services"];
        Assert.True(montgomery.GetProperty("enabled").GetBoolean());
        Assert.False(string.IsNullOrEmpty(montgomery.GetProperty("lastError").GetString()));
    }

    // --- /api/alerts ---

    private static object ValidAlert(string email = "golnaz@example.com") => new
    {
        email, breed = "golden-retriever", state = "MD", city = (string?)null, size = "Large",
    };

    [Fact]
    public async Task Alerts_CreateListUnsubscribe_Roundtrip()
    {
        var create = await _client.PostAsJsonAsync("/api/alerts", ValidAlert("roundtrip@example.com"), Json);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement;
        var id = created.GetProperty("id").GetString();
        Assert.False(string.IsNullOrEmpty(id));

        var list = await GetJson("/api/alerts?email=roundtrip@example.com");
        Assert.Contains(list.EnumerateArray(), a => a.GetProperty("id").GetString() == id);

        var unsubscribe = await _client.GetAsync($"/api/alerts/unsubscribe?id={id}&email=roundtrip@example.com");
        Assert.Equal(HttpStatusCode.OK, unsubscribe.StatusCode);
        Assert.Contains("unsubscribed", await unsubscribe.Content.ReadAsStringAsync());

        var after = await GetJson("/api/alerts?email=roundtrip@example.com");
        Assert.Equal(0, after.GetArrayLength());
    }

    [Fact]
    public async Task Alerts_ResubmittingSameSearch_IsIdempotent()
    {
        var first = await _client.PostAsJsonAsync("/api/alerts", ValidAlert("dupe@example.com"), Json);
        var second = await _client.PostAsJsonAsync("/api/alerts", ValidAlert("dupe@example.com"), Json);
        var id1 = JsonDocument.Parse(await first.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString();
        var id2 = JsonDocument.Parse(await second.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString();
        Assert.Equal(id1, id2);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public async Task Alerts_InvalidEmail_Returns400(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/alerts", new { email, breed = (string?)null }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Alerts_UnknownBreed_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/alerts",
            new { email = "ok@example.com", breed = "not-a-breed" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Alerts_DeleteWithWrongEmail_Returns404()
    {
        var create = await _client.PostAsJsonAsync("/api/alerts", ValidAlert("owner@example.com"), Json);
        var id = JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString();

        var wrong = await _client.DeleteAsync($"/api/alerts/{id}?email=attacker@example.com");
        Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);

        var right = await _client.DeleteAsync($"/api/alerts/{id}?email=owner@example.com");
        Assert.Equal(HttpStatusCode.NoContent, right.StatusCode);
    }

    // --- /api/quiz ---

    [Fact]
    public async Task Quiz_ValidAnswers_ReturnsThreeMatches()
    {
        var response = await _client.PostAsJsonAsync("/api/quiz", new
        {
            home = "apartment", activity = "low", kids = "no",
            grooming = "low", size = "small", budget = "any",
        }, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var matches = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(3, matches.GetArrayLength());
        Assert.True(matches[0].GetProperty("matchPercent").GetInt32() > 0);
        Assert.False(string.IsNullOrWhiteSpace(matches[0].GetProperty("imagePath").GetString()));
    }

    [Fact]
    public async Task QuizScores_ReturnsEveryQuizBreed_WithSearchNames()
    {
        var response = await _client.PostAsJsonAsync("/api/quiz/scores", new
        {
            home = "apartment", activity = "low", kids = "no",
            grooming = "low", size = "small", budget = "any",
        }, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var scores = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        // All 20 quiz breeds (teacup aliases excluded), each with a plain search name.
        Assert.Equal(20, scores.GetArrayLength());
        var poodle = scores.EnumerateArray().Single(s => s.GetProperty("slug").GetString() == "poodle");
        Assert.Equal("Poodle", poodle.GetProperty("searchName").GetString()); // "(Standard)" stripped
        Assert.All(scores.EnumerateArray(), s =>
            Assert.InRange(s.GetProperty("matchPercent").GetInt32(), 0, 100));
    }

    [Fact]
    public async Task Quiz_InvalidAnswer_Returns400WithReason()
    {
        var response = await _client.PostAsJsonAsync("/api/quiz", new
        {
            home = "castle", activity = "low", kids = "no",
            grooming = "low", size = "small", budget = "any",
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("home must be", await response.Content.ReadAsStringAsync());
    }
}
