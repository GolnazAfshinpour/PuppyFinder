using PuppyFinder.Api.Services;

namespace PuppyFinder.Api.Tests;

/// <summary>
/// The Keystone source's pure parsing pieces. The fixtures mirror what the live site
/// actually serves (probed August 2026): a single grid page whose detail URLs carry the
/// breed slug, and a Product/Offer ld+json block per detail page whose JSON contains raw
/// newlines inside strings — technically invalid, and the reason the parser repairs
/// before it rejects.
/// </summary>
public class KeystoneListingSourceTests
{
    // ---- grid links ----

    [Fact]
    public void ExtractsAndDedupesDetailLinks()
    {
        const string html = """
            <a href="https://www.keystonepuppies.com/puppy/frenchton-puppies-for-sale/becca-9">Becca</a>
            <a href="https://www.keystonepuppies.com/puppy/frenchton-puppies-for-sale/becca-9">Becca again</a>
            <a href='https://www.keystonepuppies.com/puppy/french-bulldog-puppies-for-sale/max-2'>Max</a>
            <a href="https://www.keystonepuppies.com/puppy-breeds/cavapoo-puppies-for-sale">breed page, not a puppy</a>
            """;

        var links = KeystoneListingSource.ExtractDetailLinks(html);

        Assert.Equal(2, links.Count);
        Assert.Equal("https://www.keystonepuppies.com/puppy/frenchton-puppies-for-sale/becca-9", links[0]);
        Assert.Equal("https://www.keystonepuppies.com/puppy/french-bulldog-puppies-for-sale/max-2", links[1]);
    }

    // ---- slug bucketing ----

    [Theory]
    [InlineData("https://www.keystonepuppies.com/puppy/frenchton-puppies-for-sale/becca-9", "frenchton")]
    [InlineData("https://www.keystonepuppies.com/puppy/french-bulldog-puppies-for-sale/max-2", "french-bulldog")]
    [InlineData("https://www.keystonepuppies.com/puppy/poodle-mini-puppies-for-sale/coco", "poodle-mini")]
    [InlineData("https://www.keystonepuppies.com/puppy/weird-shape", "")]
    public void BucketsDetailUrlsByTheirBreedSlug(string url, string expected) =>
        Assert.Equal(expected, KeystoneListingSource.SlugOfDetailUrl(url));

    [Fact]
    public void MapsOurSlugsToTheirsOnlyWhereTheyDiffer()
    {
        Assert.Equal("poodle-mini", KeystoneListingSource.KeystoneSlug("miniature-poodle"));
        // Identity is the default: an unmapped breed asks under its own name and fails safe
        // as "no listings" rather than as another breed's prices.
        Assert.Equal("french-bulldog", KeystoneListingSource.KeystoneSlug("french-bulldog"));
    }

    // ---- detail parsing ----

    [Fact]
    public void ReadsPriceBrandAndNameFromAValidProductGraph()
    {
        const string html = """
            <script type="application/ld+json">
            {"@context":"https://schema.org","@graph":[
              {"@type":"Organization","name":"Keystone Puppies"},
              {"@type":"Product","name":"Becca",
               "brand":{"@type":"Brand","name":"Frenchton"},
               "offers":{"@type":"Offer","price":1200.00,"priceCurrency":"USD"}}
            ]}
            </script>
            """;

        var detail = KeystoneListingSource.ParseDetail(html);

        Assert.NotNull(detail);
        Assert.Equal("Becca", detail.Name);
        Assert.Equal("Frenchton", detail.Brand);
        Assert.Equal(1200, detail.Price);
        Assert.Equal("USD", detail.Currency);
    }

    [Fact]
    public void RepairsTheRawNewlinesTheirJsonActuallyContains()
    {
        // Observed live: the description string carries literal newlines and markup, which
        // is invalid JSON. A strict parse loses the price over a formatting quirk in the
        // field beside it.
        const string html = "<script type=\"application/ld+json\">\n"
            + "{\"@type\":\"Product\",\"name\":\"Boss\",\n"
            + "\"description\":\"A playful boy.\n<ul>\n\t<li>Mom is Pictured</li>\n</ul>\",\n"
            + "\"brand\":{\"@type\":\"Brand\",\"name\":\"French Bulldog\"},\n"
            + "\"offers\":{\"@type\":\"Offer\",\"price\":2500,\"priceCurrency\":\"USD\"}}\n"
            + "</script>";

        var detail = KeystoneListingSource.ParseDetail(html);

        Assert.NotNull(detail);
        Assert.Equal("French Bulldog", detail.Brand);
        Assert.Equal(2500, detail.Price);
    }

    [Fact]
    public void ToleratesAStringPrice() =>
        Assert.Equal(1250, KeystoneListingSource.ParseDetail("""
            <script type="application/ld+json">
            {"@type":"Product","name":"Coco","offers":{"price":"1,250.00","priceCurrency":"USD"}}
            </script>
            """)!.Price);

    [Fact]
    public void ReturnsNullWhenThePageHasNoProduct() =>
        Assert.Null(KeystoneListingSource.ParseDetail("""
            <script type="application/ld+json">{"@type":"Article","headline":"Puppy care"}</script>
            """));

    [Fact]
    public void MapsBrandNamesOnlyWhereTheyDiffer()
    {
        // Read off live Product blocks after the first full run silently dropped every
        // Yorkie and Mini Poodle as a "crossbreed".
        Assert.Equal("Yorkie", KeystoneListingSource.ExpectedBrand("yorkshire-terrier", "Yorkshire Terrier"));
        Assert.Equal("Mini Poodles", KeystoneListingSource.ExpectedBrand("miniature-poodle", "Miniature Poodle"));
        Assert.Equal("English Bulldog", KeystoneListingSource.ExpectedBrand("bulldog", "Bulldog"));
        // The default is our own display name.
        Assert.Equal("French Bulldog", KeystoneListingSource.ExpectedBrand("french-bulldog", "French Bulldog"));
    }

    [Fact]
    public void TheBrandGateReusesTheExistingCrossbreedRule()
    {
        // "Frenchton" is a Frenchie cross; the exact-match rule that filters puppies.com
        // titles rejects it against "French Bulldog" without a separate mix list.
        Assert.False(PuppyFinder.Api.Data.ListingSources.IsPurebredTitle("Frenchton", "French Bulldog"));
        Assert.True(PuppyFinder.Api.Data.ListingSources.IsPurebredTitle("French Bulldog", "French Bulldog"));
    }
}
