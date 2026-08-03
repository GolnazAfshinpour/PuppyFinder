using System.Net;
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
/// Reads asking prices out of the schema.org structured data on a marketplace's listing
/// pages.
///
/// <para>
/// Parses the published <c>ld+json</c> rather than scraping rendered markup. That is both
/// more honest about what we're reading — a block the site publishes for machine
/// consumption — and far more stable than matching on CSS classes or a bare
/// <c>"price":N</c> regex, which would silently drift the day their markup changes and
/// leave us aggregating whatever numbers happened to match.
/// </para>
///
/// <para>See <see cref="ListingSources"/> for the terms caveat and the two standing
/// limits: structured data only, and never work around an access control.</para>
/// </summary>
public sealed partial class ListingPriceProvider(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<ListingPriceProvider> logger)
{
    /// <summary>Off unless explicitly enabled — see the terms note in <see cref="ListingSources"/>.</summary>
    public bool IsEnabled => configuration.GetValue("Prices:ListingsEnabled", false);

    private int PagesPerBreed => Math.Clamp(configuration.GetValue("Prices:ListingPages", 4), 1, 12);

    private TimeSpan Delay =>
        TimeSpan.FromMilliseconds(Math.Max(250, configuration.GetValue("Prices:ListingDelayMs", 1500)));

    [GeneratedRegex(
        """<script[^>]*type="application/ld\+json"[^>]*>(.*?)</script>""",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex LdJsonBlock();

    public async Task<ListingFetchResult> FetchAsync(
        Breed breed, string runId, CancellationToken ct)
    {
        if (!IsEnabled)
        {
            return new ListingFetchResult(breed.Slug, [], 0, 0,
                "Listing prices are disabled (set Prices:ListingsEnabled=true).");
        }

        var vendorSlug = ListingSources.VendorSlug(breed.Slug);
        var client = httpClientFactory.CreateClient("listings");
        List<ListingPrice> prices = [];
        int seen = 0, mixes = 0;

        for (var page = 1; page <= PagesPerBreed; page++)
        {
            ct.ThrowIfCancellationRequested();

            string html;
            try
            {
                using var response = await client.GetAsync(ListingSources.PageUrl(vendorSlug, page), ct);

                // A 404 on page 1 means the slug is wrong — worth reporting, because it
                // would otherwise look like "this breed has no listings". On a later page
                // it just means we've run out of results.
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return page == 1
                        ? new ListingFetchResult(breed.Slug, [], 0, 0,
                            $"no listing page for '{vendorSlug}' — slug mismatch?")
                        : Done(breed.Slug, prices, seen, mixes);
                }

                // Anything else non-success: stop rather than retry harder. We do not
                // escalate against a site that is declining to serve us.
                if (!response.IsSuccessStatusCode)
                {
                    return prices.Count > 0
                        ? Done(breed.Slug, prices, seen, mixes)
                        : new ListingFetchResult(breed.Slug, [], seen, mixes,
                            $"{(int)response.StatusCode} from {ListingSources.Host}");
                }

                html = await response.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return prices.Count > 0
                    ? Done(breed.Slug, prices, seen, mixes)
                    : new ListingFetchResult(breed.Slug, [], seen, mixes, ex.Message);
            }

            var found = Extract(html, breed, runId).ToList();
            seen += found.Count;

            var expectedName = ListingSources.VendorName(breed.Slug, breed.DisplayName);
            var purebred = found
                .Where(l => ListingSources.IsPurebredTitle(l.ListingName, expectedName))
                .ToList();
            mixes += found.Count - purebred.Count;
            prices.AddRange(purebred);

            // An empty page means the end of the results, not a failure.
            if (found.Count == 0)
            {
                break;
            }

            if (page < PagesPerBreed)
            {
                await Task.Delay(Delay, ct);
            }
        }

        return Done(breed.Slug, prices, seen, mixes);
    }

    private ListingFetchResult Done(string slug, List<ListingPrice> prices, int seen, int mixes)
    {
        logger.LogInformation(
            "Listings for {Breed}: {Kept} purebred prices from {Seen} results ({Mixes} crossbreeds dropped)",
            slug, prices.Count, seen, mixes);
        return new ListingFetchResult(slug, prices, seen, mixes, null);
    }

    /// <summary>
    /// Pulls Product/Offer prices out of every ld+json block on the page. Malformed blocks
    /// are skipped rather than failing the fetch — one bad block must not lose a page.
    /// </summary>
    private static IEnumerable<ListingPrice> Extract(string html, Breed breed, string runId)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var match in LdJsonBlock().Matches(html).Cast<Match>())
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(match.Groups[1].Value);
            }
            catch (JsonException)
            {
                continue;
            }

            using (document)
            {
                foreach (var node in Nodes(document.RootElement))
                {
                    if (Text(node, "@type") != "ItemList"
                        || !node.TryGetProperty("itemListElement", out var elements)
                        || elements.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var element in elements.EnumerateArray())
                    {
                        if (!element.TryGetProperty("item", out var item)
                            || !item.TryGetProperty("offers", out var offer)
                            || !offer.TryGetProperty("price", out var price)
                            || !price.TryGetInt32(out var amount))
                        {
                            continue;
                        }

                        // Guard against a non-USD listing being read as dollars.
                        if (Text(offer, "priceCurrency") is { } currency
                            && !currency.Equals("USD", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var reference = Text(element, "url")
                            ?? Text(item, "@id")
                            ?? Text(item, "image");
                        if (reference is null)
                        {
                            // Without an identity we can't dedupe, and a price counted
                            // twice is worse than one not counted at all.
                            continue;
                        }

                        yield return new ListingPrice(
                            BreedSlug: breed.Slug,
                            Price: amount,
                            SourceHost: ListingSources.Host,
                            ListingRef: reference,
                            ListingName: Text(item, "name") ?? "",
                            RetrievedAt: now,
                            RunId: runId);
                    }
                }
            }
        }
    }

    /// <summary>ld+json is a bare object, an array, or an @graph. Flatten all three.</summary>
    private static IEnumerable<JsonElement> Nodes(JsonElement root)
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

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
