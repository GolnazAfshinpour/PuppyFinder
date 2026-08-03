namespace PuppyFinder.Api.Data;

/// <summary>
/// Where real asking prices come from, and the rules for reading them.
///
/// <para>
/// <b>Standing caveat.</b> Puppies.com's terms forbid systematically collecting content
/// "including through bots, spiders, automated scripts, or AI-assisted tools" without
/// written permission, and restrict commercial use. This runs at the product owner's
/// direction and on their risk, with the exposure documented in docs/SOURCES.md. Two
/// limits are deliberate and should stay: we read only the schema.org <c>ld+json</c> the
/// site publishes for machine consumption, and we never defeat an access control — a site
/// that answers 403 stays unread rather than being worked around.
/// </para>
///
/// <para>
/// The listing prices are a different kind of evidence from published editorial ranges:
/// one number per animal actually for sale, in quantity, today. That makes them better
/// raw material — and dangerous in one specific way, which is why
/// <see cref="Services.ListingPriceAggregator"/> keeps the editorial range as a floor
/// guard. A classifieds site's own low end is exactly what the scam check is meant to
/// flag, so calibrating purely to it would be circular.
/// </para>
/// </summary>
public static class ListingSources
{
    /// <summary>The only host we read. Prices are in its published structured data.</summary>
    public const string Host = "puppies.com";

    /// <summary>Listing index for a breed, page 1 being the bare path.</summary>
    public static string PageUrl(string vendorSlug, int page) =>
        page <= 1
            ? $"https://puppies.com/find-a-puppy/{vendorSlug}"
            : $"https://puppies.com/find-a-puppy/{vendorSlug}/{page}";

    /// <summary>
    /// Our breed slug to theirs, only where the two differ. Verified by fetching each —
    /// a wrong slug 404s rather than silently returning another breed's prices, but a
    /// *plausible* wrong slug (english-bulldog vs bulldog) would quietly return the wrong
    /// animal, so these are checked rather than guessed.
    /// </summary>
    public static readonly Dictionary<string, string> SlugOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["german-shepherd"] = "german-shepherd-dog",
        ["bulldog"] = "english-bulldog",
        // The bare "poodle" slug mixes Toy/Miniature/Standard and only returned one page;
        // "standard-poodle" is the size our catalogue actually means by "Poodle (Standard)".
        ["poodle"] = "standard-poodle",

        // Catalog breeds beyond the curated 25. Our slugs come from dog.ceo, whose naming
        // is idiosyncratic ("airedale", "english-sheepdog", "shepherd-australian"), so most
        // of these are shape fixes rather than different breeds. Probed August 2026; only
        // the ones that actually differ are listed.
        ["airedale"] = "airedale-terrier",
        ["cardigan-corgi"] = "cardigan-welsh-corgi",
        ["english-sheepdog"] = "old-english-sheepdog",
        ["mexican-hairless"] = "xoloitzcuintli",
        ["pembroke"] = "pembroke-welsh-corgi",
        ["shepherd-australian"] = "australian-shepherd",
    };

    public static string VendorSlug(string breedSlug) =>
        SlugOverrides.TryGetValue(breedSlug, out var mapped) ? mapped : breedSlug;

    /// <summary>
    /// The listing title that counts as a purebred of our breed, where the vendor names it
    /// differently from our display name.
    ///
    /// <para>
    /// Necessary because a naming mismatch fails *silently* as "this breed has no listings"
    /// — which is exactly how three breeds returned 0 of 50 results on the first real run.
    /// Every entry below was read off the live titles, not guessed:
    /// </para>
    /// <list type="bullet">
    ///   <item>our "Bulldog" is their "English Bulldog" (10/10 titles)</item>
    ///   <item>our "German Shepherd" is their "German Shepherd Dog" (8/10)</item>
    ///   <item>our "Poodle (Standard)" is their "Poodle - Standard" (7/10 on
    ///         <c>standard-poodle</c>; the bare <c>poodle</c> slug mixes the size varieties)</item>
    /// </list>
    /// </summary>
    public static readonly Dictionary<string, string> VendorNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bulldog"] = "English Bulldog",
        ["german-shepherd"] = "German Shepherd Dog",
        ["poodle"] = "Poodle - Standard",

        // Read off the most common *purebred* title on each page, not the most common title
        // outright: cardigan-corgi's single commonest listing is "Cardigan Welsh Corgi and
        // Pembroke Welsh Corgi", and using that would invert the filter into accepting only
        // crossbreeds. The poodle sizes are named "Poodle - <Size>" on their side.
        ["airedale"] = "Airedale Terrier",
        ["cardigan-corgi"] = "Cardigan Welsh Corgi",
        ["english-sheepdog"] = "Old English Sheepdog",
        ["mexican-hairless"] = "Xoloitzcuintli",
        ["miniature-poodle"] = "Poodle - Miniature",
        ["pembroke"] = "Pembroke Welsh Corgi",
        ["shepherd-australian"] = "Australian Shepherd",
        ["standard-poodle"] = "Poodle - Standard",
        ["toy-poodle"] = "Poodle - Toy",
    };

    public static string VendorName(string breedSlug, string displayName) =>
        VendorNames.TryGetValue(breedSlug, out var mapped) ? mapped : displayName;

    /// <summary>
    /// Catalog breeds the vendor was measured to carry with enough inventory to matter.
    ///
    /// <para>
    /// An explicit list rather than "try everything", because a probe of all 154 unpriced
    /// breeds found only 54 worth collecting. The other 100 split into two groups that no
    /// mapping fixes: breeds that resolve but have almost no listings (Kerry Blue Terrier 4,
    /// Affenpinscher 1, Finnish Lapphund 0 — you cannot compute a percentile band from
    /// those), and entries that aren't sold in the US or aren't breeds at all. Attempting
    /// them every run would be ~100 pointless requests against a site whose terms we are
    /// already stretching.
    /// </para>
    ///
    /// <para>
    /// Inventory is the ceiling here, not effort: popular breeds have listings, rare ones
    /// don't, and that correlation is permanent. Re-probe if coverage matters more later.
    /// </para>
    /// </summary>
    public static readonly HashSet<string> KnownToVendor = new(StringComparer.OrdinalIgnoreCase)
    {
        "afghan-hound",
        "airedale",
        "akita",
        "basenji",
        "basset-hound",
        "bichon-frise",
        "border-collie",
        "boston-terrier",
        "cairn-terrier",
        "cardigan-corgi",
        "caucasian-ovcharka",
        "cavapoo",
        "cockapoo",
        "cocker-spaniel",
        "coton-de-tulear",
        "dalmatian",
        "english-bulldog",
        "english-mastiff",
        "english-setter",
        "english-sheepdog",
        "fox-terrier",
        "giant-schnauzer",
        "havanese",
        "irish-setter",
        "irish-terrier",
        "irish-wolfhound",
        "italian-greyhound",
        "keeshond",
        "labradoodle",
        "maltese",
        "mexican-hairless",
        "miniature-pinscher",
        "miniature-poodle",
        "miniature-schnauzer",
        "newfoundland",
        "norwegian-elkhound",
        "papillon",
        "pembroke",
        "pug",
        "puggle",
        "rhodesian-ridgeback",
        "samoyed",
        "schipperke",
        "scottish-terrier",
        "shepherd-australian",
        "shetland-sheepdog",
        "silky-terrier",
        "staffordshire-bull-terrier",
        "standard-poodle",
        "tibetan-mastiff",
        "toy-poodle",
        "vizsla",
        "weimaraner",
        "whippet",
    };

    public static bool IsKnownToVendor(string breedSlug) => KnownToVendor.Contains(breedSlug);

    /// <summary>
    /// Is this listing's title a purebred of the breed we asked for?
    ///
    /// <para>
    /// Breed searches return crossbreeds too, and not as a rounding error: 6 of 10
    /// Bernese Mountain Dog results and 5 of 10 Pembroke Welsh Corgi results were mixes.
    /// A mix is usually cheaper, so including them drags a purebred range down — the same
    /// failure as counting scam listings, arriving by a different route.
    /// </para>
    ///
    /// <para>
    /// Titles look like "French Bulldog - F" for a purebred and "Boston Terrier and
    /// French Bulldog - F" for a mix, so the test is that the name before the sex marker
    /// matches the expected name exactly. Exact match is what handles mixes: no separate
    /// " and " rule is needed, because "Boston Terrier and French Bulldog" simply isn't
    /// "French Bulldog".
    /// </para>
    ///
    /// <para>
    /// The sex marker has to be recognised narrowly rather than by stripping everything
    /// after the last " - ", because that separator is also used for size varieties:
    /// "Poodle - Standard", "Poodle - Miniature". Stripping blindly turned
    /// "Poodle - Standard - F" into "Poodle - Standard" in one place and "Poodle" in
    /// another. Observed markers are M, F and N, so only a trailing one-or-two character
    /// segment is treated as sex.
    /// </para>
    /// </summary>
    public static bool IsPurebredTitle(string listingName, string expectedName)
    {
        if (string.IsNullOrWhiteSpace(listingName))
        {
            return false;
        }

        var name = listingName.Trim();
        var dash = name.LastIndexOf(" - ", StringComparison.Ordinal);
        if (dash > 0 && name.Length - (dash + 3) is > 0 and <= 2)
        {
            name = name[..dash];
        }

        // Collapse repeated whitespace: some mixed titles carry a double space
        // ("Poodle - Miniature  and Poodle - Standard").
        name = string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var expected = string.Join(' ', expectedName.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return name.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }
}
