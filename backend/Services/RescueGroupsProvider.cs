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

    /// <summary>
    /// Results per request. 25 was an arbitrary starter value and coverage is the whole point of
    /// this source, but the API terms ask callers to avoid flooding it and document 429 as a
    /// response, so this stays modest rather than maximal.
    /// </summary>
    private const int PageSize = 100;

    /// <summary>How many pages to walk at most, so a paging bug cannot loop indefinitely.</summary>
    private const int MaxPages = 3;

    public async Task<IReadOnlyList<Listing>> FetchListingsAsync(CancellationToken cancellationToken)
    {
        var listings = new List<Listing>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var fetched = await FetchPageAsync(page, listings, cancellationToken);
            if (fetched < PageSize)
            {
                break;  // short page means there is nothing after it
            }
        }

        logger.LogInformation("RescueGroups returned {Count} listings", listings.Count);
        return listings;
    }

    /// <returns>How many records the page contained, before filtering.</returns>
    private async Task<int> FetchPageAsync(
        int page, List<Listing> listings, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("rescuegroups");

        // orgs is included for its city/state: RescueGroups' own examples put location on the
        // org, an animal's own `locations` relationship is frequently absent, and the API omits
        // null relationships rather than returning them empty.
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.rescuegroups.org/v5/public/animals/search/available/dogs"
                + $"?limit={PageSize}&page={page}&include=pictures,locations,orgs");
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

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var seen = 0;
        foreach (var animal in data.EnumerateArray())
        {
            seen++;
            var id = animal.GetProperty("id").GetString()!;
            var attrs = animal.GetProperty("attributes");

            // Already going to a home. Listing it as available wastes the reader's time and the
            // rescue's, which is the opposite of what this integration is for.
            if (attrs.TryGetProperty("isAdoptionPending", out var pending)
                && pending.ValueKind == JsonValueKind.True)
            {
                continue;
            }

            var name = CleanText(GetString(attrs, "name")) ?? "Unnamed";
            var description = CleanText(GetString(attrs, "descriptionText")) ?? "";
            if (IsPlaceholder(name, description))
            {
                continue;
            }

            // A shelter ID is a missing name that happens to be a non-empty string. Cards read
            // "Meet A030173" otherwise, which tells an adopter nothing; the breed, age, size and
            // location beside it carry the actual information.
            if (LooksLikeAnIdCode(name))
            {
                name = "Unnamed";
            }

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

                // Fall back to the organisation's own address. Measured: 9 of the first 25 dogs
                // had no locations relationship at all, and a dog with no state cannot be
                // reached by the state filter, which is one of the primary controls.
                if (string.IsNullOrWhiteSpace(state)
                    && FirstRelated(rels, "orgs") is { } orgId
                    && included.TryGetValue(("orgs", orgId), out var org))
                {
                    var orgAttrs = org.GetProperty("attributes");
                    city ??= GetString(orgAttrs, "city");
                    state = GetString(orgAttrs, "state");
                }

                // Casing varies by rescue — CA and Ca, TX and Tx, OK and ok all appear in one
                // response. Harmless today, because the filter compares case-insensitively, but
                // only until something groups on the raw value.
                if (state?.Length == 2)
                {
                    state = state.ToUpperInvariant();
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
                Name: name,
                Breed: GetString(attrs, "breedPrimary") ?? "Mixed Breed",
                Age: GetString(attrs, "ageGroup"),
                Sex: GetString(attrs, "sex"),
                Description: description,
                City: city ?? "",
                State: state ?? "",
                ImageUrl: imageUrl,
                ListingUrl: GetString(attrs, "url") ?? "https://rescuegroups.org",
                Source: SourceName,
                SourceUrl: "https://rescuegroups.org",
                // A documented, filterable animal field. Unmapped, every dog from this source
                // was invisible to the size filter. Normalised through the shared bucket map so
                // "X-Large" lands somewhere rather than being dropped.
                Size: SocrataProvider.NormalizeSize(GetString(attrs, "sizeGroup"))));
        }

        return seen;
    }

    /// <summary>
    /// Whether a record is an application placeholder rather than an animal. Several rescues
    /// publish one — no photo, or the rescue's logo, and a name that says what it is.
    ///
    /// <para>
    /// There is no flag for this, so it is matched on wording, and the wording varies: the live
    /// API returned "1Dog Not Listed", "-A Dog Not Yet Posted-" and
    /// "Foster - Apply to be a Foster Home" in the same fetch. A first version of this check
    /// matched only "not listed" and let the other two through, so the description is consulted
    /// as well — that is where these entries explain themselves.
    /// </para>
    /// </summary>
    private static bool IsPlaceholder(string name, string description) =>
        PlaceholderName.IsMatch(name)
        || name.Contains("apply", StringComparison.OrdinalIgnoreCase)
        || description.Contains("trying to apply for a dog", StringComparison.OrdinalIgnoreCase)
        || description.Contains("dog that is not listed", StringComparison.OrdinalIgnoreCase);

    private static readonly System.Text.RegularExpressions.Regex PlaceholderName = new(
        @"not\s+(yet\s+)?(listed|posted|available)|unlisted\s+dog",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase
            | System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>A bare shelter reference such as "A030173" — an identifier, not a name.</summary>
    private static bool LooksLikeAnIdCode(string name) =>
        System.Text.RegularExpressions.Regex.IsMatch(name.Trim(), @"^[A-Za-z]{0,2}[-\s]?\d{4,}$");

    private static string? FirstRelated(JsonElement relationships, string name) =>
        relationships.TryGetProperty(name, out var rel) &&
        rel.TryGetProperty("data", out var relData) &&
        relData.ValueKind == JsonValueKind.Array &&
        relData.GetArrayLength() > 0
            ? relData[0].GetProperty("id").GetString()
            : null;

    /// <summary>
    /// Turns a RescueGroups text field into the plain text the rest of the app assumes.
    ///
    /// <para>
    /// Their editors store HTML source, so bios arrive with entities intact — 193 of the first
    /// 297 did, and the page showed "I&amp;rsquo;ve been at the Orangeburg SPCA" verbatim. Also
    /// normalises the non-breaking spaces that come with them, collapses the runs of blank lines
    /// their editor leaves behind, and trims: 64 descriptions had leading or trailing whitespace.
    /// </para>
    /// </summary>
    private static string? CleanText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        // Decoded up to twice, because some rescues' text is double-encoded: one bio arrived
        // as "&amp;#39;" and a single pass left a literal "&#39;" on the card. Bounded at two
        // so a pathological value cannot loop, and safe only because these fields render as
        // escaped text — if anything ever renders them as HTML this needs sanitising instead.
        var decoded = raw;
        for (var pass = 0; pass < 2; pass++)
        {
            var next = System.Net.WebUtility.HtmlDecode(decoded);
            if (next == decoded)
            {
                break;
            }

            decoded = next;
        }

        decoded = decoded
            .Replace('\u00a0', ' ')   // &nbsp; decodes to this, and it wraps badly
            .Replace("\r\n", "\n");
        // Three or more newlines become two: a paragraph break, not a gap.
        decoded = System.Text.RegularExpressions.Regex.Replace(decoded, @"\n{3,}", "\n\n");
        // Runs of spaces and tabs collapse, without touching the line breaks.
        decoded = System.Text.RegularExpressions.Regex.Replace(decoded, @"[ \t]{2,}", " ");
        var trimmed = decoded.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
