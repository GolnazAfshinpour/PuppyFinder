using System.Linq;
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
            int fetched;
            try
            {
                fetched = await FetchPageAsync(page, listings, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException
                                       || cancellationToken.IsCancellationRequested)
            {
                // Keep the pages that did arrive. Letting this propagate discarded every dog
                // already collected, so one slow page cost the whole source — and the aggregator
                // then cached the shortfall for ten minutes.
                logger.LogWarning(
                    "RescueGroups page {Page} failed ({Message}); keeping {Count} listings from "
                    + "earlier pages", page, ex.Message, listings.Count);
                break;
            }

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

            string? city = null, state = null, imageUrl = null, orgUrl = null, orgPhone = null;
            double? orgLat = null, orgLon = null;
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
                if (FirstRelated(rels, "orgs") is { } orgId
                    && included.TryGetValue(("orgs", orgId), out var org))
                {
                    var orgAttrs = org.GetProperty("attributes");
                    if (string.IsNullOrWhiteSpace(state))
                    {
                        city ??= GetString(orgAttrs, "city");
                        state = GetString(orgAttrs, "state");
                    }

                    orgUrl = GetString(orgAttrs, "url");
                    // The organisation's location, which is as precise as this data gets — a dog
                    // in foster care is wherever its foster is, and no feed publishes that.
                    orgLat = GetDouble(orgAttrs, "lat");
                    orgLon = GetDouble(orgAttrs, "lon");
                    // The county feeds put a shelter number on every card; this one had none,
                    // while the phone sat in a relationship we were already fetching.
                    orgPhone = CleanText(GetString(orgAttrs, "phone"));
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
                ListingUrl: DetailUrl(GetString(attrs, "url"), orgUrl, id),
                Source: SourceName,
                SourceUrl: "https://rescuegroups.org",
                // A documented, filterable animal field. Unmapped, every dog from this source
                // was invisible to the size filter. Normalised through the shared bucket map so
                // "X-Large" lands somewhere rather than being dropped.
                Size: SocrataProvider.NormalizeSize(GetString(attrs, "sizeGroup")),
                ContactInfo: orgPhone,
                Latitude: orgLat,
                Longitude: orgLon,
                // The two fields the design doc named as the biggest listing gaps. Both are
                // sparse and both stay honest about it: absent means the rescue did not say,
                // never "no".
                AdoptionFee: NormalizeFee(CleanText(GetString(attrs, "adoptionFeeString"))),
                GoodWithKids: GetBool(attrs, "isKidsOk"),
                GoodWithDogs: GetBool(attrs, "isDogsOk"),
                GoodWithCats: GetBool(attrs, "isCatsOk")));
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

    /// <summary>
    /// The best available link for one animal, degrading in steps rather than collapsing to the
    /// site root.
    ///
    /// <para>
    /// The animal's own <c>url</c> is absent for most records — 207 of 297 — and falling back to
    /// rescuegroups.org meant "Meet Ace" opened a corporate homepage with no way to reach Ace.
    /// There is no canonical per-animal URL to use instead: <c>/animals/detail?AnimalID=</c> at
    /// the site root 404s, the animal's <c>slug</c> 404s there too, and the host in
    /// <c>trackerimageUrl</c> does not resolve. So the rescue's own site does the work, and when
    /// that site is RescueGroups-hosted the same detail path the populated links use still
    /// reaches the individual dog.
    /// </para>
    /// </summary>
    public static string DetailUrl(string? animalUrl, string? orgUrl, string animalId)
    {
        if (!string.IsNullOrWhiteSpace(animalUrl))
        {
            return Https(animalUrl);
        }

        if (!string.IsNullOrWhiteSpace(orgUrl)
            && Uri.TryCreate(Https(orgUrl), UriKind.Absolute, out var org))
        {
            return org.Host.EndsWith(".rescuegroups.org", StringComparison.OrdinalIgnoreCase)
                ? $"{org.GetLeftPart(UriPartial.Authority)}/animals/detail?AnimalID={animalId}"
                : org.ToString();
        }

        // Nothing better exists for this record, rather than a default nobody checked.
        return "https://rescuegroups.org";
    }

    /// <summary>
    /// Makes an adoption fee presentable without inventing anything.
    ///
    /// <para>
    /// Rescues type this field by hand and it shows: one live page returned "$175.00", "175.00",
    /// "375", "795", "150.00" and "500" among the first six. Rendered raw, half the badges lose
    /// their currency symbol and the other half carry cents nobody charges.
    /// </para>
    ///
    /// <para>
    /// So a bare amount is formatted, and <b>anything else is passed through untouched</b> —
    /// "$300-$450", "Varies", "Waived for seniors" are all real answers this field carries, and
    /// parsing them into a number would throw away the cases where the answer is not a number.
    /// </para>
    ///
    /// <para>
    /// A bare zero becomes null rather than "$0". A free adoption is a real thing, but a rescue
    /// that means it writes "Waived" or "Free"; an unedited numeric field defaulting to 0 is far
    /// more likely, and "Adoption fee $0" is a claim on the rescue's behalf that we cannot back.
    /// Null instead sends the reader to the "ask what it is" prompt, which is true either way.
    /// </para>
    /// </summary>
    public static string? NormalizeFee(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();

        // The hand-typed way of leaving the field blank. Live data returned "n/a" three times;
        // rendered as a badge it reads like a fee called "n/a", when what it means is that the
        // rescue did not state one — which the prompt below the contact box already says better.
        if (NonAnswers.Contains(trimmed))
        {
            return null;
        }

        var digits = trimmed.TrimStart('$').Trim();
        if (!decimal.TryParse(
                digits,
                System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowThousands,
                System.Globalization.CultureInfo.InvariantCulture,
                out var amount))
        {
            return trimmed;  // a range, a word, or anything else the rescue chose to write
        }

        if (amount <= 0)
        {
            return null;
        }

        // Whole dollars unless the rescue really did specify cents.
        return amount == decimal.Truncate(amount)
            ? $"${amount:n0}"
            : $"${amount:n2}";
    }

    private static readonly HashSet<string> NonAnswers = new(StringComparer.OrdinalIgnoreCase)
    {
        "n/a", "na", "n.a.", "none", "unknown", "tbd", "tba", "?", "-", "--", "ask", "call",
    };

    /// <summary>Org URLs come back as http; the app is served over https.</summary>
    private static string Https(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? string.Concat("https://", url.AsSpan("http://".Length))
            : url;

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

    /// <summary>
    /// A JSON number, tolerating the string form. Feeds are inconsistent about whether coordinates
    /// are quoted, and a silently-dropped latitude would just look like a rescue with no location.
    /// </summary>
    private static double? GetDouble(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var d) => d,
            JsonValueKind.String when double.TryParse(
                value.GetString(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => null,
        };
    }

    /// <summary>
    /// A JSON boolean, tolerating the string and numeric forms some rescues' records use.
    /// Returns null for a missing or unparseable value — which is a real answer here, not a
    /// failure: the whole point of these fields is that "unknown" is distinct from "no".
    /// </summary>
    private static bool? GetBool(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => value.TryGetInt32(out var n) ? n != 0 : null,
            JsonValueKind.String => value.GetString()?.Trim().ToLowerInvariant() switch
            {
                "true" or "yes" or "1" => true,
                "false" or "no" or "0" => false,
                _ => null,
            },
            _ => null,
        };
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
