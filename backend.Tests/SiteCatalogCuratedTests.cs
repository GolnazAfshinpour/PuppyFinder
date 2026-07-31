using PuppyFinder.Api.Data;

namespace PuppyFinder.Api.Tests;

public class SiteCatalogCuratedTests
{
    [Fact]
    public void IsCuratedDoesNotThrowDuringTypeInitialization()
    {
        // Regression: CuratedSlugs is declared above Breeds, so an eager static
        // initializer would read Breeds as null and throw a TypeInitializationException
        // on the very first call. It compiles either way — only running catches it.
        Assert.True(SiteCatalog.IsCurated("beagle"));
    }

    [Fact]
    public void EveryCuratedBreedIsRecognised()
    {
        Assert.All(SiteCatalog.Breeds, b => Assert.True(SiteCatalog.IsCurated(b.Slug)));
    }

    [Theory]
    [InlineData("affenpinscher")] // dog.ceo catalog entry, no trait data
    [InlineData("not-a-breed")]
    public void ExternalAndUnknownBreedsAreNotCurated(string slug) =>
        Assert.False(SiteCatalog.IsCurated(slug));

    [Fact]
    public void CuratedLookupIsCaseInsensitive() =>
        Assert.True(SiteCatalog.IsCurated("BEAGLE"));

    [Fact]
    public void CuratednessIsIndependentOfHavingAPrice()
    {
        // The decoupling this exists for: prices now live in the DB and a curated
        // breed can legitimately have no price row, but it must keep its trait data.
        var priceless = SiteCatalog.Breeds.First() with { PriceLow = 0, PriceHigh = 0 };

        Assert.True(SiteCatalog.IsCurated(priceless.Slug));
    }
}
