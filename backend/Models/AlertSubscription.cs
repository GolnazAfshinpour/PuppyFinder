namespace PuppyFinder.Api.Models;

/// <summary>
/// A saved search that emails the subscriber when new matching listings appear.
/// SeenListingIds tracks what was already notified (or existed at signup) so
/// each dog is announced at most once.
/// </summary>
public record AlertSubscription(
    string Id,
    string Email,
    string? Breed,   // catalog slug
    string? State,
    string? City,
    string? Size,
    DateTimeOffset CreatedAt)
{
    public HashSet<string> SeenListingIds { get; init; } = [];
}
