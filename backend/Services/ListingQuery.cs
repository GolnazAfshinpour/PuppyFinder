using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <summary>
/// The one place listing filters are defined — shared by /api/listings and the
/// alert checker so a saved alert can never match differently than the search UI.
/// </summary>
public static class ListingQuery
{
    /// <param name="breedSearchText">Free text matched against the listing's breed
    /// (callers resolve catalog slugs to search names first); null = any breed.</param>
    public static IEnumerable<Listing> Filter(
        IEnumerable<Listing> listings, string? breedSearchText, string? state, string? city, string? size)
    {
        if (!string.IsNullOrWhiteSpace(breedSearchText))
        {
            listings = listings.Where(l => l.Breed.Contains(breedSearchText.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            listings = listings.Where(l => l.State.Equals(state, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            listings = listings.Where(l => l.City.Contains(city.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(size))
        {
            // Known-size mismatches drop; listings without size data drop too — a Small
            // filter that still shows 90-lb dogs reads as broken.
            listings = listings.Where(l => size.Equals(l.Size, StringComparison.OrdinalIgnoreCase));
        }

        return listings;
    }
}
