using System.Text.Json;
using System.Text.RegularExpressions;
using PuppyFinder.Api.Data;
using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <summary>Outcome of fetching one breed's listings.</summary>
public record ListingFetchResult(
    string BreedSlug,
    IReadOnlyList<ListingPrice> Prices,
    int SeenTotal,
    int DroppedMixes,
    string? Error)
{
    public bool Succeeded => Error is null;
}

/// <summary>
/// One marketplace we read asking prices from. Each source carries its own enablement,
/// because the sites' positions differ: puppies.com's terms forbid automated collection
/// (its flag defaults off and stays off), while keystonepuppies.com publishes no terms of
/// use at all and its robots.txt welcomes crawlers.
/// </summary>
public interface IListingPriceSource
{
    /// <summary>The host this source reads — recorded on every price row it produces.</summary>
    string Host { get; }

    bool IsEnabled { get; }

    /// <summary>
    /// Whether this source is worth asking about a breed at all. Sources whose marginal
    /// request cost per breed is zero (a memoized site-wide index) return true and let the
    /// data answer; sources that pay a request per breed limit themselves to breeds the
    /// vendor was measured to carry.
    /// </summary>
    bool Carries(string breedSlug);

    Task<ListingFetchResult> FetchAsync(Breed breed, string runId, CancellationToken ct);
}

/// <summary>
/// Fans one breed's listing-price fetch out to every enabled source and merges the
/// samples. Multiple hosts per breed is deliberately better than one: cross-host
/// corroboration is the property the editorial pipeline requires of its sources, and the
/// original single-host design never had it.
///
/// <para>
/// Parses only the published <c>ld+json</c> on every source (see <see cref="LdJson"/>) —
/// a block the site publishes for machine consumption, and far more stable than matching
/// CSS classes, which would silently drift the day the markup changes.
/// </para>
/// </summary>
public sealed class ListingPriceProvider(IEnumerable<IListingPriceSource> sources)
{
    public bool IsEnabled => sources.Any(s => s.IsEnabled);

    public bool Carries(string breedSlug) => sources.Any(s => s.IsEnabled && s.Carries(breedSlug));

    public async Task<ListingFetchResult> FetchAsync(Breed breed, string runId, CancellationToken ct)
    {
        var active = sources.Where(s => s.IsEnabled && s.Carries(breed.Slug)).ToList();
        if (active.Count == 0)
        {
            return new ListingFetchResult(breed.Slug, [], 0, 0,
                "no enabled listing source carries this breed");
        }

        List<ListingPrice> prices = [];
        List<string> errors = [];
        int seen = 0, mixes = 0;

        foreach (var source in active)
        {
            ct.ThrowIfCancellationRequested();
            var result = await source.FetchAsync(breed, runId, ct);
            seen += result.SeenTotal;
            mixes += result.DroppedMixes;
            if (result.Succeeded)
            {
                prices.AddRange(result.Prices);
            }
            else
            {
                errors.Add($"{source.Host}: {result.Error}");
            }
        }

        // One host failing must not discard another's sample; only a run where every host
        // failed reports as an error, naming each host's reason.
        return errors.Count == active.Count
            ? new ListingFetchResult(breed.Slug, [], seen, mixes, string.Join("; ", errors))
            : new ListingFetchResult(breed.Slug, prices, seen, mixes, null);
    }
}

/// <summary>
/// Shared ld+json plumbing for the listing sources: find the blocks, flatten the shapes,
/// read the fields. Tolerates the malformation observed in the wild — keystonepuppies.com
/// leaves raw newlines inside JSON strings, which is invalid JSON a strict parse rejects
/// wholesale, losing a price over a formatting quirk in the description beside it.
/// </summary>
public static partial class LdJson
{
    // The type attribute's quoting varies by site: puppies.com double-quotes it,
    // keystonepuppies.com single-quotes it — and requiring one style silently read the
    // other's pages as having no structured data at all.
    [GeneratedRegex(
        """<script[^>]*type\s*=\s*["']application/ld\+json["'][^>]*>(.*?)</script>""",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Block();

    /// <summary>Every parseable ld+json document on the page; malformed blocks are repaired
    /// where possible and skipped where not — one bad block must not lose a page.</summary>
    public static IEnumerable<JsonDocument> Documents(string html)
    {
        foreach (var match in Block().Matches(html).Cast<Match>())
        {
            JsonDocument? document = null;
            try
            {
                document = JsonDocument.Parse(match.Groups[1].Value);
            }
            catch (JsonException)
            {
                // Second attempt with control characters collapsed to spaces: legal JSON
                // never needs them outside strings, and inside strings they are the
                // malformation this repairs. Anything still unparseable is skipped.
                try
                {
                    document = JsonDocument.Parse(
                        ControlCharacters().Replace(match.Groups[1].Value, " "));
                }
                catch (JsonException)
                {
                    // skip this block
                }
            }

            if (document is not null)
            {
                yield return document;
            }
        }
    }

    [GeneratedRegex(@"[\u0000-\u001F]")]
    private static partial Regex ControlCharacters();

    /// <summary>ld+json is a bare object, an array, or an @graph. Flatten all three.</summary>
    public static IEnumerable<JsonElement> Nodes(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                foreach (var nested in Nodes(item))
                {
                    yield return nested;
                }
            }
            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        yield return root;

        if (root.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in graph.EnumerateArray())
            {
                foreach (var nested in Nodes(item))
                {
                    yield return nested;
                }
            }
        }
    }

    public static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>Whether a node's @type is (or includes) the given schema.org type.</summary>
    public static bool IsType(JsonElement node, string type)
    {
        if (!node.TryGetProperty("@type", out var t))
        {
            return false;
        }

        return t.ValueKind switch
        {
            JsonValueKind.String => type.Equals(t.GetString(), StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Array => t.EnumerateArray().Any(x =>
                x.ValueKind == JsonValueKind.String
                && type.Equals(x.GetString(), StringComparison.OrdinalIgnoreCase)),
            _ => false,
        };
    }

    /// <summary>A whole-dollar price, tolerating the number and string forms ("1200.00").</summary>
    public static int? Price(JsonElement offer)
    {
        if (!offer.TryGetProperty("price", out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var d) => (int)Math.Round(d),
            JsonValueKind.String when decimal.TryParse(
                value.GetString(),
                System.Globalization.NumberStyles.AllowDecimalPoint | System.Globalization.NumberStyles.AllowThousands,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed) => (int)Math.Round(parsed),
            _ => null,
        };
    }
}
