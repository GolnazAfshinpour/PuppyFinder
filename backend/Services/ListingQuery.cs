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
    bool IncludeUnlisted = true);

/// <summary>
/// The one place listing filters are defined — shared by /api/listings and the
/// alert checker so a saved alert can never match differently than the search UI.
/// </summary>
public static class ListingQuery
{
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

        if (AgeParser.IsGroup(filter.AgeGroup) && filter.AgeGroup is { } wantedAge)
        {
            listings = listings.Where(l => l.AgeGroup is null
                ? filter.IncludeUnlisted
                : wantedAge.Equals(l.AgeGroup, StringComparison.OrdinalIgnoreCase));
        }

        return listings;
    }

    /// <summary>
    /// Result ordering. Confirmed matches always precede listings that only survived
    /// because a field was blank, so "unknown" never outranks "yes".
    /// </summary>
    public static IEnumerable<Listing> Sort(IEnumerable<Listing> listings, string? sort, ListingFilter filter)
    {
        // "nearest" is deliberately absent until listings carry coordinates (arrives
        // with RescueGroups) — a distance sort that silently isn't one is worse than
        // not offering one.
        var ordered = sort?.ToLowerInvariant() switch
        {
            "youngest" => listings.OrderBy(l => l.AgeMonths ?? int.MaxValue),
            "oldest" => listings.OrderByDescending(l => l.AgeMonths ?? -1),
            _ => listings.OrderBy(l => 0),
        };

        return ordered
            .ThenBy(l => Unconfirmed(l, filter))
            .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>True when this listing matched only because the shelter left a filtered field blank.</summary>
    public static bool Unconfirmed(Listing listing, ListingFilter filter) =>
        (!string.IsNullOrWhiteSpace(filter.Size) && listing.Size is null)
        || (AgeParser.IsGroup(filter.AgeGroup) && listing.AgeGroup is null);
}
