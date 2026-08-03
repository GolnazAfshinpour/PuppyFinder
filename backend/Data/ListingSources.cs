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
    };

    public static string VendorName(string breedSlug, string displayName) =>
        VendorNames.TryGetValue(breedSlug, out var mapped) ? mapped : displayName;

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
