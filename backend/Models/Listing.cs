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
    string? AnimalRef = null, // shelter's own ID ("A545419") — what to mention when calling
    // Where the animal is, for distance search. Usually the listing organisation's location
    // rather than the animal's own — a fostered dog is wherever its foster is, which no feed
    // publishes. Null when the source records none, which is common.
    double? Latitude = null,
    double? Longitude = null,
    /// <summary>
    /// What the rescue charges, verbatim. The single item adopters rank most important on a
    /// profile, and this app showed it nowhere.
    ///
    /// <para>
    /// A string rather than a number because RescueGroups publishes <c>adoptionFeeString</c>,
    /// and the string carries things a number cannot: "$250", "$300-$450", "Varies", "Waived for
    /// seniors". Parsing it to a number would throw away the cases where the answer is genuinely
    /// not a number. Present on roughly 28% of live records.
    /// </para>
    /// </summary>
    string? AdoptionFee = null,
    /// <summary>
    /// How the dog does with children, other dogs, and cats. Three states, not two: true, false,
    /// and <b>the rescue didn't say</b>.
    ///
    /// <para>
    /// Nullable is the whole point. RescueGroups omits null attributes from the response
    /// entirely, so an absent field is genuinely unknown — and collapsing that to "no" would
    /// libel a dog that is perfectly fine with children, which is the same mistake the size and
    /// age filters were fixed for. Live coverage: dogs 41%, kids 25%, cats 21%.
    /// </para>
    /// </summary>
    bool? GoodWithKids = null,
    bool? GoodWithDogs = null,
    bool? GoodWithCats = null)
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

    /// <summary>
    /// Miles from the point the visitor searched from. Stamped per request like
    /// <see cref="Unconfirmed"/>, because it is a property of the question rather than of the dog.
    /// Null when either end has no coordinates.
    /// </summary>
    public double? DistanceMiles { get; init; }
}
