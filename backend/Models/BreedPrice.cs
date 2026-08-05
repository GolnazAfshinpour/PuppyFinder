namespace PuppyFinder.Api.Models;

/// <summary>
/// How much we trust a breed's published price range. This is a property of the
/// data, not a claim in the copy — the UI reads it rather than asserting
/// "verified" on its own. See docs/SOURCES.md.
/// </summary>
public static class PriceConfidence
{
    /// <summary>No citable source. Includes the original hardcoded numbers.</summary>
    public const string Unverified = "unverified";

    /// <summary>One citable source — enough to show, not enough to call settled.</summary>
    public const string SingleSource = "single_source";

    /// <summary>Enough sources, but they disagree materially. Show the spread, not a band.</summary>
    public const string Contested = "contested";

    /// <summary>Three-plus independent sources including an editorially accountable one.</summary>
    public const string Verified = "verified";
}

/// <summary>
/// What a quoted figure actually covers. The single most important field in the
/// pipeline: most apparent disagreement between sources is conflated scope, not
/// real disagreement (a $5,000 merle French Bulldog and a $2,000 pet-quality one
/// are different questions). Only <see cref="PetStandard"/> feeds the published
/// range.
/// </summary>
public static class PriceScope
{
    /// <summary>Pet-quality, standard colour, reputable breeder. The only aggregated scope.</summary>
    public const string PetStandard = "pet_standard";

    public const string ShowOrPedigree = "show_or_pedigree";
    public const string RareColour = "rare_colour";

    /// <summary>
    /// Explicitly scoped to a region ("Northeast $1,200–2,500"). Real data on the wrong
    /// axis: mixing it with national figures widens a range for a reason that has
    /// nothing to do with the breed.
    /// </summary>
    public const string Regional = "regional";

    /// <summary>Adoption fee. Recorded for context, never mixed into a purchase range.</summary>
    public const string Rescue = "rescue";

    /// <summary>The source didn't say. Recorded, never aggregated — that's the point.</summary>
    public const string Unscoped = "unscoped";

    public static readonly string[] All =
        [PetStandard, ShowOrPedigree, RareColour, Regional, Rescue, Unscoped];

    public static bool IsKnown(string? scope) => scope is not null && All.Contains(scope);
}

/// <summary>
/// Whether a source published a band or a single number. Tier A publishers often give
/// only an average ("about $5,000"), and requiring a low+high would silently discard
/// their data — so averages are kept, but they corroborate rather than widen.
/// </summary>
public static class FigureKind
{
    /// <summary>A low and a high. The only kind that feeds the published range.</summary>
    public const string Range = "range";

    /// <summary>
    /// A single figure. Never widens the range; counts as a source when it falls inside
    /// the aggregated range, and forces Contested when it falls outside — which is real
    /// disagreement worth surfacing rather than hiding.
    /// </summary>
    public const string Average = "average";

    public static readonly string[] All = [Range, Average];

    public static bool IsKnown(string? kind) => kind is not null && All.Contains(kind);
}

/// <summary>Which kind of evidence a published range was derived from.</summary>
public static class PriceBasis
{
    /// <summary>Median of ranges published by editorially accountable sources.</summary>
    public const string Editorial = "editorial";

    /// <summary>Interquartile range of real asking prices on a marketplace.</summary>
    public const string Listings = "listings";

    public static readonly string[] All = [Editorial, Listings];
}

/// <summary>
/// The bar a range must clear to be trusted. Configuration rather than constants,
/// because the right thresholds depend on what the sources actually support — and
/// because aggregation is a pure function over stored observations, re-tuning these
/// costs nothing (no re-research, no API spend). Defaults are the strict values.
/// </summary>
public record PriceThresholds(
    int MinSources = 3,
    bool RequireTierA = true,
    double MaxSpreadRatio = 2.0,
    int DriftReviewPercent = 40,
    // How wide the published band itself may be, as high ÷ low.
    //
    // MaxSpreadRatio measures whether sources *agree with each other* on midpoints; it
    // says nothing about how wide the band they produce is. Dachshund showed the gap:
    // three sources agreeing within 1.88x still yielded $500-$3,500, a 7x band that
    // passed every rule and would have gone live labelled "verified from 3 sources".
    // A 7x band cannot screen anything — with PriceCheck's 0.5x far-below rule, only a
    // quote under $250 would be flagged, so the check reads as working while catching
    // nothing. A range that wide is an honest "we don't know", not a benchmark.
    double MaxVerifiedBandRatio = 4.0,
    // Listing samples: how many live asking prices before the middle half means anything.
    // 20 is two pages' worth after crossbreeds are dropped — enough that one scam listing
    // can't move the 25th percentile.
    int MinListingSample = 20,
    // How far a listing sample's 25th percentile may fall below the published low before
    // we treat it as the marketplace's cheap tail rather than the breed's price. At 0.75,
    // Beagle's $400 against a published $400 passes but a $250 would not.
    double ListingFloorFactor = 0.75,
    // How far back to pool listing samples. Runs return near-disjoint sets, so pooling is
    // what makes the range stable rather than a fresh random sample every month; the window
    // is what stops it going stale. 90 days is short against how slowly breed prices move.
    int ListingWindowDays = 90,
    // The largest share of a listing sample that may sit at a single identical price.
    //
    // A breeder listing a litter of eighteen at $2,000 is one offer, not eighteen data
    // points, and we cannot tell that apart from eighteen breeders independently choosing
    // $2,000 - the structured data carries no seller id. So rather than discarding repeats,
    // refuse the sample when one price dominates it, because that is when a single seller is
    // effectively setting the breed's range.
    //
    // Measured: Irish Wolfhound had 18 of 27 listings at exactly $2,000 and published a
    // $2,000-$2,100 band - 1.05x wide, from what is almost certainly one litter. A legitimate
    // $2,500 Wolfhound would have been flagged as above typical. Afghan Hound: 11 of 20 at
    // $3,000. Most breeds sit under 20%, so this refuses the degenerate cases without
    // touching the healthy ones.
    double MaxSinglePriceShare = 0.35)
{
    public static PriceThresholds FromConfiguration(IConfiguration configuration) => new(
        MinSources: configuration.GetValue("Prices:MinSources", 3),
        RequireTierA: configuration.GetValue("Prices:RequireTierA", true),
        MaxSpreadRatio: configuration.GetValue("Prices:MaxSpreadRatio", 2.0),
        DriftReviewPercent: configuration.GetValue("Prices:DriftReviewPercent", 40),
        MaxVerifiedBandRatio: configuration.GetValue("Prices:MaxVerifiedBandRatio", 4.0),
        MinListingSample: configuration.GetValue("Prices:MinListingSample", 20),
        ListingFloorFactor: configuration.GetValue("Prices:ListingFloorFactor", 0.75),
        ListingWindowDays: configuration.GetValue("Prices:ListingWindowDays", 90),
        MaxSinglePriceShare: configuration.GetValue("Prices:MaxSinglePriceShare", 0.35));
}

/// <summary>
/// What we currently believe about an observation. There is deliberately no "pending": a
/// collector either produces a figure that passed <see cref="PriceObservationValidator"/> or one
/// that did not, and whether the resulting *range* wants a human look is derived at aggregation
/// time rather than stored here. A pending status existed for a while and nothing ever wrote it.
/// </summary>
public static class ObservationStatus
{
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
}

/// <summary>Editorial accountability of a source. Tier B alone can never reach Verified.</summary>
public static class PublisherTier
{
    /// <summary>Insurance, financial, veterinary publishers — named editors, corrections policy.</summary>
    public const string A = "a";

    /// <summary>Breed-content sites: real research, but affiliate-monetized.</summary>
    public const string B = "b";
}

/// <summary>The live price range for one breed, plus how well we can back it.</summary>
public record BreedPrice(
    string BreedSlug,
    int PriceLow,
    int PriceHigh,
    string Confidence,
    int SourceCount,
    DateTimeOffset UpdatedAt,
    /// <summary>max(low) / min(low) across sources — above 2.0 forces Contested.</summary>
    double? SpreadRatio = null,
    /// <summary>
    /// Which kind of evidence produced this range — see <see cref="PriceBasis"/>. Once
    /// listing samples and published articles can both produce a live range, one that
    /// doesn't say which it came from is unattributable, which is the fault this whole
    /// feature exists to correct.
    /// </summary>
    string Basis = PriceBasis.Editorial)
{
    public string TypicalPrice => $"${PriceLow:n0}–${PriceHigh:n0}";
}

/// <summary>
/// One price figure attributed to one source. Append-only: every observation the
/// research job ever returns is kept, so a change can always be traced back to
/// the page and run that produced it.
/// </summary>
public record PriceObservation(
    string BreedSlug,
    int PriceLow,
    int PriceHigh,
    string Scope,
    /// <summary>range | average — see <see cref="FigureKind"/>.</summary>
    string Kind,
    string SourceUrl,
    string Publisher,
    string PublisherTier,
    /// <summary>Verbatim snippet supporting the figure. No quote, no write.</summary>
    string Quote,
    DateTimeOffset RetrievedAt,
    string RunId,
    string Model,
    string Status,
    long Id = 0,
    /// <summary>The source's own publication date, when it states one.</summary>
    DateTimeOffset? PublishedAt = null,
    /// <summary>An explicit "below $X is a scam" statement — corroborating evidence, not a threshold.</summary>
    string? RedFlagQuote = null,
    string? RejectReason = null)
{
    /// <summary>
    /// Midpoint of the figure — for an <see cref="FigureKind.Average"/> this is the
    /// figure itself. Source disagreement is measured on midpoints rather than lows,
    /// because a wide band from one source isn't the same thing as sources disagreeing.
    /// </summary>
    public double Midpoint => (PriceLow + PriceHigh) / 2.0;
}

/// <summary>One execution of the research job — the audit record for a batch run.</summary>
public record PriceRun(
    string Id,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt = null,
    int BreedsChecked = 0,
    int Accepted = 0,
    int Pending = 0,
    int Rejected = 0,
    string? Error = null);
