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
    string FallbackListingUrl,
    string? SizeField = null,   // e.g. Montgomery's "petsize" (SMALL/MED/LARGE)
    string? MemoField = null);  // free-text bio; also mined for weight when SizeField is absent

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
            var memo = dataset.MemoField is null ? null : Get(row, dataset.MemoField);

            listings.Add(new Listing(
                Id: $"{dataset.SourceName}-{listings.Count}-{name}".Replace(' ', '-').ToLowerInvariant(),
                Name: ToTitleCase(CleanName(name)),
                Breed: ExpandBreedAbbreviations(ToTitleCase(Get(row, dataset.BreedField))) ?? "Mixed Breed",
                Age: ToTitleCase(Get(row, dataset.AgeField)),
                Sex: NormalizeSex(Get(row, dataset.SexField)),
                // Real shelter bios only (King County's memo); no generated filler —
                // the UI already attributes the source.
                Description: Truncate(CleanMemo(memo), 240),
                City: ToTitleCase(dataset.CityField is null ? null : Get(row, dataset.CityField)) ?? dataset.DefaultCity,
                State: dataset.State,
                ImageUrl: IsImageUrl(image) ? UpgradeToHttps(image!) : null,
                // Best per-animal page wins: a PetHarbor detail page derived from the
                // image ID, then the feed's own link (sometimes just a generic info
                // page), then the shelter's adoption page. Feeds have shipped dead
                // fallback links before — Montgomery's old adoptdog.html 404s.
                ListingUrl: PetHarborDetailUrl(image) ?? link ?? dataset.FallbackListingUrl,
                Source: dataset.SourceName,
                SourceUrl: dataset.SourceUrl,
                Size: NormalizeSize(dataset.SizeField is null ? null : Get(row, dataset.SizeField))
                      ?? SizeFromWeightText(memo)));
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

    /// <summary>
    /// PetHarbor image URLs (get_image.asp?ID=A123&LOCATION=XYZ) carry the animal's
    /// shelter ID, which maps 1:1 to its public detail page (pet.asp?uaid=XYZ.A123) —
    /// verified July 2026. Param casing varies per shelter, so match case-insensitively.
    /// </summary>
    public static string? PetHarborDetailUrl(string? imageUrl)
    {
        if (imageUrl is null ||
            !Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) ||
            !(uri.Host.Equals("petharbor.com", StringComparison.OrdinalIgnoreCase) ||
              uri.Host.EndsWith(".petharbor.com", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        string? id = null, location = null;
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2) continue;
            if (parts[0].Equals("id", StringComparison.OrdinalIgnoreCase)) id = parts[1];
            if (parts[0].Equals("location", StringComparison.OrdinalIgnoreCase)) location = parts[1];
        }

        return id is null || location is null
            ? null
            : $"https://petharbor.com/pet.asp?uaid={location.ToUpperInvariant()}.{id.ToUpperInvariant()}";
    }

    // Some feeds (Montgomery County) publish http:// image URLs, which browsers
    // block as mixed content on an https page. PetHarbor serves the same images
    // over https (verified July 2026).
    private static string UpgradeToHttps(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? string.Concat("https://", url.AsSpan("http://".Length))
            : url;

    /// <summary>
    /// Shelters prefix names with bookkeeping markers ("*BELLARINA" = needs review,
    /// etc.) that mean nothing to adopters — strip anything before the first letter.
    /// </summary>
    public static string CleanName(string name)
    {
        var start = 0;
        while (start < name.Length && !char.IsLetter(name[start]))
        {
            start++;
        }

        var cleaned = name[start..].Trim();
        return cleaned.Length > 0 ? cleaned : name.Trim();
    }

    // PetHarbor truncates breed words to fixed widths ("LABRADOR RETR",
    // "GERM SHEPHERD"). Expand the common, unambiguous ones per word.
    private static readonly Dictionary<string, string> BreedWordFixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Retr"] = "Retriever", ["Terr"] = "Terrier", ["Ter"] = "Terrier",
        ["Shep"] = "Shepherd", ["Germ"] = "German", ["Aust"] = "Australian",
        ["Span"] = "Spaniel", ["Eng"] = "English", ["Amer"] = "American", ["Am"] = "American",
        ["Shetld"] = "Shetland",
    };

    public static string? ExpandBreedAbbreviations(string? breed) =>
        breed is null
            ? null
            : string.Join(' ', breed.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => BreedWordFixes.GetValueOrDefault(word, word)));

    // Buckets match the app's Size filter. Values verified against the live feeds
    // (Montgomery petsize: SMALL/MED/LARGE; KITTE is cats and never reaches dogs).
    public static string? NormalizeSize(string? raw) => raw?.Trim().ToUpperInvariant() switch
    {
        "TOY" or "SM" or "SMALL" => "Small",
        "MED" or "MEDIUM" => "Medium",
        "LG" or "LARGE" or "X-LRG" or "XLRG" => "Large",
        _ => null,
    };

    /// <summary>
    /// King County has no size field, but its bios state the weight ("… 92.0 lbs …").
    /// Derive the bucket from the first weight mentioned; null when none appears.
    /// </summary>
    public static string? SizeFromWeightText(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            text, @"(\d+(?:\.\d+)?)\s*(?:lbs?|pounds)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success || !double.TryParse(match.Groups[1].Value, out var pounds))
        {
            return null;
        }

        return pounds switch
        {
            < 25 => "Small",
            <= 60 => "Medium",
            _ => "Large",
        };
    }

    // King County memos are "</p>"-separated: metadata blocks (Received on / Description /
    // Age / Adoption Fee / Current Location) followed by the actual bio. The card already
    // shows the metadata as structured fields, so keep only the bio text.
    private static readonly string[] MemoMetadataPrefixes =
        ["Received on:", "Description:", "Age:", "Adoption Fee:", "Current Location:"];

    public static string CleanMemo(string? memo)
    {
        if (string.IsNullOrWhiteSpace(memo))
        {
            return "";
        }

        var segments = memo.Split("</p>", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var lastMetadata = Array.FindLastIndex(segments, s =>
            MemoMetadataPrefixes.Any(p => s.StartsWith(p, StringComparison.OrdinalIgnoreCase)));
        var bio = string.Join(' ', segments.Skip(lastMetadata + 1));

        // Drop any residual markup and collapse the leftover whitespace.
        bio = System.Text.RegularExpressions.Regex.Replace(bio, "<[^>]*>", " ");
        return System.Text.RegularExpressions.Regex.Replace(bio, @"\s+", " ").Trim();
    }

    private static string Truncate(string? text, int max)
    {
        var trimmed = text?.Trim() ?? "";
        return trimmed.Length <= max ? trimmed : trimmed[..max].TrimEnd() + "…";
    }

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
