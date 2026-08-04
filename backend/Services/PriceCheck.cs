using PuppyFinder.Api.Data;
using PuppyFinder.Api.Models;

namespace PuppyFinder.Api.Services;

/// <summary>How a quoted price compares to what the breed actually sells for.</summary>
public record PriceVerdict(
    string Level,        // Unknown | Free | FarBelow | Below | Typical | Above
    string Headline,
    string Detail,
    bool IsWarning,      // render as a caution rather than reassurance
    int? PriceLow = null,
    int? PriceHigh = null,
    int? PercentAway = null,  // how far outside the range, as a % of the nearest bound
    /// <summary>
    /// How well the range itself is backed (see <see cref="PriceConfidence"/>). The UI
    /// must not present a verdict as more authoritative than the data behind it.
    /// </summary>
    string Confidence = PriceConfidence.Unverified,
    int SourceCount = 0,
    /// <summary>
    /// Which kind of evidence produced the range (see <see cref="PriceBasis"/>). The copy has
    /// to branch on it: "20 independent sources" and "20 puppies listed for sale" are very
    /// different claims, and the first was being made about the second.
    /// </summary>
    string Basis = PriceBasis.Editorial);

/// <summary>
/// Screens a quoted puppy price against the breed's typical range.
///
/// This is the highest-signal scam check available to a buyer: a price far below
/// market is the single most reported hook in puppy fraud, and it's checkable with
/// data we already carry. The inverse matters just as much though — a *plausible*
/// price is not a safety signal, and every verdict says so, because the one thing
/// worse than no check is a check that reads as an all-clear.
/// </summary>
public static class PriceCheck
{
    /// <summary>Below this fraction of the low end, price alone is a serious red flag.</summary>
    private const double FarBelowFactor = 0.5;

    /// <summary>
    /// True when we can defensibly screen a quote for this breed — i.e. the range
    /// came from independent cited sources.
    /// </summary>
    public static bool CanScreen(BreedPrice? backing) =>
        backing?.Confidence == PriceConfidence.Verified;

    public static PriceVerdict Evaluate(Breed? breed, int price, BreedPrice? backing = null)
    {
        // Owner decision (July 2026): no scam screening until the database holds
        // sourced ranges. Enforced here rather than only in the UI, so no caller —
        // API consumer, future job, or a component someone forgets to gate — can
        // render a fraud verdict measured against a number we can't attribute.
        //
        // This is deliberately data-driven, not a feature flag: each breed starts
        // screening the moment its range reaches `verified`, and nothing has to be
        // switched on by hand.
        if (!CanScreen(backing))
        {
            return Unavailable(breed, backing);
        }

        return WithConfidenceCaveat(EvaluateAgainstRange(breed, price, backing), backing);
    }

    private static PriceVerdict Unavailable(Breed? breed, BreedPrice? backing) => new(
        "Unavailable",
        "We're not price-checking this breed yet",
        breed is null
            ? "Pick a breed to see whether we can check quotes for it yet."
            : $"Telling you a {breed.DisplayName} quote is a scam means measuring it against a "
              + "number we can stand behind, and we don't have a sourced range for this breed yet. "
              + "We'd rather say nothing than wrongly accuse a legitimate breeder — or reassure you "
              + "about a real scam. Until then: get quotes from three breeders, treat the one that "
              + "sharply undercuts the others as the outlier rather than the bargain, and run the "
              + "safety checklist, which doesn't depend on price at all.",
        IsWarning: false,
        Confidence: backing?.Confidence ?? PriceConfidence.Unverified,
        SourceCount: backing?.SourceCount ?? 0,
        Basis: backing?.Basis ?? PriceBasis.Editorial);

    /// <summary>
    /// Appends a plain statement of how well the range is backed. Without this the
    /// verdict reads with the same authority whether it's measured against three
    /// cited sources or a number nobody can source — which is the exact failure this
    /// whole pipeline exists to fix.
    /// </summary>
    private static PriceVerdict WithConfidenceCaveat(PriceVerdict verdict, BreedPrice? backing)
    {
        // Nothing to caveat: no range was used in the first place.
        if (verdict.Level == "Unknown")
        {
            return verdict;
        }

        var caveat = verdict.Confidence switch
        {
            // Say what the evidence actually is. Afghan Hound's range is the middle half of 20
            // live listings, and this line called it "20 independent sources" — crediting
            // published research that had nothing to do with the number.
            PriceConfidence.Verified when verdict.Basis == PriceBasis.Listings =>
                $" That's the middle half of {verdict.SourceCount} puppies listed for sale right"
                + " now — tap to see the sample.",
            PriceConfidence.Verified =>
                $" This range comes from {verdict.SourceCount} independent sources — tap to see them.",
            PriceConfidence.Contested =>
                " Treat that range loosely: our sources disagree materially about this breed, so the"
                + " spread is wide and the midpoint means little. Three local quotes will tell you more.",
            PriceConfidence.SingleSource =>
                " That range rests on a single source, so treat it as a rough marker rather than a"
                + " going rate.",
            // Includes the original hardcoded numbers. Say it outright.
            _ => " One caveat on the range itself: it's our own estimate and isn't sourced yet, so"
                 + " use it as a rough orientation only and get three quotes of your own.",
        };

        return verdict with { Detail = verdict.Detail + caveat };
    }

    private static PriceVerdict EvaluateAgainstRange(Breed? breed, int price, BreedPrice? backing)
    {
        var confidence = backing?.Confidence ?? PriceConfidence.Unverified;
        var sources = backing?.SourceCount ?? 0;
        var basis = backing?.Basis ?? PriceBasis.Editorial;

        // Curated breeds carry real ranges; the dog.ceo catalog entries don't
        // (PriceHigh 0). Saying so beats inventing a number to judge against.
        if (breed is null || breed.PriceHigh <= 0 || breed.PriceLow <= 0)
        {
            return new PriceVerdict(
                "Unknown",
                "We don't have a verified price range for this breed",
                breed is null
                    ? "Pick a breed from the list and we'll compare the quote against its typical range."
                    : $"We don't have a range for {breed.DisplayName} yet. "
                      + "Get quotes from at least three breeders to establish the going rate yourself — "
                      + "and treat any one that undercuts the others sharply as the outlier, not the bargain.",
                IsWarning: false,
                Confidence: confidence,
                SourceCount: sources,
                Basis: basis);
        }

        var low = breed.PriceLow;
        var high = breed.PriceHigh;
        var range = $"${low:n0}–${high:n0}";

        if (price <= 0)
        {
            return new PriceVerdict(
                "Free",
                $"A free {breed.DisplayName} is not a bargain",
                $"{breed.DisplayName} puppies typically sell for {range}. Nobody gives away a dog worth that "
                + "by accident. \"Free to a good home, just pay shipping\" is one of the oldest scam scripts "
                + "there is — the shipping fee is the product, and the puppy doesn't exist.",
                IsWarning: true,
                low, high, null, confidence, sources, basis);
        }

        if (price < low * FarBelowFactor)
        {
            return new PriceVerdict(
                "FarBelow",
                $"${price:n0} is {PercentBelow(price, low)}% below the typical {range}",
                "A price far under market is the most common puppy-scam signal there is. If the seller also "
                + "won't do a live video call on your terms, wants a wire transfer, gift cards, Zelle or crypto, "
                + "or adds fees after you commit, walk away — those four together are the whole playbook.",
                IsWarning: true,
                low, high, PercentBelow(price, low), confidence, sources, basis);
        }

        if (price < low)
        {
            return new PriceVerdict(
                "Below",
                $"${price:n0} is a little under the typical {range}",
                "This can be legitimate — pet-quality rather than show, an older puppy, a local pickup with no "
                + "transport, or a breeder without champion lines. Ask directly why it's priced below market. "
                + "A real breeder has a straight answer; a scammer has a story.",
                IsWarning: false,
                low, high, PercentBelow(price, low), confidence, sources, basis);
        }

        if (price > high)
        {
            return new PriceVerdict(
                "Above",
                $"${price:n0} is {PercentAbove(price, high)}% above the typical {range}",
                "Being expensive isn't a scam signal by itself — health-tested parents, champion pedigree, or a "
                + "rare colour all cost more. But make them show you what you're paying for: OFA or Embark "
                + "results for both parents, registration papers, and the contract. \"Rare\" colours often mean "
                + "disqualifying ones that carry health problems.",
                IsWarning: false,
                low, high, PercentAbove(price, high), confidence, sources, basis);
        }

        return new PriceVerdict(
            "Typical",
            $"${price:n0} is in the typical {range}",
            "Price isn't a red flag here — but a believable price is not a safety check. Competent scammers "
            + "price realistically for exactly this reason. Everything else still has to hold up: see the puppy "
            + "and its mother live, get health testing on paper, and never send money by wire, gift card or crypto.",
            IsWarning: false,
            low, high, null, confidence, sources, basis);
    }

    private static int PercentBelow(int price, int low) => (int)Math.Round((low - price) * 100.0 / low);

    private static int PercentAbove(int price, int high) => (int)Math.Round((price - high) * 100.0 / high);
}
