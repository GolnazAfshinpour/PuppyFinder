using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Data;

/// <summary>
/// Which sites may set a breed's price range, and which may not.
///
/// A reviewed, version-controlled list rather than an open web search. There is no
/// authoritative published source for breed prices (breed parent clubs deliberately
/// avoid quoting figures; AKC publishes none; Good Dog publishes none), so the best
/// available evidence is editorially accountable publishers cross-checked against
/// each other. That makes *which sites count* a load-bearing decision, not a detail.
/// See docs/SOURCES.md.
/// </summary>
public static class PriceSources
{
    /// <summary>
    /// Editorially accountable: insurance, financial and veterinary publishers with
    /// named editors and a corrections policy. At least one Tier A source is required
    /// before a range can reach <see cref="PriceConfidence.Verified"/>.
    /// </summary>
    public static readonly string[] TierA =
    [
        "metlifepetinsurance.com",
        "insurify.com",
        "pumpkin.care",
        "forbes.com",
        "nerdwallet.com",
        "petmd.com",
        "thesprucepets.com",
        "rover.com",
        "akc.org",
        "pawlicy.com",
        "lemonade.com",
        "trupanion.com",
        "insuranceopedia.com",
        "moneygeek.com",
    ];

    /// <summary>
    /// Breed-content sites. They do real research, but they're affiliate-monetized —
    /// Dogster states outright that it earns commission on links. Useful for
    /// corroboration; never sufficient on their own.
    /// </summary>
    public static readonly string[] TierB =
    [
        "dogster.com",
        "caninebible.com",
        "a-z-animals.com",
        "dogtemperament.com",
        "breeds101.com",
        "hepper.com",
        "emborapets.com",
        "breedadvisor.com",
        "citizenshipper.com",
        "k9magazine.com",
    ];

    /// <summary>
    /// Never a price authority, for two distinct reasons.
    ///
    /// Sellers (breeders, kennels, brokers) are conflicted: their asking price is a
    /// fact about them, not about the market.
    ///
    /// The classifieds are worse than merely conflicted — they are the very thing the
    /// scam check screens against. Letting Lancaster or Craigslist listings set the
    /// floor would drag it downward every month and quietly disarm the feature while
    /// appearing to work.
    /// </summary>
    public static readonly string[] Blocked =
    [
        // Classifieds we publish caution labels about (see SiteCatalog).
        "lancasterpuppies.com",
        "greenfieldpuppies.com",
        "puppies.com",
        "craigslist.org",
        "pawrade.com",
        "puppyspot.com",
        // Marketplaces and sellers — real listings, but a seller pricing its own stock.
        "gooddog.com",
        "marketplace.akc.org",
        "bluehavenfrenchbulldogs.com",
        "bulldogbazar.com",
        // User-generated and machine-generated content: no editorial accountability,
        // and a common vector for the same numbers circulating unattributed.
        "reddit.com",
        "quora.com",
        "pinterest.com",
        "facebook.com",
        "answers.com",
    ];

    /// <summary>Domains the research job is allowed to search, in preference order.</summary>
    public static IReadOnlyList<string> AllowedDomains { get; } = [.. TierA, .. TierB];

    /// <summary>
    /// The tier for a source URL, or null when the host isn't on the allowlist —
    /// which is a hard reject, not a downgrade. An unrecognised domain means the
    /// model found something we haven't reviewed, and reviewing it is a human job.
    /// </summary>
    public static string? TierFor(string? sourceUrl)
    {
        var host = HostOf(sourceUrl);
        if (host is null)
        {
            return null;
        }

        if (Matches(host, TierA)) return PublisherTier.A;
        if (Matches(host, TierB)) return PublisherTier.B;
        return null;
    }

    public static bool IsBlocked(string? sourceUrl) =>
        HostOf(sourceUrl) is { } host && Matches(host, Blocked);

    /// <summary>Host of an absolute http(s) URL, lowercased and without "www.".</summary>
    public static string? HostOf(string? sourceUrl)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        var host = uri.Host.ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }

    // Suffix match on a dot boundary so "insurify.com" covers "blog.insurify.com" but
    // "notinsurify.com" matches nothing.
    private static bool Matches(string host, string[] domains) =>
        domains.Any(d =>
            host.Equals(d, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith($".{d}", StringComparison.OrdinalIgnoreCase));
}
