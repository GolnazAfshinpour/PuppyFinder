using System.Net.Http.Headers;
using System.Text.Json;
using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <summary>
/// Pulls adoptable-dog listings from the official Petfinder v2 API.
/// Requires free credentials from https://www.petfinder.com/developers/
/// configured under "Petfinder:ApiKey" and "Petfinder:ApiSecret".
/// </summary>
public sealed class PetfinderProvider(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<PetfinderProvider> logger) : IListingProvider
{
    public string SourceName => "Petfinder";

    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(configuration["Petfinder:ApiKey"]) &&
        !string.IsNullOrWhiteSpace(configuration["Petfinder:ApiSecret"]);

    public async Task<IReadOnlyList<Listing>> FetchListingsAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("petfinder");

        using var tokenResponse = await client.PostAsync(
            "https://api.petfinder.com/v2/oauth2/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = configuration["Petfinder:ApiKey"]!,
                ["client_secret"] = configuration["Petfinder:ApiSecret"]!,
            }),
            cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();

        using var tokenJson = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken));
        var token = tokenJson.RootElement.GetProperty("access_token").GetString();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.petfinder.com/v2/animals?type=dog&status=adoptable&sort=recent&limit=24");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var listings = new List<Listing>();

        foreach (var animal in json.RootElement.GetProperty("animals").EnumerateArray())
        {
            var contact = animal.GetProperty("contact").GetProperty("address");
            var photos = animal.GetProperty("photos");

            listings.Add(new Listing(
                Id: $"petfinder-{animal.GetProperty("id").GetInt64()}",
                Name: animal.GetProperty("name").GetString() ?? "Unnamed",
                Breed: animal.GetProperty("breeds").GetProperty("primary").GetString() ?? "Mixed Breed",
                Age: animal.GetProperty("age").GetString(),
                Sex: animal.GetProperty("gender").GetString(),
                Description: animal.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                City: contact.GetProperty("city").GetString() ?? "",
                State: contact.GetProperty("state").GetString() ?? "",
                ImageUrl: photos.GetArrayLength() > 0
                    ? photos[0].GetProperty("medium").GetString()
                    : null,
                ListingUrl: animal.GetProperty("url").GetString() ?? "https://www.petfinder.com",
                Source: SourceName,
                SourceUrl: "https://www.petfinder.com"));
        }

        logger.LogInformation("Petfinder returned {Count} listings", listings.Count);
        return listings;
    }
}
