using PuppyFinder.Api.Data;
using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <summary>Why an observation was refused. Recorded so a rejection is auditable.</summary>
public record RejectedObservation(PriceObservation Observation, string Reason);

/// <summary>
/// Why a range wants attention. Two very different situations used to share one boolean, which
/// is part of why the review queue built on it was unusable: on the live data 50 of 59 breeds
/// are verified, but most of the 25 breeds with editorial observations sit under the
/// three-source bar — so a single "needs review" flag was mostly "we don't have enough sources
/// yet", a state no human decision resolves. Only <see cref="Drifted"/> is worth surfacing, and
/// it is logged as a warning when it happens; see docs/PRICE-SEARCH.md.
/// </summary>
public static class PriceReviewReason
{
    /// <summary>The published midpoint moved sharply. Rare, and worth looking at today.</summary>
    public const string Drifted = "drifted";

    /// <summary>Short of the verified bar. Waiting on more evidence, not on a decision.</summary>
    public const string BelowBar = "below-bar";
}

/// <summary>What aggregation concluded, and enough detail to explain the conclusion.</summary>
public record PriceAggregation(
    BreedPrice? Price,
    /// <summary>Observations that fed the range, after scope filtering and independence collapse.</summary>
    IReadOnlyList<PriceObservation> Counted,
    /// <summary>Human-readable reason for the confidence level, carried into the logs.</summary>
    string Rationale,
    /// <summary>A <see cref="PriceReviewReason"/>, or null when nothing wants attention.</summary>
    string? ReviewReason = null);

/// <summary>
/// The trust rules for researched prices: what may be recorded, what may be counted,
/// and what a range has to clear to be believed.
///
/// Pure and I/O-free by design — this is the part that decides whether the app will
/// accuse a breeder of fraud, so it must be exhaustively testable without a network.
/// Every rule here came from actually running the research by hand first; the
/// commentary notes which ones were wrong on the first attempt.
/// </summary>
public static class PriceObservationValidator
{
    private const int MinQuoteLength = 20;
    private const int AbsoluteFloor = 100;      // below this, not a purebred puppy price
    private const int AbsoluteCeiling = 25_000; // above this, not a mainstream figure
    private const int MaxRangeWidthFactor = 10; // a 10x band isn't a range, it's a shrug
    private const int MaxSourceAgeMonths = 36;  // older than this describes a past market
    private const double MaxSourceAgeDays = MaxSourceAgeMonths * 30.44;

    /// <summary>
    /// Hard rejects: malformed rather than debatable, so they are refused outright instead of
    /// being recorded as something anyone needs to weigh.
    /// </summary>
    public static string? Reject(PriceObservation o)
    {
        if (PriceSources.HostOf(o.SourceUrl) is null)
        {
            return "source_url is missing or not an http(s) URL";
        }

        if (PriceSources.IsBlocked(o.SourceUrl))
        {
            // Sellers price their own stock, and the classifieds are what the scam
            // check screens against — either would corrupt the floor.
            return $"{PriceSources.HostOf(o.SourceUrl)} is excluded as a price authority";
        }

        if (PriceSources.TierFor(o.SourceUrl) is null)
        {
            return $"{PriceSources.HostOf(o.SourceUrl)} is not on the reviewed source list";
        }

        if (string.IsNullOrWhiteSpace(o.Quote) || o.Quote.Trim().Length < MinQuoteLength)
        {
            return "no verbatim quote supporting the figure";
        }

        if (!PriceScope.IsKnown(o.Scope))
        {
            return $"unknown scope '{o.Scope}'";
        }

        if (!FigureKind.IsKnown(o.Kind))
        {
            return $"unknown figure kind '{o.Kind}'";
        }

        // An average is a single point, so low == high is correct for it.
        if (o.Kind == FigureKind.Range && o.PriceLow >= o.PriceHigh)
        {
            return "range low is not below its high";
        }

        if (o.Kind == FigureKind.Average && o.PriceLow != o.PriceHigh)
        {
            return "an average must carry the same value as low and high";
        }

        if (o.PriceLow < AbsoluteFloor || o.PriceHigh > AbsoluteCeiling)
        {
            return $"outside the plausible ${AbsoluteFloor:n0}–${AbsoluteCeiling:n0} band";
        }

        if (o.PriceHigh > o.PriceLow * MaxRangeWidthFactor)
        {
            return $"range spans more than {MaxRangeWidthFactor}x, too wide to be informative";
        }

        // Stale figures, only when the source dates itself. Puppy prices moved sharply
        // over the pandemic and after, so a figure old enough to predate that is evidence
        // about a market that no longer exists — and the scam check would measure today's
        // quotes against it. An undated source isn't punished here; it just can't reach
        // Tier A standing on its own.
        //
        // Found on PetMD's French Bulldog page: "$1,500–$5,000", updated March 2023. A
        // plausible-looking Tier A figure that is simply out of date.
        if (o.PublishedAt is { } published
            && (o.RetrievedAt - published).TotalDays > MaxSourceAgeDays)
        {
            var months = (int)Math.Round((o.RetrievedAt - published).TotalDays / 30.44);
            return $"source is dated {published:yyyy-MM-dd}, {months} months old — older than "
                + $"the {MaxSourceAgeMonths}-month limit";
        }

        return null;
    }

    public static (List<PriceObservation> Kept, List<RejectedObservation> Rejected) Partition(
        IEnumerable<PriceObservation> observations)
    {
        List<PriceObservation> kept = [];
        List<RejectedObservation> rejected = [];
        foreach (var o in observations)
        {
            if (Reject(o) is { } reason)
            {
                rejected.Add(new RejectedObservation(o, reason));
            }
            else
            {
                kept.Add(o with { PublisherTier = PriceSources.TierFor(o.SourceUrl)! });
            }
        }

        return (kept, rejected);
    }

    /// <summary>
    /// Collapses observations that report byte-identical figures from different domains
    /// down to one. Two sites quoting exactly $1,200–$2,400 is copied content, not
    /// independent corroboration, and counting it twice is how a single unsourced
    /// number would launder itself into "verified".
    /// </summary>
    public static List<PriceObservation> CollapseDuplicates(IEnumerable<PriceObservation> observations) =>
        observations
            .GroupBy(o => (o.PriceLow, o.PriceHigh, o.Kind))
            // Keep the most accountable publisher as the representative.
            .Select(g => g.OrderBy(o => o.PublisherTier == PublisherTier.A ? 0 : 1).First())
            .ToList();

    /// <summary>
    /// One vote per publisher. A page that states several pet-quality figures — a range in
    /// the body and an average in a comparison table, say — is still one editorial voice,
    /// and must not supply two of the three "independent sources" that unlock a live scam
    /// check.
    ///
    /// Found while gathering French Bulldog figures by hand: Insurify publishes both
    /// "around $5,000 ... on average" and a "$2,000–$8,000" table range, both pet-quality.
    /// Counting rows rather than publishers let one page do most of the work of clearing
    /// the bar, which is the opposite of what the bar is for. The prompt is *right* to emit
    /// one row per figure — the aggregation is where they have to be reconciled.
    ///
    /// The representative is chosen conservatively: a range beats an average (only a range
    /// can define a band), and among ranges the widest wins. A too-wide band makes the
    /// scam check quieter; a too-narrow one makes it accuse honest breeders.
    /// </summary>
    public static List<PriceObservation> CollapseByPublisher(IEnumerable<PriceObservation> observations) =>
        observations
            .GroupBy(o => PriceSources.HostOf(o.SourceUrl) ?? o.Publisher, StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderBy(o => o.Kind == FigureKind.Range ? 0 : 1)
                .ThenByDescending(o => o.PriceHigh - o.PriceLow)
                .First())
            .ToList();

    /// <summary>
    /// Derives the live range and its confidence from stored observations.
    ///
    /// Deliberately a pure function over the observation table rather than something the
    /// research job computes inline: thresholds can then be re-tuned for free, with no
    /// re-research and no API spend. <see cref="PriceRun"/> gathers; this decides.
    /// </summary>
    public static PriceAggregation Aggregate(
        string breedSlug,
        IEnumerable<PriceObservation> observations,
        PriceThresholds thresholds,
        BreedPrice? current = null,
        DateTimeOffset? now = null)
    {
        var timestamp = now ?? DateTimeOffset.UtcNow;

        // Only national pet-quality figures are comparable. Rare-colour premiums,
        // show prospects, regional bands and rescue fees are all real data about
        // *different questions* — mixing them is what made raw figures look like
        // wild disagreement when they mostly weren't.
        // Two collapses, guarding two different ways the source count can be inflated:
        // CollapseByPublisher stops one page counting twice, CollapseDuplicates stops the
        // same figure syndicated across domains counting twice. Publisher first, so the
        // cross-domain check compares one representative figure per voice.
        var usable = CollapseDuplicates(CollapseByPublisher(observations
            .Where(o => o.Status != ObservationStatus.Rejected)
            .Where(o => o.Scope == PriceScope.PetStandard)
            // Re-derive the tier from the reviewed source list rather than trusting the
            // stored value. Tier gates the path to Verified, so a row written by an older
            // build, a bug, or a model that mislabelled itself must not be able to grant
            // itself Tier A standing.
            .Select(o => o with { PublisherTier = PriceSources.TierFor(o.SourceUrl) ?? PublisherTier.B })));

        var ranges = usable.Where(o => o.Kind == FigureKind.Range).ToList();
        var averages = usable.Where(o => o.Kind == FigureKind.Average).ToList();

        if (ranges.Count == 0)
        {
            // No usable published figure means no editorial range — return nothing rather
            // than the existing row's numbers relabelled.
            //
            // Relabelling caused a self-referential ratchet. Akita has no editorial source and
            // no seed, so its stored row held a listings-derived $1,000-$2,000; this method
            // handed those numbers back as "the editorial range", and they became the floor
            // guard for the next listing run. A better-sampled $650-$1,650 from 69 listings
            // was then refused for sitting below "the published low" — which was our own
            // earlier output from the very same source. The guard exists to stop a
            // marketplace validating itself, and this let it do exactly that, one run removed.
            return new PriceAggregation(
                null,
                [],
                averages.Count > 0
                    ? "no source published a range — averages alone can't define one"
                    : "no usable pet-quality figures found");
        }

        // Median, not min/max: one outlying source must not be able to widen the band
        // the scam check measures against.
        var low = (int)Math.Round(Median(ranges.Select(o => (double)o.PriceLow)));
        var high = (int)Math.Round(Median(ranges.Select(o => (double)o.PriceHigh)));

        // An average corroborates when it lands inside the band, and contradicts when it
        // doesn't. Insurify's "$5,000" against MetLife's $2,500–4,000 is genuine
        // disagreement, and this is where it surfaces instead of being averaged away.
        var corroborating = averages.Where(a => a.PriceLow >= low && a.PriceLow <= high).ToList();
        var contradicting = averages.Except(corroborating).ToList();

        var counted = ranges.Concat(corroborating).ToList();
        var spread = SpreadRatio(counted.Select(o => o.Midpoint));
        var hasTierA = counted.Any(o => o.PublisherTier == PublisherTier.A);

        var (confidence, rationale) = Classify(
            counted.Count, hasTierA, spread, contradicting.Count, thresholds, low, high);

        // A big move in the number the fraud check uses is noted here and *decided* at publish
        // time, in PriceRefreshJob.ReaggregateBreedAsync. It used to be decided here, by
        // downgrading to Contested, which was wrong in two ways: the downgrade published the new
        // figures anyway (so the hold lasted exactly one run, until the next run compared them
        // against the row it had just written), and it could only see the editorial range — a
        // sharp move in a listing-derived range, which is the path that actually runs today, was
        // never gated at all.
        //
        // Reported, not enforced: this range may lose precedence to a listing sample, so what
        // matters is how far the *published* range moves, and only the publish path knows that.
        var drift = PriceDrift.Percent(current, low, high);
        var drifted = drift is { } d && d > thresholds.DriftReviewPercent;

        // Drift wins when both apply: it is the case someone can act on today, and it would
        // otherwise be buried under every under-sourced breed.
        var reviewReason = drifted
            ? PriceReviewReason.Drifted
            : confidence != PriceConfidence.Verified ? PriceReviewReason.BelowBar : null;
        if (drifted)
        {
            rationale += $"; midpoint moved {drift}% from the live value";
        }

        return new PriceAggregation(
            new BreedPrice(breedSlug, low, high, confidence, counted.Count, timestamp, spread),
            counted,
            rationale,
            reviewReason);
    }

    private static (string Confidence, string Rationale) Classify(
        int sources, bool hasTierA, double? spread, int contradicting, PriceThresholds t,
        int low, int high)
    {
        var band = low > 0 ? (double)high / low : double.PositiveInfinity;

        if (sources == 0)
        {
            return (PriceConfidence.Unverified, "no usable sources");
        }

        if (sources == 1)
        {
            return (PriceConfidence.SingleSource, "only one independent source");
        }

        if (contradicting > 0)
        {
            return (PriceConfidence.Contested,
                $"{contradicting} published average(s) fall outside the aggregated range");
        }

        if (sources < t.MinSources)
        {
            return (PriceConfidence.Contested,
                $"{sources} independent sources, {t.MinSources} required");
        }

        if (t.RequireTierA && !hasTierA)
        {
            return (PriceConfidence.Contested,
                "no editorially accountable (Tier A) source among them");
        }

        if (spread is { } ratio && ratio > t.MaxSpreadRatio)
        {
            return (PriceConfidence.Contested,
                $"sources disagree {ratio:0.00}x on midpoint, limit {t.MaxSpreadRatio:0.00}x");
        }

        // Sources agreeing is not the same as the band being usable. See
        // PriceThresholds.MaxVerifiedBandRatio — Dachshund's $500-$3,500 cleared every
        // other rule.
        if (band > t.MaxVerifiedBandRatio)
        {
            return (PriceConfidence.Contested,
                $"the resulting ${low:n0}–${high:n0} band spans {band:0.00}x, too wide to screen "
                + $"against (limit {t.MaxVerifiedBandRatio:0.00}x)");
        }

        return (PriceConfidence.Verified,
            $"{sources} independent sources including Tier A, agreeing within {spread:0.00}x");
    }

    /// <summary>
    /// How much sources disagree about what the breed costs, as max ÷ min of their
    /// midpoints after discarding outliers.
    ///
    /// Two corrections are baked in here, both found by testing rather than reasoning:
    ///
    /// The first version used max(low) ÷ min(low), which measured the wrong thing — for
    /// Beagle it scored 3.33 (from $300 vs $1,000 lows) over a $700 absolute difference,
    /// flagging a sensible $700–$1,500 band as contested. It detected "one source quoted
    /// a wide band", not "sources disagree".
    ///
    /// The second used Tukey's 1.5 × IQR rule, which silently fails on the case it was
    /// added for: with five points and one extreme, the extreme sits in the upper half
    /// and inflates Q3 enough to include itself (for [1000,1050,1100,1150,9000] the upper
    /// fence landed at 11,150). Median absolute deviation has no such feedback loop.
    /// </summary>
    public static double? SpreadRatio(IEnumerable<double> midpoints)
    {
        var values = midpoints.Where(m => m > 0).OrderBy(m => m).ToList();
        if (values.Count < 2)
        {
            return null;
        }

        var trimmed = TrimOutliers(values);
        var min = trimmed.Min();
        return min <= 0 ? null : trimmed.Max() / min;
    }

    /// <summary>
    /// Drops outliers by modified z-score (median absolute deviation), |z| &gt; 3.5 —
    /// the conventional cutoff. MAD is used rather than IQR because a single extreme
    /// value can't drag the threshold out far enough to protect itself.
    /// </summary>
    private static List<double> TrimOutliers(List<double> sorted)
    {
        // Below four points, discarding one of three sources does more harm than the
        // outlier would.
        if (sorted.Count < 4)
        {
            return sorted;
        }

        var median = Median(sorted);
        var mad = Median(sorted.Select(v => Math.Abs(v - median)));

        // Degenerate case (most values identical): MAD is 0 and nothing is
        // discriminable. Keeping everything is the conservative choice — a wider
        // spread means Contested, which errs toward not screening.
        if (mad <= 0)
        {
            return sorted;
        }

        var kept = sorted.Where(v => 0.6745 * Math.Abs(v - median) / mad <= 3.5).ToList();
        return kept.Count >= 2 ? kept : sorted;
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0)
        {
            return 0;
        }

        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

}

/// <summary>
/// How far a proposed range moves the midpoint. Shared, because two copies of this arithmetic
/// would eventually disagree and it decides whether a change needs a person's approval.
/// </summary>
public static class PriceDrift
{
    /// <summary>Percentage the midpoint would move, or null when there's nothing to compare.</summary>
    public static int? Percent(BreedPrice? current, int low, int high)
    {
        if (current is null || current.PriceLow <= 0)
        {
            return null;
        }

        var before = (current.PriceLow + current.PriceHigh) / 2.0;
        if (before <= 0)
        {
            return null;
        }

        return (int)Math.Round(Math.Abs((low + high) / 2.0 - before) / before * 100);
    }
}
