using PuppyFinder.Api.Data;
using PuppyFinder.Api.Models;
using PuppyFinder.Api.Services;

namespace PuppyFinder.Api.Tests;

public class PriceCheckTests
{
    // French Bulldog-ish numbers: a wide, expensive range is where scams cluster.
    private static readonly Breed Frenchie = new(
        "french-bulldog", "French Bulldog", "french-bulldog",
        Size: "Small", Energy: 3, Grooming: 2, Shedding: 3, KidFriendly: 4, ApartmentFriendly: 5,
        PriceLow: 3000, PriceHigh: 6000, Blurb: "");

    // A dog.ceo catalog entry: no verified range.
    private static readonly Breed Unpriced = new(
        "affenpinscher", "Affenpinscher", "affenpinscher",
        Size: "Medium", Energy: 3, Grooming: 3, Shedding: 3, KidFriendly: 3, ApartmentFriendly: 3,
        PriceLow: 0, PriceHigh: 0, Blurb: "");

    private static BreedPrice Verified(int sources = 3) =>
        new("french-bulldog", 3000, 6000, PriceConfidence.Verified, sources, DateTimeOffset.UtcNow);

    [Theory]
    [InlineData(1000, "FarBelow")] // 67% under — the classic bait
    [InlineData(1499, "FarBelow")] // just under half the low end
    [InlineData(1500, "Below")]    // exactly half is not yet "far"
    [InlineData(2900, "Below")]
    [InlineData(3000, "Typical")]  // bounds are inclusive
    [InlineData(4500, "Typical")]
    [InlineData(6000, "Typical")]
    [InlineData(6001, "Above")]
    [InlineData(12000, "Above")]
    public void ClassifiesAQuoteAgainstTheBreedRange(int price, string expectedLevel) =>
        Assert.Equal(expectedLevel, PriceCheck.Evaluate(Frenchie, price, Verified()).Level);

    [Fact]
    public void FarBelowQuotesAreFlaggedAsWarningsWithThePercentage()
    {
        var verdict = PriceCheck.Evaluate(Frenchie, 800, Verified());

        Assert.Equal("FarBelow", verdict.Level);
        Assert.True(verdict.IsWarning);
        Assert.Equal(73, verdict.PercentAway); // (3000-800)/3000
        Assert.Contains("73% below", verdict.Headline);
        Assert.Equal(3000, verdict.PriceLow);
        Assert.Equal(6000, verdict.PriceHigh);
    }

    [Fact]
    public void FreePurebredsAreTreatedAsTheScamScriptTheyUsuallyAre()
    {
        var verdict = PriceCheck.Evaluate(Frenchie, 0, Verified());

        Assert.Equal("Free", verdict.Level);
        Assert.True(verdict.IsWarning);
        Assert.Contains("just pay shipping", verdict.Detail);
    }

    [Fact]
    public void AboveRangeIsNotTreatedAsAScam()
    {
        var verdict = PriceCheck.Evaluate(Frenchie, 9000, Verified());

        Assert.Equal("Above", verdict.Level);
        Assert.False(verdict.IsWarning);
        Assert.Equal(50, verdict.PercentAway); // (9000-6000)/6000
    }

    [Fact]
    public void ATypicalPriceIsNeverPresentedAsAnAllClear()
    {
        // The failure mode that would make this feature harmful: a plausible price
        // reading as "this seller is safe". Competent scammers price realistically.
        var verdict = PriceCheck.Evaluate(Frenchie, 4500, Verified());

        Assert.Equal("Typical", verdict.Level);
        Assert.Contains("not a safety check", verdict.Detail);
        Assert.Contains("mother", verdict.Detail);
    }

    [Fact]
    public void UnpricedBreedsSaySoRatherThanGuessing()
    {
        var verdict = PriceCheck.Evaluate(Unpriced, 800);

        // "No range at all" and "range we can't vouch for" are the same outcome for
        // the reader — no check — so they share a level. The copy still names the breed.
        Assert.Equal("Unavailable", verdict.Level);
        Assert.Null(verdict.PriceLow);
        Assert.Contains("three breeders", verdict.Detail);
        Assert.Contains("Affenpinscher", verdict.Detail);
    }

    [Fact]
    public void NoBreedSelectedAsksForOne()
    {
        var verdict = PriceCheck.Evaluate(null, 800);

        Assert.Equal("Unavailable", verdict.Level);
        Assert.Contains("Pick a breed", verdict.Detail);
    }

    [Fact]
    public void EveryVerdictCarriesActionableDetail()
    {
        int[] prices = [0, 500, 2900, 4500, 9000];

        foreach (var price in prices)
        {
            var verdict = PriceCheck.Evaluate(Frenchie, price, Verified());
            Assert.False(string.IsNullOrWhiteSpace(verdict.Headline));
            Assert.True(verdict.Detail.Length > 80, $"${price} verdict needs real guidance, not a label");
        }
    }

    // ---- confidence: the verdict must never read as more authoritative than its data ----

    private static PuppyFinder.Api.Models.BreedPrice Backing(string confidence, int sources = 0) =>
        new("french-bulldog", 3000, 6000, confidence, sources, DateTimeOffset.UtcNow);

    [Theory]
    [InlineData(PriceConfidence.Unverified)]
    [InlineData(PriceConfidence.SingleSource)]
    [InlineData(PriceConfidence.Contested)]
    public void NoScreeningUntilTheRangeIsSourced(string confidence)
    {
        // Owner decision: don't run fraud detection on numbers we can't attribute.
        // A scam-shaped quote must produce no verdict rather than a confident one.
        var verdict = PriceCheck.Evaluate(Frenchie, 800, Backing(confidence, sources: 2));

        Assert.Equal("Unavailable", verdict.Level);
        Assert.False(verdict.IsWarning);
        // No range leaks out either — the number is the input to the same inference.
        Assert.Null(verdict.PriceLow);
        Assert.Null(verdict.PriceHigh);
        Assert.Null(verdict.PercentAway);
    }

    [Fact]
    public void TheUnavailableVerdictExplainsItselfAndOffersSomethingElse()
    {
        var verdict = PriceCheck.Evaluate(Frenchie, 800, Backing(PriceConfidence.Unverified));

        Assert.Contains("don't have a sourced range", verdict.Detail);
        // It must not just refuse — it points at advice that doesn't depend on price.
        Assert.Contains("three breeders", verdict.Detail);
        Assert.Contains("safety checklist", verdict.Detail);
    }

    [Fact]
    public void MissingBackingIsTreatedAsUnscreenable()
    {
        // A caller that forgets to pass provenance must get the gate, not a verdict.
        var verdict = PriceCheck.Evaluate(Frenchie, 800);

        Assert.Equal("Unavailable", verdict.Level);
    }

    [Theory]
    [InlineData(PriceConfidence.Unverified, false)]
    [InlineData(PriceConfidence.SingleSource, false)]
    [InlineData(PriceConfidence.Contested, false)]
    [InlineData(PriceConfidence.Verified, true)]
    public void CanScreenOnlyOnVerifiedData(string confidence, bool expected) =>
        Assert.Equal(expected, PriceCheck.CanScreen(Backing(confidence)));

    [Fact]
    public void CanScreenIsFalseWithoutAnyBacking() =>
        Assert.False(PriceCheck.CanScreen(null));

    [Fact]
    public void AVerifiedRangeCitesItsSourceCountInsteadOfHedging()
    {
        var verdict = PriceCheck.Evaluate(Frenchie, 4500, Backing(PriceConfidence.Verified, 3));

        Assert.Contains("3 independent sources", verdict.Detail);
        Assert.DoesNotContain("isn't sourced yet", verdict.Detail);
        // The core honesty line survives regardless of confidence.
        Assert.Contains("not a safety check", verdict.Detail);
    }

    [Fact]
    public void AVerifiedRangeStillWarnsHardOnAScamQuote()
    {
        var verdict = PriceCheck.Evaluate(Frenchie, 800, Verified());

        Assert.True(verdict.IsWarning);
        Assert.Equal("FarBelow", verdict.Level);
        Assert.Contains("73% below", verdict.Headline);
    }

    [Fact]
    public void TheNoRangeVerdictIsNotCaveatedTwice()
    {
        // Nothing was measured against, so a provenance caveat would be noise.
        var verdict = PriceCheck.Evaluate(Unpriced, 800, null);

        // No range at all is a different state from "range we can't vouch for".
        Assert.Equal("Unavailable", verdict.Level);
    }
}
