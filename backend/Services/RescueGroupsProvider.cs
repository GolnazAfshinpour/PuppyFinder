using System.Text.Json;
using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <summary>
/// Pulls adoptable-dog listings from the RescueGroups.org v5 public API (JSON:API format).
/// Requires a free API key from https://rescuegroups.org/services/adoptable-pet-data-api/
/// configured under "RescueGroups:ApiKey".
/// </summary>
public sealed class RescueGroupsProvider(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<RescueGroupsProvider> logger) : IListingProvider
{
    public string SourceName => "RescueGroups";

    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(configuration["RescueGroups:ApiKey"]);

    public async Task<IReadOnlyList<Listing>> FetchListingsAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("rescuegroups");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.rescuegroups.org/v5/public/animals/search/available/dogs?limit=25&include=pictures,locations");
        request.Headers.TryAddWithoutValidation("Authorization", configuration["RescueGroups:ApiKey"]);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;

        // JSON:API — related pictures/locations arrive in "included", keyed by type+id.
        var included = new Dictionary<(string Type, string Id), JsonElement>();
        if (root.TryGetProperty("included", out var includedArray))
        {
            foreach (var item in includedArray.EnumerateArray())
            {
                included[(item.GetProperty("type").GetString()!, item.GetProperty("id").GetString()!)] = item;
            }
        }

        var listings = new List<Listing>();
        if (!root.TryGetProperty("data", out var data))
        {
            return listings;
        }

        foreach (var animal in data.EnumerateArray())
        {
            var id = animal.GetProperty("id").GetString()!;
            var attrs = animal.GetProperty("attributes");

            string? city = null, state = null, imageUrl = null;
            if (animal.TryGetProperty("relationships", out var rels))
            {
                if (FirstRelated(rels, "locations") is { } locId &&
                    included.TryGetValue(("locations", locId), out var location))
                {
                    var locAttrs = location.GetProperty("attributes");
                    city = GetString(locAttrs, "city");
                    state = GetString(locAttrs, "state");
                }

                if (FirstRelated(rels, "pictures") is { } picId &&
                    included.TryGetValue(("pictures", picId), out var picture))
                {
                    var picAttrs = picture.GetProperty("attributes");
                    imageUrl = picAttrs.TryGetProperty("large", out var large)
                        ? GetString(large, "url")
                        : GetString(picAttrs, "url");
                }
            }

            listings.Add(new Listing(
                Id: $"rescuegroups-{id}",
                Name: GetString(attrs, "name") ?? "Unnamed",
                Breed: GetString(attrs, "breedPrimary") ?? "Mixed Breed",
                Age: GetString(attrs, "ageGroup"),
                Sex: GetString(attrs, "sex"),
                Description: GetString(attrs, "descriptionText") ?? "",
                City: city ?? "",
                State: state ?? "",
                ImageUrl: imageUrl,
                ListingUrl: GetString(attrs, "url") ?? "https://rescuegroups.org",
                Source: SourceName,
                SourceUrl: "https://rescuegroups.org"));
        }

        logger.LogInformation("RescueGroups returned {Count} listings", listings.Count);
        return listings;
    }

    private static string? FirstRelated(JsonElement relationships, string name) =>
        relationships.TryGetProperty(name, out var rel) &&
        rel.TryGetProperty("data", out var relData) &&
        relData.ValueKind == JsonValueKind.Array &&
        relData.GetArrayLength() > 0
            ? relData[0].GetProperty("id").GetString()
            : null;

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
