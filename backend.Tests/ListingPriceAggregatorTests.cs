using PuppyFinder.Api.Data;
using PuppyFinder.Api.Models;
using PuppyFinder.Api.Services;

namespace PuppyFinder.Api.Tests;

/// <summary>
/// Real asking prices are better raw material than published summaries — more of them,
/// current, and actually what a buyer faces. They carry one specific danger, and it is the
/// thing most of these tests are about: the cheap tail of a classifieds marketplace is
/// exactly what the scam check exists to flag, so a range calibrated to it would be
/// circular. Measured on Beagle, where listings gave $400-$900 against a published
/// $400-$1,200.
/// </summary>
public class ListingPriceAggregatorTests
{
    private static readonly PriceThresholds Strict = new();

    private static List<ListingPrice> Sample(params int[] prices) =>
        [.. prices.Select((price, index) => new ListingPrice(
            BreedSlug: "french-bulldog",
            Price: price,
            SourceHost: ListingSources.Host,
            ListingRef: $"https://www.puppies.com/listings/{index:D4}",
            ListingName: "French Bulldog - F",
            RetrievedAt: DateTimeOffset.UnixEpoch.AddDays(index),
            RunId: "listings-test"))];

    /// <summary>Prices clustered around a midpoint, so the IQR band is narrow and sane.</summary>
    private static List<ListingPrice> HealthySample(int count = 24, int centre = 2500) =>
        Sample([.. Enumerable.Range(0, count).Select(i => centre + (i % 6 - 3) * 200)]);

    // ---------------------------------------------------------------- sample size

    [Fact]
    public void TooFewListingsProducesNoRange()
    {
        var result = ListingPriceAggregator.Aggregate(
            "french-bulldog", Sample(1500, 2000, 2500), Strict);

        Assert.Null(result.Price);
        Assert.Contains("20 required", result.Rationale);
    }

    [Fact]
    public void ASampleAtTheThresholdIsEnough()
    {
        var result = ListingPriceAggregator.Aggregate("french-bulldog", HealthySample(20), Strict);

        Assert.NotNull(result.Price);
        Assert.Equal(20, result.SampleSize);
    }

    // ---------------------------------------------------------------- the band

    [Fact]
    public void TheBandIsTheMiddleHalfNotTheExtremes()
    {
        // One $400 scam listing and one $15,000 rare-colour listing among ordinary ones.
        // Min-max would publish $400-$15,000, which describes nothing.
        var listings = Sample(400, 1800, 2000, 2000, 2200, 2200, 2400, 2400, 2500, 2500,
                              2600, 2600, 2800, 2800, 3000, 3000, 3200, 3200, 3400, 3600,
                              3800, 15_000);

        var result = ListingPriceAggregator.Aggregate("french-bulldog", listings, Strict);

        Assert.NotNull(result.Price);
        Assert.True(result.Price!.PriceLow >= 2000, $"low was {result.Price.PriceLow}");
        Assert.True(result.Price.PriceHigh <= 3400, $"high was {result.Price.PriceHigh}");
    }

    [Fact]
    public void ImplausiblePricesAreDiscardedBeforePercentiles()
    {
        // "Free to good home" at $1 and a $90,000 typo are not asking prices for a puppy.
        var listings = Sample([.. Enumerable.Repeat(2500, 22), 1, 90_000]);

        var result = ListingPriceAggregator.Aggregate("french-bulldog", listings, Strict);

        Assert.Equal(22, result.SampleSize);
    }

    [Fact]
    public void ASampleWhereEveryListingCostsTheSameCannotDescribeARange()
    {
        var result = ListingPriceAggregator.Aggregate(
            "french-bulldog", Sample([.. Enumerable.Repeat(2500, 24)]), Strict);

        Assert.Null(result.Price);
        Assert.Contains("single price", result.Rationale);
    }

    [Fact]
    public void TheSameListingFetchedTwiceCountsOnce()
    {
        // The unique index only protects within a run; across runs the same animal
        // reappears, and counting it twice moves the percentiles while looking like a
        // bigger sample. This matters more now that samples are pooled across runs.
        var once = HealthySample(24);
        var twice = once.Concat(once.Select(l => l with { RunId = "listings-later" })).ToList();

        var result = ListingPriceAggregator.Aggregate("french-bulldog", twice, Strict);

        Assert.Equal(24, result.SampleSize);
    }

    [Fact]
    public void ARelistedAnimalCountsAtItsMostRecentPrice()
    {
        // Same listing, price dropped on a later run. The newer figure is the live asking
        // price; averaging the two would invent a number nobody is charging.
        var original = Sample(3000)[0];
        var reduced = original with
        {
            Price = 2000,
            RetrievedAt = original.RetrievedAt.AddDays(30),
            RunId = "listings-later",
        };
        var rest = Sample([.. Enumerable.Repeat(2500, 23)])
            .Select((l, i) => l with { ListingRef = $"other-{i}" });

        var result = ListingPriceAggregator.Aggregate(
            "french-bulldog", [original, reduced, .. rest], Strict);

        Assert.Equal(24, result.SampleSize);
        // 3000 must be gone: with 23 listings at 2500 the band is degenerate unless the
        // relisted one contributes exactly once.
        Assert.DoesNotContain(3000, new[] { result.Price?.PriceLow ?? 0, result.Price?.PriceHigh ?? 0 });
    }

    [Fact]
    public void PoolingAcrossRunsIsWhatMakesTheSampleBigEnough()
    {
        // The property that forced pooling: two runs forty minutes apart returned ZERO
        // overlapping listings for most breeds, so a single run is a small random slice of a
        // much larger pool. Australian Shepherd swung from a verified $800-$1,500 to a
        // refused $500 floor between two runs. Disjoint runs mean pooling adds genuinely new
        // observations rather than re-confirming the same ones.
        var runOne = HealthySample(12).Select((l, i) => l with { ListingRef = $"run1-{i}", RunId = "r1" });
        var runTwo = HealthySample(12).Select((l, i) => l with { ListingRef = $"run2-{i}", RunId = "r2" });

        var alone = ListingPriceAggregator.Aggregate("french-bulldog", runOne, Strict);
        var pooled = ListingPriceAggregator.Aggregate("french-bulldog", [.. runOne, .. runTwo], Strict);

        Assert.Null(alone.Price);              // 12 listings, below the floor of 20
        Assert.NotNull(pooled.Price);          // 24 pooled, over it
        Assert.Equal(24, pooled.SampleSize);
    }

    // ------------------------------------------------- one seller's litter

    [Fact]
    public void ASampleDominatedByOneRepeatedPriceIsRefused()
    {
        // The real Irish Wolfhound case: 18 of 27 listings at exactly $2,000, published as a
        // $2,000-$2,100 band. That is 1.05x wide, almost certainly one breeder's litter, and it
        // would have flagged a legitimate $2,500 Wolfhound as above typical — the
        // "too-narrow band accuses honest breeders" failure, arriving from inside the sample.
        var litter = Sample([
            .. Enumerable.Repeat(2000, 18),
            2100, 2100, 2200, 2500, 2500, 2800, 3000, 3200, 3500]);

        var result = ListingPriceAggregator.Aggregate("irish-wolfhound", litter, Strict);

        Assert.Equal(PriceConfidence.Contested, result.Price!.Confidence);
        Assert.Contains("one seller's litter", result.Rationale);
    }

    [Fact]
    public void RepeatedRoundNumbersAreFineWhenTheSampleIsBroad()
    {
        // Round numbers repeat legitimately — 27 of 109 Bernese listings are $1,500, from what
        // are plainly different breeders. The guard must not punish that, or it would refuse
        // most healthy samples: nearly every breed has one popular price point.
        var broad = Sample([
            .. Enumerable.Repeat(1500, 8),
            .. Enumerable.Range(0, 24).Select(i => 1200 + i * 50)]);

        var result = ListingPriceAggregator.Aggregate("bernese-mountain-dog", broad, Strict);

        Assert.Equal(PriceConfidence.Verified, result.Price!.Confidence);
    }

    [Fact]
    public void TheConcentrationLimitIsConfigurable()
    {
        var litter = Sample([.. Enumerable.Repeat(2000, 18), .. Enumerable.Range(0, 9).Select(i => 2100 + i * 100)]);

        var lenient = ListingPriceAggregator.Aggregate(
            "irish-wolfhound", litter, Strict with { MaxSinglePriceShare = 0.9 });

        Assert.Equal(PriceConfidence.Verified, lenient.Price!.Confidence);
    }

    [Fact]
    public void ConcentrationIsCheckedBeforeBandWidth()
    {
        // Order matters and isn't arbitrary: a dominated sample produces a deceptively *tight*
        // band, so the band-width rule can never catch it. Reporting "band too wide" here
        // would also be simply untrue.
        var litter = Sample([.. Enumerable.Repeat(2000, 18), .. Enumerable.Range(0, 9).Select(i => 2050 + i * 10)]);

        var result = ListingPriceAggregator.Aggregate("irish-wolfhound", litter, Strict);

        Assert.Contains("one seller's litter", result.Rationale);
        Assert.DoesNotContain("middle half spans", result.Rationale);
    }

    // ---------------------------------------------------------------- the floor guard

    [Fact]
    public void AListingRangeFarBelowThePublishedLowIsNotPublished()
    {
        // The Beagle shape: a marketplace whose middle half sits well under what every
        // publisher reports. Accepting it would teach the check that the cheap tail is
        // normal — screening classifieds quotes against a classifieds baseline.
        var cheap = Sample([.. Enumerable.Range(0, 24).Select(i => 200 + i % 4 * 50)]);
        var published = new BreedPrice("beagle", 800, 1500, PriceConfidence.Verified, 3, DateTimeOffset.UnixEpoch);

        var result = ListingPriceAggregator.Aggregate("beagle", cheap, Strict, published);

        Assert.Equal(PriceConfidence.Contested, result.Price!.Confidence);
        Assert.Contains("cheap tail", result.Rationale);
    }

    [Fact]
    public void AListingRangeConsistentWithPublishedSourcesIsVerified()
    {
        var published = new BreedPrice("french-bulldog", 1500, 4000, PriceConfidence.Contested, 4, DateTimeOffset.UnixEpoch);

        var result = ListingPriceAggregator.Aggregate(
            "french-bulldog", HealthySample(24, centre: 2500), Strict, published);

        Assert.Equal(PriceConfidence.Verified, result.Price!.Confidence);
        Assert.Contains("live listings", result.Rationale);
    }

    [Fact]
    public void WithNoPublishedRangeTheGuardCannotFireAndListingsStandAlone()
    {
        // Most breeds have no editorial range at all. Refusing them entirely would throw
        // away the coverage that makes listings worth using; the band-width rule still
        // applies, so this isn't unguarded.
        var result = ListingPriceAggregator.Aggregate(
            "akita", HealthySample(24), Strict, editorial: null);

        Assert.Equal(PriceConfidence.Verified, result.Price!.Confidence);
    }

    [Fact]
    public void AListingRangeAboveThePublishedRangeIsAllowed()
    {
        // The guard is one-directional on purpose. Listings running higher than published
        // articles is expected — articles lag a rising market — and a higher floor makes
        // the scam check stricter, not weaker.
        var published = new BreedPrice("french-bulldog", 1500, 2500, PriceConfidence.Verified, 3, DateTimeOffset.UnixEpoch);

        var result = ListingPriceAggregator.Aggregate(
            "french-bulldog", HealthySample(24, centre: 4000), Strict, published);

        Assert.Equal(PriceConfidence.Verified, result.Price!.Confidence);
    }

    // ---------------------------------------------------------------- band width

    [Fact]
    public void AMiddleHalfTooWideToScreenAgainstIsHeldBack()
    {
        // Same rule the editorial path needed: agreement is not usability.
        //
        // Note an evenly-scattered sample will NOT trip this — $400 to $9,600 in even steps
        // still has a 2.57x interquartile range, because the IQR is robust by construction.
        // Reaching a 4x middle half takes a genuinely two-population market, which is the
        // real case worth catching: standard-colour puppies at one price and rare-colour or
        // show prospects at another, listed side by side under one breed. There is no single
        // "typical" price there, and inventing one would mislabel both halves.
        var bimodal = Sample([
            .. Enumerable.Range(0, 12).Select(i => 500 + i * 25),
            .. Enumerable.Range(0, 12).Select(i => 5000 + i * 100)]);

        var result = ListingPriceAggregator.Aggregate("french-bulldog", bimodal, Strict);

        Assert.Equal(PriceConfidence.Contested, result.Price!.Confidence);
        Assert.Contains("middle half spans", result.Rationale);
    }

    // ---------------------------------------------------------------- percentiles

    [Fact]
    public void PercentilesAreRealPricesNotInterpolatedOnes()
    {
        int[] sorted = [1000, 2000, 3000, 4000, 5000];

        // Every returned value must be a price somebody is actually asking.
        foreach (var p in new[] { 0, 25, 50, 75, 100 })
        {
            Assert.Contains(ListingPriceAggregator.Percentile(sorted, p), sorted);
        }

        Assert.Equal(3000, ListingPriceAggregator.Percentile(sorted, 50));
        Assert.Equal(1000, ListingPriceAggregator.Percentile(sorted, 0));
        Assert.Equal(5000, ListingPriceAggregator.Percentile(sorted, 100));
    }

    [Fact]
    public void PercentileOfAnEmptySampleIsZeroRatherThanThrowing()
    {
        Assert.Equal(0, ListingPriceAggregator.Percentile([], 50));
    }

    // ---------------------------------------------------------------- crossbreeds

    [Theory]
    // Breed searches return mixes in quantity — 6 of 10 Bernese results, 5 of 10 Corgi.
    // A mix is usually cheaper, so counting them drags a purebred range down: the same
    // failure as counting scam listings, reached by a different route.
    [InlineData("French Bulldog - F", "French Bulldog", true)]
    [InlineData("French Bulldog - M", "French Bulldog", true)]
    [InlineData("French Bulldog", "French Bulldog", true)]
    [InlineData("Boston Terrier and French Bulldog - F", "French Bulldog", false)]
    [InlineData("French Bulldog and Pug - M", "French Bulldog", false)]
    [InlineData("Frenchton - F", "French Bulldog", false)]
    [InlineData("", "French Bulldog", false)]
    public void OnlyPurebredTitlesCount(string listingName, string breed, bool expected) =>
        Assert.Equal(expected, ListingSources.IsPurebredTitle(listingName, breed));

    [Fact]
    public void SlugOverridesAreUsedWhereTheVendorDisagreesWithUs()
    {
        // Verified by fetching: a wrong slug 404s, but a *plausible* wrong slug would
        // quietly return another breed's prices.
        Assert.Equal("german-shepherd-dog", ListingSources.VendorSlug("german-shepherd"));
        Assert.Equal("english-bulldog", ListingSources.VendorSlug("bulldog"));
        Assert.Equal("beagle", ListingSources.VendorSlug("beagle"));
        // dog.ceo's naming needs reshaping rather than translating.
        Assert.Equal("old-english-sheepdog", ListingSources.VendorSlug("english-sheepdog"));
        Assert.Equal("australian-shepherd", ListingSources.VendorSlug("shepherd-australian"));
    }

    [Fact]
    public void ThePoodleSizesAreDistinctBreedsWithDistinctVendorNames()
    {
        // Their titles are "Poodle - <Size>", where " - " is the size separator and not the
        // sex marker. Getting this wrong collapsed all three sizes into one name.
        Assert.Equal("Poodle - Standard", ListingSources.VendorName("standard-poodle", "Standard Poodle"));
        Assert.Equal("Poodle - Miniature", ListingSources.VendorName("miniature-poodle", "Miniature Poodle"));
        Assert.Equal("Poodle - Toy", ListingSources.VendorName("toy-poodle", "Toy Poodle"));

        Assert.True(ListingSources.IsPurebredTitle("Poodle - Standard - F", "Poodle - Standard"));
        Assert.False(ListingSources.IsPurebredTitle("Poodle - Miniature - F", "Poodle - Standard"));
        Assert.False(ListingSources.IsPurebredTitle("Bernedoodle and Poodle - Standard", "Poodle - Standard"));
    }

    [Fact]
    public void TheExpectedNameIsThePurebredTitleNotTheCommonestOne()
    {
        // cardigan-corgi's single most common listing title is "Cardigan Welsh Corgi and
        // Pembroke Welsh Corgi" — a mix. Taking the commonest title outright would have
        // inverted the filter into accepting only crossbreeds.
        var expected = ListingSources.VendorName("cardigan-corgi", "Cardigan Corgi");

        Assert.Equal("Cardigan Welsh Corgi", expected);
        Assert.True(ListingSources.IsPurebredTitle("Cardigan Welsh Corgi - M", expected));
        Assert.False(ListingSources.IsPurebredTitle("Cardigan Welsh Corgi and Pembroke Welsh Corgi", expected));
    }

    [Fact]
    public void DuplicateCatalogEntriesAreNotSeparatePricedBreeds()
    {
        // Two catalog entries for one animal produced two different prices for it, and the
        // duplicate bypassed the floor guard entirely: only the curated entry has a seed
        // range to check against, so australian-shepherd was correctly refused at a $500
        // floor while shepherd-australian published exactly that, unguarded.
        Assert.Contains("shepherd-australian", PuppyFinder.Api.Data.SiteCatalog.DuplicateOfCurated);
        Assert.Contains("english-bulldog", PuppyFinder.Api.Data.SiteCatalog.DuplicateOfCurated);
        Assert.Contains("standard-poodle", PuppyFinder.Api.Data.SiteCatalog.DuplicateOfCurated);
        Assert.Contains("pembroke", PuppyFinder.Api.Data.SiteCatalog.DuplicateOfCurated);
        Assert.Contains("shepherd-german", PuppyFinder.Api.Data.SiteCatalog.DuplicateOfCurated);

        // The other poodle sizes are genuinely distinct breeds we don't otherwise carry.
        Assert.DoesNotContain("miniature-poodle", PuppyFinder.Api.Data.SiteCatalog.DuplicateOfCurated);
        Assert.DoesNotContain("toy-poodle", PuppyFinder.Api.Data.SiteCatalog.DuplicateOfCurated);
        Assert.DoesNotContain("cardigan-corgi", PuppyFinder.Api.Data.SiteCatalog.DuplicateOfCurated);
    }


    [Fact]
    public void VendorCoverageIsAMeasuredListNotAnAspiration()
    {
        // 54 of the 154 unpriced breeds were probed as reachable with real inventory. The
        // rest resolve to almost nothing or aren't breeds; retrying them every run would be
        // ~100 pointless requests.
        Assert.True(ListingSources.IsKnownToVendor("akita"));
        Assert.True(ListingSources.IsKnownToVendor("boston-terrier"));
        Assert.True(ListingSources.IsKnownToVendor("mexican-hairless")); // Xoloitzcuintli

        // Measured as having too little inventory to compute a band from.
        Assert.False(ListingSources.IsKnownToVendor("affenpinscher"));
        Assert.False(ListingSources.IsKnownToVendor("kerryblue-terrier"));
        // Not a dog breed at all — dog.ceo's list is not a breed list.
        Assert.False(ListingSources.IsKnownToVendor("dhole"));
        Assert.False(ListingSources.IsKnownToVendor("blenheim-spaniel"));
    }
}
