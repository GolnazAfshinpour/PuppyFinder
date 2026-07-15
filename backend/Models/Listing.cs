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
    string? Size = null); // Teacup | Small | Medium | Large — null when the feed has no size data
