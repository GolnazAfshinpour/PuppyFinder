using System.Text.Json;
using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <summary>
/// Field mapping for one Socrata open-data dataset of shelter animals.
/// These government feeds are public JSON endpoints — no API key required.
/// </summary>
public record SocrataDataset(
    string SourceName,
    string SourceUrl,
    string Endpoint,
    string NameField,
    string BreedField,
    string AgeField,
    string SexField,
    string? ImageField,
    string? LinkField,
    string? CityField,
    string DefaultCity,
    string State,
    string? AnimalTypeField,
    string FallbackListingUrl);

/// <summary>
/// Pulls adoptable-dog listings from a government open-data (Socrata) feed.
/// Instantiated once per dataset; always enabled since no credentials are needed.
/// </summary>
public sealed class SocrataProvider(
    SocrataDataset dataset,
    IHttpClientFactory httpClientFactory,
    ILogger<SocrataProvider> logger) : IListingProvider
{
    public string SourceName => dataset.SourceName;

    public bool IsEnabled => true;

    public async Task<IReadOnlyList<Listing>> FetchListingsAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("socrata");
        var payload = await client.GetStringAsync(dataset.Endpoint, cancellationToken);

        using var json = JsonDocument.Parse(payload);
        var listings = new List<Listing>();

        foreach (var row in json.RootElement.EnumerateArray())
        {
            // Some feeds mix species; keep dogs only when the dataset has a type column.
            if (dataset.AnimalTypeField is not null &&
                Get(row, dataset.AnimalTypeField)?.Contains("dog", StringComparison.OrdinalIgnoreCase) is not true)
            {
                continue;
            }

            var name = Get(row, dataset.NameField);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var image = dataset.ImageField is null ? null : GetUrl(row, dataset.ImageField);
            var link = dataset.LinkField is null ? null : GetUrl(row, dataset.LinkField);

            listings.Add(new Listing(
                Id: $"{dataset.SourceName}-{listings.Count}-{name}".Replace(' ', '-').ToLowerInvariant(),
                Name: ToTitleCase(name),
                Breed: ToTitleCase(Get(row, dataset.BreedField)) ?? "Mixed Breed",
                Age: ToTitleCase(Get(row, dataset.AgeField)),
                Sex: NormalizeSex(Get(row, dataset.SexField)),
                Description: $"Adoptable through {dataset.SourceName}.",
                City: ToTitleCase(dataset.CityField is null ? null : Get(row, dataset.CityField)) ?? dataset.DefaultCity,
                State: dataset.State,
                ImageUrl: IsImageUrl(image) ? image : null,
                ListingUrl: link ?? dataset.FallbackListingUrl,
                Source: dataset.SourceName,
                SourceUrl: dataset.SourceUrl));
        }

        logger.LogInformation("{Source} returned {Count} dog listings", dataset.SourceName, listings.Count);
        return listings;
    }

    private static string? Get(JsonElement row, string field) =>
        row.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    // Socrata "URL" columns arrive as {"url": "http://..."} objects rather than plain strings.
    private static string? GetUrl(JsonElement row, string field)
    {
        if (!row.TryGetProperty(field, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Object when value.TryGetProperty("url", out var nested) &&
                                      nested.ValueKind == JsonValueKind.String => nested.GetString(),
            _ => null,
        };
    }

    private static bool IsImageUrl(string? url) =>
        url is not null && Uri.IsWellFormedUriString(url, UriKind.Absolute);

    private static string? NormalizeSex(string? raw) => raw?.Trim().ToUpperInvariant() switch
    {
        null or "" => null,
        "M" => "Male",
        "F" => "Female",
        "N" => "Male (neutered)",
        "S" => "Female (spayed)",
        var other => ToTitleCase(other),
    };

    private static string? ToTitleCase(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? null
            : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(raw.Trim().ToLowerInvariant());
}
