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

    /// <summary>Adoption fee. Recorded for context, never mixed into a purchase range.</summary>
    public const string Rescue = "rescue";

    /// <summary>The source didn't say. Recorded, never aggregated — that's the point.</summary>
    public const string Unscoped = "unscoped";

    public static readonly string[] All =
        [PetStandard, ShowOrPedigree, RareColour, Rescue, Unscoped];

    public static bool IsKnown(string? scope) => scope is not null && All.Contains(scope);
}

public static class ObservationStatus
{
    public const string Accepted = "accepted";
    public const string Pending = "pending";
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
    double? SpreadRatio = null)
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
    string? RejectReason = null);

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
