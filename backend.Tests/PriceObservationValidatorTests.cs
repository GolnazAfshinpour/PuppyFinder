using PuppyFinder.Api.Models;
using PuppyFinder.Api.Services;

namespace PuppyFinder.Api.Tests;

public class PriceObservationValidatorTests
{
    private static readonly PriceThresholds Strict = new();

    private static PriceObservation Obs(
        int low, int high,
        string publisher = "MetLife Pet Insurance",
        string url = "https://www.metlifepetinsurance.com/blog/breed-spotlights/french-bulldog/",
        string scope = PriceScope.PetStandard,
        string kind = FigureKind.Range,
        string slug = "french-bulldog") => new(
        BreedSlug: slug,
        PriceLow: low,
        PriceHigh: high,
        Scope: scope,
        Kind: kind,
        SourceUrl: url,
        Publisher: publisher,
        PublisherTier: PublisherTier.A,
        Quote: "Expect to pay somewhere in this band for a puppy from a reputable breeder.",
        RetrievedAt: DateTimeOffset.UtcNow,
        RunId: "run-test",
        Model: "claude-opus-5",
        Status: ObservationStatus.Accepted);

    // Convenience: a Tier B source, for the "Tier B alone isn't enough" rule. The tier
    // is derived from the URL by the validator, so the field here is irrelevant.
    private static PriceObservation TierB(int low, int high, string host = "dogster.com") =>
        Obs(low, high, publisher: host, url: $"https://www.{host}/lifestyle/price")
            with { PublisherTier = PublisherTier.B };

    // ---------------------------------------------------------------- hard rejects

    [Theory]
    [InlineData("", "not an http")]
    [InlineData("not-a-url", "not an http")]
    [InlineData("ftp://metlifepetinsurance.com/x", "not an http")]
    public void RejectsFiguresWithoutAUsableSourceUrl(string url, string expected) =>
        Assert.Contains(expected, PriceObservationValidator.Reject(Obs(2000, 4000, url: url)));

    [Theory]
    // The classifieds are what the scam check screens against; letting their listing
    // prices set the floor would drag it down and quietly disarm the feature.
    [InlineData("https://www.lancasterpuppies.com/breeds/french-bulldog/puppy")]
    [InlineData("https://www.craigslist.org/search/pet")]
    [InlineData("https://www.puppies.com/find-a-puppy/french-bulldog")]
    // Sellers price their own stock.
    [InlineData("https://www.gooddog.com/breeds/french-bulldog")]
    [InlineData("https://bluehavenfrenchbulldogs.com/article/pricing")]
    public void RejectsSourcesThatCannotBeAPriceAuthority(string url) =>
        Assert.Contains("excluded as a price authority", PriceObservationValidator.Reject(Obs(2000, 4000, url: url)));

    [Fact]
    public void RejectsDomainsNobodyHasReviewed() =>
        Assert.Contains("not on the reviewed source list",
            PriceObservationValidator.Reject(Obs(2000, 4000, url: "https://random-puppy-blog.example/prices")));

    [Fact]
    public void RejectsFiguresWithNoVerbatimQuote()
    {
        var thin = Obs(2000, 4000) with { Quote = "$2k-4k" };

        Assert.Contains("no verbatim quote", PriceObservationValidator.Reject(thin));
    }

    [Theory]
    [InlineData(4000, 2000, "low is not below its high")]
    [InlineData(50, 4000, "plausible")]         // below the floor
    [InlineData(2000, 40_000, "plausible")]     // above the ceiling
    [InlineData(500, 9000, "more than 10x")]    // a shrug, not a range
    public void RejectsImplausibleBands(int low, int high, string expected) =>
        Assert.Contains(expected, PriceObservationValidator.Reject(Obs(low, high)));

    [Fact]
    public void RejectsAnAverageThatCarriesTwoDifferentValues()
    {
        var malformed = Obs(4000, 5000, kind: FigureKind.Average);

        Assert.Contains("same value as low and high", PriceObservationValidator.Reject(malformed));
    }

    [Fact]
    public void AcceptsAWellFormedFigureAndStampsTheTierFromTheUrl()
    {
        var claimedWrongTier = Obs(2500, 4000) with { PublisherTier = PublisherTier.B };

        var (kept, rejected) = PriceObservationValidator.Partition([claimedWrongTier]);

        Assert.Empty(rejected);
        // Tier is derived from the reviewed list, never taken on the model's word.
        Assert.Equal(PublisherTier.A, Assert.Single(kept).PublisherTier);
    }

    // ---------------------------------------------------------------- scope filtering

    [Theory]
    [InlineData(PriceScope.RareColour)]      // merle/lilac premiums — the worst confound
    [InlineData(PriceScope.ShowOrPedigree)]  // champion lines
    [InlineData(PriceScope.Regional)]        // real data, wrong axis
    [InlineData(PriceScope.Rescue)]          // adoption fee, not a purchase price
    [InlineData(PriceScope.Unscoped)]        // source didn't say — never guess
    public void OnlyPetQualityNationalFiguresFeedTheRange(string excludedScope)
    {
        var result = PriceObservationValidator.Aggregate("french-bulldog",
            [Obs(2500, 4000), Obs(2600, 4200, "Insurify", "https://insurify.com/x"),
             Obs(9000, 20_000, scope: excludedScope)],
            Strict);

        // The excluded figure must not widen the band, whatever its value.
        Assert.Equal(2550, result.Price!.PriceLow);
        Assert.Equal(4100, result.Price.PriceHigh);
        Assert.Equal(2, result.Counted.Count);
    }

    [Fact]
    public void WithNoRangeAtAllThereIsNoBandToPublish()
    {
        var result = PriceObservationValidator.Aggregate("french-bulldog",
            [Obs(5000, 5000, kind: FigureKind.Average)], Strict);

        Assert.Null(result.Price);
        Assert.Contains("averages alone", result.Rationale);
    }

    // ---------------------------------------------------------------- independence

    [Fact]
    public void IdenticalFiguresFromDifferentDomainsCountOnce()
    {
        // Copied content, not corroboration — this is how one unsourced number would
        // otherwise launder itself into "verified" across three sites.
        var result = PriceObservationValidator.Aggregate("french-bulldog",
            [Obs(2500, 4000), TierB(2500, 4000, "dogster.com"), TierB(2500, 4000, "hepper.com")],
            Strict);

        Assert.Equal(1, result.Price!.SourceCount);
        Assert.Equal(PriceConfidence.SingleSource, result.Price.Confidence);
    }

    [Fact]
    public void CollapsingKeepsTheMostAccountablePublisher()
    {
        var kept = PriceObservationValidator.CollapseDuplicates(
            [TierB(2500, 4000) with { PublisherTier = PublisherTier.B }, Obs(2500, 4000)]);

        Assert.Equal(PublisherTier.A, Assert.Single(kept).PublisherTier);
    }

    // ------------------------------------------------- one vote per publisher

    [Fact]
    public void OnePageStatingTwoFiguresIsStillOneSource()
    {
        // Found gathering Frenchie figures by hand: Insurify's page publishes both
        // "around $5,000 ... on average" and a "$2,000-$8,000" table range. Counting rows
        // would let one editorial voice supply two of the three sources that unlock a live
        // scam check.
        var result = PriceObservationValidator.Aggregate("french-bulldog",
            [Obs(2000, 8000, "Insurify", "https://insurify.com/x"),
             Obs(5000, 5000, "Insurify", "https://insurify.com/x", kind: FigureKind.Average),
             Obs(2500, 4000)],
            Strict);

        Assert.Equal(2, result.Price!.SourceCount);
        Assert.NotEqual(PriceConfidence.Verified, result.Price.Confidence);
    }

    [Fact]
    public void ThreePagesFromOneDomainCannotReachVerifiedAlone()
    {
        // The degenerate version: a single publisher's breed hub could otherwise clear a
        // three-source bar by itself.
        var result = PriceObservationValidator.Aggregate("french-bulldog",
            [Obs(2400, 4000, url: "https://www.metlifepetinsurance.com/blog/a/"),
             Obs(2500, 4100, url: "https://www.metlifepetinsurance.com/blog/b/"),
             Obs(2600, 4200, url: "https://www.metlifepetinsurance.com/blog/c/")],
            Strict);

        Assert.Equal(1, result.Price!.SourceCount);
        Assert.Equal(PriceConfidence.SingleSource, result.Price.Confidence);
    }

    [Fact]
    public void ThePublisherRepresentativePrefersARangeAndTheWiderBand()
    {
        // Conservative on purpose: a too-wide band makes the scam check quieter, a
        // too-narrow one makes it accuse honest breeders.
        var collapsed = PriceObservationValidator.CollapseByPublisher(
            [Obs(3000, 3000, kind: FigureKind.Average),
             Obs(2800, 3600),
             Obs(2500, 4000)]);

        var representative = Assert.Single(collapsed);
        Assert.Equal(FigureKind.Range, representative.Kind);
        Assert.Equal(2500, representative.PriceLow);
        Assert.Equal(4000, representative.PriceHigh);
    }

    [Fact]
    public void DifferentSubdomainsOfOnePublisherStillCountSeparatelyOnlyIfTheHostDiffers()
    {
        // HostOf strips "www." but nothing else, so a genuinely different host is a
        // different voice. Documents the boundary rather than asserting a preference.
        var collapsed = PriceObservationValidator.CollapseByPublisher(
            [Obs(2500, 4000, url: "https://www.metlifepetinsurance.com/a/"),
             Obs(2600, 4100, url: "https://metlifepetinsurance.com/b/")]);

        Assert.Single(collapsed);
    }

    // ---------------------------------------------------------------- the range itself

    [Fact]
    public void MedianResistsAnOutlierThatWouldWidenTheBand()
    {
        var result = PriceObservationValidator.Aggregate("french-bulldog",
            [Obs(2500, 4000), Obs(2400, 4100, "Insurify", "https://insurify.com/x"),
             Obs(800, 12_000, "PetMD", "https://www.petmd.com/x")],
            Strict);

        // min/max would have produced $800–$12,000.
        Assert.Equal(2400, result.Price!.PriceLow);
        Assert.Equal(4100, result.Price.PriceHigh);
    }

    // ---------------------------------------------------------------- averages

    [Fact]
    public void AnAverageInsideTheRangeCorroboratesWithoutWideningIt()
    {
        var result = PriceObservationValidator.Aggregate("french-bulldog",
            [Obs(2500, 4000), Obs(2600, 4200, "Insurify", "https://insurify.com/x"),
             Obs(3200, 3200, "PetMD", "https://www.petmd.com/x", kind: FigureKind.Average)],
            Strict);

        Assert.Equal(2550, result.Price!.PriceLow);
        Assert.Equal(4100, result.Price.PriceHigh);
        Assert.Equal(3, result.Price.SourceCount);
        Assert.Equal(PriceConfidence.Verified, result.Price.Confidence);
    }

    [Fact]
    public void AnAverageOutsideTheRangeIsRealDisagreementAndForcesContested()
    {
        // The live case from the dry run: Insurify says ~$5,000 for a Frenchie while
        // MetLife says $2,500-4,000. Averaging that away would hide the conflict.
        var result = PriceObservationValidator.Aggregate("french-bulldog",
            [Obs(2500, 4000), Obs(2600, 4200, "Rover", "https://www.rover.com/x"),
             TierB(2400, 4100, "dogster.com"),
             Obs(5000, 5000, "Insurify", "https://insurify.com/x", kind: FigureKind.Average)],
            Strict);

        Assert.Equal(PriceConfidence.Contested, result.Price!.Confidence);
        Assert.Contains("fall outside", result.Rationale);
    }

    // ---------------------------------------------------------------- spread metric

    [Fact]
    public void SpreadIsMeasuredOnMidpointsNotLows()
    {
        // The regression this rule exists for. Real Beagle figures from the dry run:
        // max(low)/min(low) = 1000/300 = 3.33 would flag a sensible $700-$1,500 band as
        // contested over a $700 absolute difference. Midpoints give 1650/800 = 2.06.
        double[] beagleMidpoints = [800, 800, 1150, 1250, 1650];

        var spread = PriceObservationValidator.SpreadRatio(beagleMidpoints);

        Assert.Equal(2.06, spread!.Value, 2);
    }

    [Fact]
    public void SpreadDiscardsAnOutlierRatherThanLettingItDecide()
    {
        double[] withOutlier = [1000, 1050, 1100, 1150, 9000];

        var spread = PriceObservationValidator.SpreadRatio(withOutlier);

        // 9000 is dropped, so the ratio reflects the four sources that agree. Tukey's
        // IQR rule failed here — the outlier inflated Q3 past its own fence.
        Assert.Equal(1.15, spread!.Value, 2);
    }

    [Fact]
    public void SpreadNeedsTwoPointsToMeanAnything() =>
        Assert.Null(PriceObservationValidator.SpreadRatio([1200]));

    [Fact]
    public void WideMidpointDisagreementForcesContestedEvenWithEnoughSources()
    {
        var result = PriceObservationValidator.Aggregate("beagle",
            [Obs(400, 1200, slug: "beagle"),
             Obs(1400, 2600, "Insurify", "https://insurify.com/x", slug: "beagle"),
             TierB(1500, 2800, "hepper.com") with { BreedSlug = "beagle" }],
            Strict);

        Assert.Equal(PriceConfidence.Contested, result.Price!.Confidence);
        Assert.Contains("disagree", result.Rationale);
    }

    // ---------------------------------------------------------------- confidence gate

    [Fact]
    public void ThreeIndependentSourcesWithATierAAndTightAgreementReachVerified()
    {
        var result = PriceObservationValidator.Aggregate("french-bulldog",
            [Obs(2500, 4000), Obs(2600, 4100, "Rover", "https://www.rover.com/x"),
             TierB(2450, 4200, "caninebible.com")],
            Strict);

        Assert.Equal(PriceConfidence.Verified, result.Price!.Confidence);
        Assert.False(result.NeedsReview);
    }

    [Fact]
    public void TierBAloneNeverReachesVerifiedHoweverManyAgree()
    {
        var result = PriceObservationValidator.Aggregate("french-bulldog",
            [TierB(2500, 4000, "dogster.com"), TierB(2550, 4050, "hepper.com"),
             TierB(2600, 4100, "caninebible.com"), TierB(2480, 4020, "breeds101.com")],
            Strict);

        Assert.Equal(PriceConfidence.Contested, result.Price!.Confidence);
        Assert.Contains("Tier A", result.Rationale);
    }

    [Fact]
    public void TwoSourcesIsNotEnoughUnderTheStrictBar()
    {
        var result = PriceObservationValidator.Aggregate("french-bulldog",
            [Obs(2500, 4000), TierB(2450, 4200, "caninebible.com")], Strict);

        Assert.Equal(PriceConfidence.Contested, result.Price!.Confidence);
        Assert.Contains("3 required", result.Rationale);
    }

    [Fact]
    public void AnythingShortOfVerifiedIsFlaggedForReview()
    {
        var result = PriceObservationValidator.Aggregate("french-bulldog",
            [Obs(2500, 4000), TierB(2450, 4200, "caninebible.com")], Strict);

        // Screening stays off, and a human is asked to look.
        Assert.True(result.NeedsReview);
    }

    // ---------------------------------------------------------------- drift guard

    [Fact]
    public void ALargeMoveInTheLiveValueIsFlaggedEvenWhenWellSourced()
    {
        var current = new BreedPrice("french-bulldog", 1000, 1400, PriceConfidence.Verified, 3, DateTimeOffset.UtcNow);

        var result = PriceObservationValidator.Aggregate("french-bulldog",
            [Obs(2500, 4000), Obs(2600, 4100, "Rover", "https://www.rover.com/x"),
             TierB(2450, 4200, "caninebible.com")],
            Strict, current);

        Assert.Equal(PriceConfidence.Verified, result.Price!.Confidence);
        Assert.True(result.NeedsReview);
        Assert.Contains("moved", result.Rationale);
    }

    // ---------------------------------------------------------------- thresholds are config

    [Fact]
    public void TheSameObservationsGiveDifferentConfidenceUnderADifferentBar()
    {
        // This is what makes deferring the threshold decision free: re-tuning is a
        // re-aggregation over stored rows, not a re-research.
        PriceObservation[] two = [Obs(2500, 4000), TierB(2450, 4200, "caninebible.com")];

        var strict = PriceObservationValidator.Aggregate("french-bulldog", two, Strict);
        var relaxed = PriceObservationValidator.Aggregate("french-bulldog", two,
            Strict with { MinSources = 2 });

        Assert.Equal(PriceConfidence.Contested, strict.Price!.Confidence);
        Assert.Equal(PriceConfidence.Verified, relaxed.Price!.Confidence);
        // Same range either way — only the trust label moves.
        Assert.Equal(strict.Price.PriceLow, relaxed.Price.PriceLow);
    }

    [Fact]
    public void RejectedObservationsAreIgnoredByAggregation()
    {
        var result = PriceObservationValidator.Aggregate("french-bulldog",
            [Obs(2500, 4000), Obs(2600, 4100, "Rover", "https://www.rover.com/x"),
             TierB(2450, 4200, "caninebible.com"),
             Obs(9000, 9500, "PetMD", "https://www.petmd.com/x") with { Status = ObservationStatus.Rejected }],
            Strict);

        Assert.Equal(3, result.Price!.SourceCount);
        Assert.Equal(PriceConfidence.Verified, result.Price.Confidence);
    }
}
