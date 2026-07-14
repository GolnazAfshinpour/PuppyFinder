using PuppyFinder.Api.Data;

namespace PuppyFinder.Api.Tests;

/// <summary>
/// BuildLink encodes externally-verified URL patterns (see docs/SOURCES.md).
/// These tests pin every pattern so a refactor can't silently change where
/// users land — a wrong link doesn't crash, it just quietly loses the filter.
/// </summary>
public class SiteCatalogLinkTests
{
    private static Site GetSite(string id) => SiteCatalog.Sites.Single(s => s.Id == id);
    private static Breed GetBreed(string slug) => SiteCatalog.Breeds.Single(b => b.Slug == slug);

    private static string Link(string siteId, string? breedSlug = null, string? state = null, string? city = null) =>
        SiteCatalog.BuildLink(GetSite(siteId), breedSlug is null ? null : GetBreed(breedSlug), state, city);

    // --- AKC Marketplace ---

    [Theory]
    [InlineData(null, null, "https://marketplace.akc.org/puppies/golden-retriever")]
    [InlineData("TX", null, "https://marketplace.akc.org/puppies/golden-retriever/texas")]
    [InlineData("TX", "Houston", "https://marketplace.akc.org/puppies/golden-retriever/texas/houston")]
    public void Akc_UsesBreedStateCityPath(string? state, string? city, string expected) =>
        Assert.Equal(expected, Link("akc", "golden-retriever", state, city));

    [Fact]
    public void Akc_UsesAkcSpecificBreedSlug() =>
        // AKC's slug differs from ours for some breeds (german-shepherd-dog).
        Assert.Equal("https://marketplace.akc.org/puppies/german-shepherd-dog", Link("akc", "german-shepherd"));

    // --- Good Dog ---

    [Fact]
    public void GoodDog_BreedOnly() =>
        Assert.Equal("https://www.gooddog.com/golden-retriever", Link("gooddog", "golden-retriever"));

    [Fact]
    public void GoodDog_StateUsesLowercaseAbbreviation() =>
        Assert.Equal("https://www.gooddog.com/golden-retriever/tx", Link("gooddog", "golden-retriever", "TX"));

    [Fact]
    public void GoodDog_CityUsesCityDashStateSlug() =>
        Assert.Equal("https://www.gooddog.com/golden-retriever/houston-tx", Link("gooddog", "golden-retriever", "TX", "Houston"));

    [Fact]
    public void GoodDog_MultiWordCityIsHyphenated() =>
        Assert.Equal("https://www.gooddog.com/golden-retriever/san-antonio-tx", Link("gooddog", "golden-retriever", "TX", "San Antonio"));

    [Fact]
    public void GoodDog_TeacupPoodle_UsesToySizePage() =>
        Assert.Equal("https://www.gooddog.com/poodle/size/toy", Link("gooddog", "teacup-poodle"));

    [Fact]
    public void GoodDog_TeacupPoodleWithState_LocationWins() =>
        // Good Dog's size and location pages can't combine (404), so state wins.
        Assert.Equal("https://www.gooddog.com/poodle/tx", Link("gooddog", "teacup-poodle", "TX"));

    // --- PuppySpot ---

    [Fact]
    public void PuppySpot_BreedOnly_UsesByBreedersPage() =>
        Assert.Equal("https://www.puppyspot.com/puppies-for-sale-by-breeders/breed/golden-retriever", Link("puppyspot", "golden-retriever"));

    [Fact]
    public void PuppySpot_BreedAndState_UsesFindPuppiesPage() =>
        Assert.Equal("https://www.puppyspot.com/find-puppies/golden-retriever/texas", Link("puppyspot", "golden-retriever", "TX"));

    [Fact]
    public void PuppySpot_StateOnly() =>
        Assert.Equal("https://www.puppyspot.com/find-puppies/new-york", Link("puppyspot", state: "NY"));

    // --- Petfinder (Dec-2025 rebuild dropped URL-driven search) ---

    [Fact]
    public void Petfinder_Breed_LandsOnBreedPage() =>
        Assert.Equal("https://www.petfinder.com/dogs-and-puppies/breeds/golden-retriever/", Link("petfinder", "golden-retriever", "TX"));

    [Fact]
    public void Petfinder_NoBreed_LandsOnSearch() =>
        Assert.Equal("https://www.petfinder.com/search/dogs-for-adoption/us/", Link("petfinder"));

    // --- Adopt-a-Pet ---

    [Fact]
    public void AdoptAPet_BreedStateCity() =>
        Assert.Equal("https://www.adoptapet.com/s/adopt-a-golden-retriever/texas/houston", Link("adoptapet", "golden-retriever", "TX", "Houston"));

    // --- Puppies.com ---

    [Fact]
    public void PuppiesCom_BreedStateCity() =>
        Assert.Equal("https://www.puppies.com/find-a-puppy/golden-retriever/texas/houston", Link("puppies", "golden-retriever", "TX", "Houston"));

    [Fact]
    public void PuppiesCom_TeacupBreed_UsesParentBreedSlug() =>
        Assert.Equal("https://www.puppies.com/find-a-puppy/yorkshire-terrier", Link("puppies", "teacup-yorkie"));

    // --- Lancaster Puppies ---

    [Fact]
    public void Lancaster_BreedOnly() =>
        Assert.Equal("https://www.lancasterpuppies.com/sale/puppies/golden-retriever/", Link("lancaster", "golden-retriever"));

    [Fact]
    public void Lancaster_BreedAndState() =>
        Assert.Equal("https://www.lancasterpuppies.com/sale/puppies/golden-retriever/united-states/texas/", Link("lancaster", "golden-retriever", "TX"));

    [Fact]
    public void Lancaster_StateOnly_UsesNearMePage() =>
        Assert.Equal("https://www.lancasterpuppies.com/sale/puppies/near-me/united-states/new-york/", Link("lancaster", state: "NY"));

    // --- Greenfield Puppies ---

    [Fact]
    public void Greenfield_BreedOnly_StatePathDoesNotExist() =>
        Assert.Equal("https://www.greenfieldpuppies.com/golden-retriever-puppies-for-sale/", Link("greenfield", "golden-retriever", "TX"));

    // --- Pawrade ---

    [Fact]
    public void Pawrade_BreedOnly() =>
        Assert.Equal("https://www.pawrade.com/puppies/golden-retriever/", Link("pawrade", "golden-retriever"));

    [Fact]
    public void Pawrade_StateSlugsConcatenateWords() =>
        // Verified via Pawrade's sitemap: "newyork", never "new-york".
        Assert.Equal("https://www.pawrade.com/puppies-for-sale/newyork/golden-retriever/", Link("pawrade", "golden-retriever", "NY"));

    [Fact]
    public void Pawrade_StateOnly() =>
        Assert.Equal("https://www.pawrade.com/puppies-for-sale/texas/", Link("pawrade", state: "TX"));

    // --- Rescue Me! ---

    [Fact]
    public void RescueMe_UsesNicknameSubdomainAndConcatenatedState() =>
        Assert.Equal("https://lab.rescueme.org/newyork", Link("rescueme", "labrador-retriever", "NY"));

    [Fact]
    public void RescueMe_UnknownBreedWithState_FallsBackToDogSubdomain()
    {
        var externalBreed = new Breed("some-new-breed", "Some New Breed", "some-new-breed",
            "Medium", 3, 3, 3, 3, 3, 0, 0, "");
        Assert.Equal("https://dog.rescueme.org/texas",
            SiteCatalog.BuildLink(GetSite("rescueme"), externalBreed, "TX"));
    }

    // --- Cross-cutting rules ---

    [Fact]
    public void CityWithoutState_IsIgnoredEverywhere()
    {
        foreach (var site in SiteCatalog.Sites)
        {
            var withCity = SiteCatalog.BuildLink(site, GetBreed("golden-retriever"), state: null, city: "Houston");
            var without = SiteCatalog.BuildLink(site, GetBreed("golden-retriever"), state: null, city: null);
            Assert.Equal(without, withCity);
        }
    }

    [Theory]
    [InlineData("aspca")]
    [InlineData("bestfriends")]
    [InlineData("akcrescue")]
    public void SitesWithoutDeepLinks_AlwaysUseHomeUrl(string siteId) =>
        Assert.Equal(GetSite(siteId).HomeUrl, Link(siteId, "golden-retriever", "TX", "Houston"));

    [Fact]
    public void NoFilters_NeverBreaks_AndAlwaysAbsoluteHttps()
    {
        foreach (var site in SiteCatalog.Sites)
        {
            var url = SiteCatalog.BuildLink(site, null, null);
            Assert.StartsWith("https://", url);
            Assert.True(Uri.IsWellFormedUriString(url, UriKind.Absolute), $"{site.Id}: {url}");
        }
    }

    [Fact]
    public void AllBreedAndStateCombos_ProduceWellFormedUrls()
    {
        string?[] states = [null, "TX", "NY", "WV"];
        foreach (var site in SiteCatalog.Sites)
            foreach (var breed in SiteCatalog.Breeds)
                foreach (var state in states)
                {
                    var url = SiteCatalog.BuildLink(site, breed, state, "San Antonio");
                    Assert.True(Uri.IsWellFormedUriString(url, UriKind.Absolute), $"{site.Id}/{breed.Slug}/{state}: {url}");
                    Assert.DoesNotContain(" ", url);
                }
    }

    [Fact]
    public void Sites_AreOrderedBuyFirstThenAdopt()
    {
        var kinds = SiteCatalog.Sites.Select(s => s.Kind).ToList();
        var firstAdopt = kinds.IndexOf("Adopt");
        Assert.DoesNotContain("Buy from breeders", kinds.Skip(firstAdopt));
    }
}
