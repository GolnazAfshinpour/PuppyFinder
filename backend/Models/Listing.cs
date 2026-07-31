using PuppyFinder.Api.Services;

namespace PuppyFinder.Api.Models;

/// <summary>
/// One dog/puppy listing aggregated from a source website. ListingUrl links
/// back to the original listing on the source site.
/// </summary>
public record Listing(
    string Id,
    string Name,
    string Breed,
    string? Age,
    string? Sex,
    string Description,
    string City,
    string State,
    string? ImageUrl,
    string ListingUrl,
    string Source,
    string SourceUrl,
    string? Size = null, // Teacup | Small | Medium | Large — null when the feed has no size data
    // Shelter contact info shown on the card itself — the PetHarbor detail pages bury
    // it badly enough that visitors report "no contact info" after clicking through.
    string? ContactInfo = null,
    string? AnimalRef = null) // shelter's own ID ("A545419") — what to mention when calling
{
    /// <summary>Derived from the free-text <see cref="Age"/> so the UI can filter and
    /// sort on it. Serialized to JSON automatically — the frontend never re-parses ages.</summary>
    public int? AgeMonths => AgeParser.ToMonths(Age);

    /// <summary>Puppy | Young | Adult | Senior, or null when the feed gave no usable age.</summary>
    public string? AgeGroup => AgeParser.ToGroup(Age);

    /// <summary>
    /// Set per-request by /api/listings: this dog survived a size or age filter only
    /// because the shelter left that field blank. The card says so rather than
    /// implying a match we can't back up. Depends on the query, not the dog, which is
    /// why it's an init-property stamped on the way out rather than derived here.
    /// </summary>
    public bool Unconfirmed { get; init; }
}
