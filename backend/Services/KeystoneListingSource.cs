using System.Text.RegularExpressions;
using PuppyFinder.Api.Data;
using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <summary>
/// Reads asking prices from keystonepuppies.com — verified August 2026: per-puppy prices
/// in schema.org Product/Offer ld+json on every detail page, a permissive robots.txt
/// asking only <c>Crawl-delay: 2</c>, and <b>no terms of use anywhere on the site</b>
/// (a disclaimer and a privacy policy are the whole legal footer). Unlike puppies.com,
/// there is no terms conflict to document — which is why this source defaults on when
/// listing collection is enabled at all.
///
/// <para>
/// The whole inventory (~400 puppies) sits on one grid page whose detail URLs carry the
/// breed slug (<c>/puppy/{breed}-puppies-for-sale/{name}</c>), so the grid is fetched
/// once per run and memoized; each breed then costs only its own detail pages. The breed
/// is confirmed against the Product's <c>brand.name</c> before a price counts — the URL
/// bucket selects, the structured data decides.
/// </para>
/// </summary>
public sealed class KeystoneListingSource(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<KeystoneListingSource> logger) : IListingPriceSource
{
    public string Host => "keystonepuppies.com";

    private const string GridUrl = "https://www.keystonepuppies.com/find-my-puppy";

    public bool IsEnabled =>
        configuration.GetValue("Prices:ListingsEnabled", false)
        && configuration.GetValue("Prices:KeystoneEnabled", true);

    /// <summary>
    /// Every breed: the grid is memoized per run, so a breed the vendor doesn't carry
    /// costs a dictionary miss, not a request. This is what lets the run measure the
    /// vendor's real inventory instead of guessing it in a hardcoded list.
    /// </summary>
    public bool Carries(string breedSlug) => true;

    /// <summary>Detail pages fetched per breed. 25 clears the aggregator's 20-listing
    /// minimum with headroom for parse misses, without walking a popular breed's whole
    /// inventory.</summary>
    private int DetailsPerBreed =>
        Math.Clamp(configuration.GetValue("Prices:ListingDetailsPerBreed", 25), 1, 60);

    /// <summary>Never below 2s: their robots.txt asks for <c>Crawl-delay: 2</c>.</summary>
    private TimeSpan Delay => TimeSpan.FromMilliseconds(
        Math.Max(2000, configuration.GetValue("Prices:ListingDelayMs", 1500)));

    /// <summary>
    /// Our slug to theirs, only where they differ; read off their own breed-page URLs,
    /// not guessed. A wrong bucket is caught downstream by the brand-name gate, so an
    /// unmapped breed fails safe as "no listings" rather than as another breed's prices.
    /// </summary>
    private static readonly Dictionary<string, string> SlugOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        // Read off the live grid's 54 buckets (Aug 2026): they put the size variety after
        // the breed ("poodle-mini") and the corgi's nationality first.
        ["miniature-poodle"] = "poodle-mini",
        ["toy-poodle"] = "poodle-toy",
        ["miniature-schnauzer"] = "schnauzer-mini",
        ["pembroke"] = "welsh-corgi-pembroke",
        ["pembroke-welsh-corgi"] = "welsh-corgi-pembroke",
        ["bulldog"] = "english-bulldog",
        ["shepherd-australian"] = "australian-shepherd",
        ["german-shepherd"] = "german-shepherd",  // theirs, unlike puppies.com's "-dog" form
    };

    public static string KeystoneSlug(string breedSlug) =>
        SlugOverrides.GetValueOrDefault(breedSlug, breedSlug);

    /// <summary>
    /// The brand name Keystone gives a breed, where it differs from our display name —
    /// read off live Product blocks, not guessed, because a mismatch fails silently as
    /// "every listing was a crossbreed". The first full run dropped all 15 Yorkies
    /// ("Yorkie") and all 19 Mini Poodles ("Mini Poodles", plural) exactly that way.
    /// </summary>
    private static readonly Dictionary<string, string> BrandNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["miniature-poodle"] = "Mini Poodles",
        ["yorkshire-terrier"] = "Yorkie",
        ["bulldog"] = "English Bulldog",
        ["english-bulldog"] = "English Bulldog",
    };

    public static string ExpectedBrand(string breedSlug, string displayName) =>
        BrandNames.GetValueOrDefault(breedSlug, displayName);

    /// <summary>The site-wide grid, fetched once per run. Failure is memoized too, so a
    /// grid outage costs one failed request rather than one per breed.</summary>
    private (string RunId, ILookup<string, string>? Buckets, string? Error)? _grid;

    public async Task<ListingFetchResult> FetchAsync(Breed breed, string runId, CancellationToken ct)
    {
        if (!IsEnabled)
        {
            return new ListingFetchResult(breed.Slug, [], 0, 0,
                "keystonepuppies.com collection is disabled.");
        }

        var client = httpClientFactory.CreateClient("listings");

        if (_grid is not { } grid || grid.RunId != runId)
        {
            try
            {
                using var response = await client.GetAsync(GridUrl, ct);
                response.EnsureSuccessStatusCode();
                var html = await response.Content.ReadAsStringAsync(ct);
                var gridLinks = ExtractDetailLinks(html);
                grid = (runId, gridLinks.ToLookup(SlugOfDetailUrl, l => l), null);
                logger.LogInformation(
                    "Keystone grid: {Count} puppies across {Breeds} breed buckets",
                    gridLinks.Count, grid.Buckets!.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                grid = (runId, null, ex.Message);
            }

            _grid = grid;
        }

        if (grid.Buckets is null)
        {
            return new ListingFetchResult(breed.Slug, [], 0, 0, $"grid fetch failed: {grid.Error}");
        }

        var links = grid.Buckets[KeystoneSlug(breed.Slug)].Take(DetailsPerBreed).ToList();
        if (links.Count == 0)
        {
            // The vendor doesn't carry this breed today. An empty answer, not an error.
            return new ListingFetchResult(breed.Slug, [], 0, 0, null);
        }

        var expectedName = ExpectedBrand(breed.Slug, breed.DisplayName);
        var now = DateTimeOffset.UtcNow;
        List<ListingPrice> prices = [];
        int seen = 0, mixes = 0, failures = 0;

        foreach (var link in links)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(Delay, ct);

            string html;
            try
            {
                using var response = await client.GetAsync(link, ct);
                if (!response.IsSuccessStatusCode)
                {
                    failures++;
                    continue;  // one adopted puppy's dead page must not end the breed
                }

                html = await response.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures++;
                logger.LogWarning("Keystone detail {Url} failed: {Message}", link, ex.Message);
                continue;
            }

            if (ParseDetail(html) is not { } detail || detail.Price is not { } amount)
            {
                continue;
            }

            seen++;
            if (detail.Currency is { } currency
                && !currency.Equals("USD", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // The brand names the breed ("Frenchton", "French Bulldog"); the same exact-match
            // rule that filters crossbreeds on puppies.com titles applies to it unchanged.
            if (!ListingSources.IsPurebredTitle(detail.Brand ?? detail.Name ?? "", expectedName))
            {
                mixes++;
                continue;
            }

            prices.Add(new ListingPrice(
                BreedSlug: breed.Slug,
                Price: amount,
                SourceHost: Host,
                ListingRef: link,
                ListingName: detail.Name ?? detail.Brand ?? "",
                RetrievedAt: now,
                RunId: runId));
        }

        if (prices.Count == 0 && failures > 0 && seen == 0)
        {
            return new ListingFetchResult(breed.Slug, [], 0, mixes,
                $"{failures} of {links.Count} detail pages failed");
        }

        logger.LogInformation(
            "Keystone listings for {Breed}: {Kept} purebred prices from {Seen} products "
            + "({Mixes} crossbreeds dropped, {Failures} pages failed)",
            breed.Slug, prices.Count, seen, mixes, failures);
        return new ListingFetchResult(breed.Slug, prices, seen, mixes, null);
    }

    /// <summary>Every puppy detail link on the grid, deduped, in page order.</summary>
    public static IReadOnlyList<string> ExtractDetailLinks(string html)
    {
        List<string> links = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (var match in DetailLink().Matches(html).Cast<Match>())
        {
            var url = match.Groups[1].Value;
            if (seen.Add(url))
            {
                links.Add(url);
            }
        }

        return links;
    }

    private static readonly Regex DetailLinkPattern = new(
        """href=["'](https://www\.keystonepuppies\.com/puppy/[^"'?#]+)["']""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static Regex DetailLink() => DetailLinkPattern;

    /// <summary>
    /// The vendor's breed slug inside a detail URL:
    /// <c>/puppy/frenchton-puppies-for-sale/becca-9</c> → <c>frenchton</c>.
    /// Unrecognised shapes bucket under "" and are never asked for.
    /// </summary>
    public static string SlugOfDetailUrl(string url)
    {
        var match = Regex.Match(
            url, @"/puppy/([^/]+?)(?:-puppies)?(?:-for-sale)?/", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : "";
    }

    public sealed record ProductDetail(string? Name, string? Brand, int? Price, string? Currency);

    /// <summary>
    /// The Product node out of a detail page's ld+json, or null when the page has none.
    /// Their JSON carries raw newlines inside strings — technically invalid — which
    /// <see cref="LdJson.Documents"/> repairs rather than discarding the price beside it.
    /// </summary>
    public static ProductDetail? ParseDetail(string html)
    {
        foreach (var document in LdJson.Documents(html))
        {
            using (document)
            {
                foreach (var node in LdJson.Nodes(document.RootElement))
                {
                    if (!LdJson.IsType(node, "Product"))
                    {
                        continue;
                    }

                    string? brand = null;
                    if (node.TryGetProperty("brand", out var brandNode)
                        && brandNode.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        brand = LdJson.Text(brandNode, "name");
                    }

                    int? price = null;
                    string? currency = null;
                    if (node.TryGetProperty("offers", out var offers)
                        && offers.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        price = LdJson.Price(offers);
                        currency = LdJson.Text(offers, "priceCurrency");
                    }

                    return new ProductDetail(LdJson.Text(node, "name"), brand, price, currency);
                }
            }
        }

        return null;
    }
}
