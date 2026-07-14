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
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton<IHttpClientFactory>(new OfflineHttpClientFactory());
        });

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
    public async Task Sites_ReturnsAllThirteen_BuyFirstInTrustOrder()
    {
        var sites = await GetJson("/api/sites");
        Assert.Equal(13, sites.GetArrayLength());
        Assert.Equal("gooddog", sites[0].GetProperty("id").GetString());
        Assert.Equal("Buy from breeders", sites[0].GetProperty("kind").GetString());
        Assert.Equal("aspca", sites[12].GetProperty("id").GetString());
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
