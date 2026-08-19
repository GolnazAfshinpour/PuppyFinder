using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <summary>
/// What the user asked for. A record rather than a parameter list so /api/listings
/// and the alert checker can't drift apart, and so adding a filter (radius, sex)
/// doesn't churn every call site.
/// </summary>
/// <param name="BreedSearchText">Free text matched against the listing's breed
/// (callers resolve catalog slugs to search names first); null = any breed.</param>
/// <param name="AgeGroup">Puppy | Young | Adult | Senior; null = any age.</param>
/// <param name="IncludeUnlisted">Keep listings whose size/age the shelter never
/// filled in. Default true — see the note on <see cref="ListingQuery.Filter"/>.</param>
public record ListingFilter(
    string? BreedSearchText = null,
    string? State = null,
    string? City = null,
    string? Size = null,
    string? AgeGroup = null,
    bool IncludeUnlisted = true,
    // Where the visitor is searching from, and how far they will travel. All three are needed
    // for the filter to apply at all — a radius with no origin is meaningless.
    double? Latitude = null,
    double? Longitude = null,
    int? RadiusMiles = null,
    // Requested from the rescue's own listing, not from the breed. False means "not asked for",
    // never "must not be good with kids" — nobody searches for that.
    bool GoodWithKids = false,
    bool GoodWithDogs = false,
    bool GoodWithCats = false)
{
    /// <summary>The good-with fields this search asked about, paired with how to read each dog.</summary>
    public IEnumerable<(bool Wanted, Func<Listing, bool?> Value)> GoodWith =>
    [
        (GoodWithKids, l => l.GoodWithKids),
        (GoodWithDogs, l => l.GoodWithDogs),
        (GoodWithCats, l => l.GoodWithCats),
    ];

    /// <summary>True when this filter can actually measure distance.</summary>
    public bool HasOrigin =>
        Latitude is { } lat && Longitude is { } lon && GeoDistance.IsPlausible(lat, lon);

    /// <summary>True when distance should narrow the results, not merely be reported.</summary>
    public bool HasRadius => HasOrigin && RadiusMiles is > 0;
}

/// <summary>
/// The one place listing filters are defined — shared by /api/listings and the
/// alert checker so a saved alert can never match differently than the search UI.
/// </summary>
public static class ListingQuery
{
    /// <summary>Miles from the filter's origin to this listing, or null if either lacks coords.</summary>
    public static double? DistanceFor(Listing listing, ListingFilter filter) =>
        GeoDistance.Miles(filter.Latitude, filter.Longitude, listing.Latitude, listing.Longitude);

    /// <remarks>
    /// Size and age treat missing data as "unknown", not "no": industry-wide only a
    /// fraction of shelter listings have complete profiles, so dropping every blank
    /// field silently deletes most of the inventory and the user concludes there are
    /// no dogs. Unknowns are kept — callers mark them "size not listed" and
    /// <see cref="Sort"/> ranks them below confirmed matches — and
    /// <see cref="ListingFilter.IncludeUnlisted"/> lets someone who wants a hard
    /// match opt out.
    /// </remarks>
    public static IEnumerable<Listing> Filter(IEnumerable<Listing> listings, ListingFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.BreedSearchText))
        {
            listings = listings.Where(l =>
                l.Breed.Contains(filter.BreedSearchText.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.State))
        {
            listings = listings.Where(l => l.State.Equals(filter.State, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            listings = listings.Where(l => l.City.Contains(filter.City.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Size))
        {
            listings = listings.Where(l => l.Size is null
                ? filter.IncludeUnlisted
                : filter.Size.Equals(l.Size, StringComparison.OrdinalIgnoreCase));
        }

        // Same rule as size and age: a dog whose rescue recorded no location is "unknown", not
        // "far away". Dropping those would silently hide real dogs over a blank field, and most
        // of them are within range of somebody.
        if (filter.HasRadius)
        {
            listings = listings.Where(l =>
                DistanceFor(l, filter) is { } miles
                    ? miles <= filter.RadiusMiles!.Value
                    : filter.IncludeUnlisted);
        }

        if (AgeParser.IsGroup(filter.AgeGroup) && filter.AgeGroup is { } wantedAge)
        {
            listings = listings.Where(l => l.AgeGroup is null
                ? filter.IncludeUnlisted
                : wantedAge.Equals(l.AgeGroup, StringComparison.OrdinalIgnoreCase));
        }

        // Good-with follows the same unknown-is-not-no rule, and has to: only 21-41% of listings
        // record these, so a hard filter would delete most of the inventory over blank fields.
        //
        // The one asymmetry in this whole file. An explicit `false` is dropped unconditionally —
        // `IncludeUnlisted` does not reach it, because it is not unlisted. Someone filtering
        // "good with kids" has a child in the house, and a rescue that wrote down "no" is the one
        // fact in this dataset that must never be overridden by a convenience toggle.
        foreach (var (wanted, value) in filter.GoodWith)
        {
            if (!wanted)
            {
                continue;
            }

            listings = listings.Where(l => value(l) switch
            {
                true => true,
                false => false,
                null => filter.IncludeUnlisted,
            });
        }

        return listings;
    }

    /// <summary>
    /// Result ordering. Confirmed matches always precede listings that only survived
    /// because a field was blank, so "unknown" never outranks "yes".
    /// </summary>
    public static IEnumerable<Listing> Sort(IEnumerable<Listing> listings, string? sort, ListingFilter filter)
    {
        // "nearest" waited until listings carried coordinates, on the grounds that a distance
        // sort which silently isn't one is worse than not offering it. RescueGroups supplies
        // them, so it exists now — but only when the request gave somewhere to measure from.
        // Without an origin it falls through to the default rather than pretending to order.
        var ordered = sort?.ToLowerInvariant() switch
        {
            "youngest" => listings.OrderBy(l => l.AgeMonths ?? int.MaxValue),
            "oldest" => listings.OrderByDescending(l => l.AgeMonths ?? -1),
            "nearest" when filter.HasOrigin =>
                listings.OrderBy(l => DistanceFor(l, filter) ?? double.MaxValue),
            _ => listings.OrderBy(l => 0),
        };

        return ordered
            .ThenBy(l => Unconfirmed(l, filter))
            .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>True when this listing matched only because the shelter left a filtered field blank.</summary>
    public static bool Unconfirmed(Listing listing, ListingFilter filter) =>
        (!string.IsNullOrWhiteSpace(filter.Size) && listing.Size is null)
        || (AgeParser.IsGroup(filter.AgeGroup) && listing.AgeGroup is null)
        || filter.GoodWith.Any(g => g.Wanted && g.Value(listing) is null);
}
