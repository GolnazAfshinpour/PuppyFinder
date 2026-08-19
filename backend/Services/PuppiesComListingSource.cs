using System.Net;
using System.Text.Json;
using PuppyFinder.Api.Data;
using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <summary>
/// Reads asking prices out of the schema.org ItemList on puppies.com breed-search pages.
///
/// <para>
/// <b>Off by its own flag, on top of the master switch.</b> This site's terms forbid
/// automated collection (see <see cref="ListingSources"/> and docs/SOURCES.md), and
/// collection was paused by owner decision when the repository went public. Re-enabling
/// requires <c>Prices:PuppiesComEnabled=true</c> <i>in addition to</i>
/// <c>Prices:ListingsEnabled=true</c>, so turning listing collection back on for the
/// sources with no terms conflict can never silently resume this one — that is a fresh
/// decision, and the second flag is what makes it one.
/// </para>
/// </summary>
public sealed class PuppiesComListingSource(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<PuppiesComListingSource> logger) : IListingPriceSource
{
    public string Host => ListingSources.Host;

    public bool IsEnabled =>
        configuration.GetValue("Prices:ListingsEnabled", false)
        && configuration.GetValue("Prices:PuppiesComEnabled", false);

    /// <summary>
    /// Curated breeds plus the ones a probe measured the vendor to carry — every request
    /// here costs a page fetch against a site whose terms we would already be stretching,
    /// so "try everything" is not on the table.
    /// </summary>
    public bool Carries(string breedSlug) =>
        SiteCatalog.IsCurated(breedSlug) || ListingSources.IsKnownToVendor(breedSlug);

    private int PagesPerBreed => Math.Clamp(configuration.GetValue("Prices:ListingPages", 4), 1, 12);

    private TimeSpan Delay =>
        TimeSpan.FromMilliseconds(Math.Max(250, configuration.GetValue("Prices:ListingDelayMs", 1500)));

    public async Task<ListingFetchResult> FetchAsync(
        Breed breed, string runId, CancellationToken ct)
    {
        if (!IsEnabled)
        {
            return new ListingFetchResult(breed.Slug, [], 0, 0,
                "puppies.com collection is disabled (Prices:PuppiesComEnabled) — its terms "
                + "restrict automated collection.");
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
    /// Pulls ItemList/Offer prices out of every ld+json block on the page. Malformed blocks
    /// are skipped rather than failing the fetch — one bad block must not lose a page.
    /// </summary>
    private static IEnumerable<ListingPrice> Extract(string html, Breed breed, string runId)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var document in LdJson.Documents(html))
        {
            using (document)
            {
                foreach (var node in LdJson.Nodes(document.RootElement))
                {
                    if (!LdJson.IsType(node, "ItemList")
                        || !node.TryGetProperty("itemListElement", out var elements)
                        || elements.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var element in elements.EnumerateArray())
                    {
                        if (!element.TryGetProperty("item", out var item)
                            || !item.TryGetProperty("offers", out var offer)
                            || LdJson.Price(offer) is not { } amount)
                        {
                            continue;
                        }

                        // Guard against a non-USD listing being read as dollars.
                        if (LdJson.Text(offer, "priceCurrency") is { } currency
                            && !currency.Equals("USD", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var reference = LdJson.Text(element, "url")
                            ?? LdJson.Text(item, "@id")
                            ?? LdJson.Text(item, "image");
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
                            ListingName: LdJson.Text(item, "name") ?? "",
                            RetrievedAt: now,
                            RunId: runId);
                    }
                }
            }
        }
    }
}
