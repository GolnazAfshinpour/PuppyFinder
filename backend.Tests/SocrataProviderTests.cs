using PuppyFinder.Api.Services;

namespace PuppyFinder.Api.Tests;

public class SocrataProviderTests
{
    [Theory]
    // Montgomery style: lowercase params, www host, http.
    [InlineData("http://www.petharbor.com/get_image.asp?res=DETAIL&id=A538754&location=MONT",
        "https://petharbor.com/pet.asp?uaid=MONT.A538754")]
    // King County style: uppercase params, bare host, https.
    [InlineData("https://petharbor.com/get_image.asp?RES=Detail&LOCATION=KING&ID=A749993",
        "https://petharbor.com/pet.asp?uaid=KING.A749993")]
    public void PetHarborDetailUrl_DerivesDetailPageFromImageUrl(string imageUrl, string expected) =>
        Assert.Equal(expected, SocrataProvider.PetHarborDetailUrl(imageUrl));

    [Theory]
    [InlineData(null)]
    [InlineData("not a url")]
    [InlineData("https://example.com/get_image.asp?id=A1&location=MONT")] // wrong host
    [InlineData("https://evilpetharbor.com/get_image.asp?id=A1&location=MONT")] // host suffix spoof
    [InlineData("https://petharbor.com/get_image.asp?id=A538754")] // no location
    [InlineData("https://petharbor.com/get_image.asp?location=MONT")] // no id
    public void PetHarborDetailUrl_ReturnsNullWhenNotDerivable(string? imageUrl) =>
        Assert.Null(SocrataProvider.PetHarborDetailUrl(imageUrl));

    [Theory]
    [InlineData("http://www.petharbor.com/get_image.asp?res=DETAIL&id=A538754&location=MONT", "A538754")]
    [InlineData("https://petharbor.com/get_image.asp?RES=Detail&LOCATION=KING&ID=a749993", "A749993")]
    [InlineData("https://example.com/get_image.asp?id=A1&location=MONT", null)]
    [InlineData(null, null)]
    public void PetHarborAnimalId_ExtractsShelterReference(string? imageUrl, string? expected) =>
        Assert.Equal(expected, SocrataProvider.PetHarborAnimalId(imageUrl));

    [Theory]
    [InlineData("*BELLARINA", "BELLARINA")] // shelter bookkeeping marker
    [InlineData("** Rex", "Rex")]
    [InlineData("  Sashi ", "Sashi")]
    [InlineData("Dior", "Dior")]
    [InlineData("*#42*", "*#42*")] // nothing lettered to salvage — keep as-is
    public void CleanName_StripsLeadingMarkers(string raw, string expected) =>
        Assert.Equal(expected, SocrataProvider.CleanName(raw));

    [Theory]
    [InlineData("Labrador Retr", "Labrador Retriever")]
    [InlineData("Germ Shepherd", "German Shepherd")]
    [InlineData("Beagle / Labrador Retr", "Beagle / Labrador Retriever")]
    [InlineData("Bull Terr Mix", "Bull Terrier Mix")]
    [InlineData("Am Pit Bull Ter", "American Pit Bull Terrier")]
    [InlineData("Siberian Husky", "Siberian Husky")] // untouched when nothing abbreviated
    [InlineData(null, null)]
    public void ExpandBreedAbbreviations_FixesPetHarborTruncations(string? raw, string? expected) =>
        Assert.Equal(expected, SocrataProvider.ExpandBreedAbbreviations(raw));

    [Theory]
    [InlineData("SMALL", "Small")]
    [InlineData("MED", "Medium")]
    [InlineData("LARGE", "Large")]
    [InlineData("med ", "Medium")]
    [InlineData("KITTE", null)] // cats-only value never maps to a dog size
    [InlineData(null, null)]
    public void NormalizeSize_MapsFeedValuesToAppBuckets(string? raw, string? expected) =>
        Assert.Equal(expected, SocrataProvider.NormalizeSize(raw));

    [Theory]
    [InlineData("Bentley is a friendly boy at 92.0 lbs who loves walks.", "Large")]
    [InlineData("She weighs about 45.5 lbs.", "Medium")]
    [InlineData("A tiny 12 lb lapdog.", "Small")]
    [InlineData("Weighs 30 pounds soaking wet.", "Medium")]
    [InlineData("A very good dog with no weight mentioned.", null)]
    [InlineData(null, null)]
    public void SizeFromWeightText_DerivesBucketFromBioWeight(string? memo, string? expected) =>
        Assert.Equal(expected, SocrataProvider.SizeFromWeightText(memo));

    [Fact]
    public void CleanMemo_KeepsOnlyTheBioAfterMetadataBlocks()
    {
        const string memo = "Received on: 2026-06-22</p> Description: Tan Neutered Male German Shepherd / Mix Dog</p> "
            + "Age: 2 YEARS</p> Adoption Fee: $50</p> Current Location: In RASKC Foster Home </p>"
            + "Hi there, my name is Milo and I'm ready for adoption!</p></p>I weigh 60 lbs.";
        Assert.Equal("Hi there, my name is Milo and I'm ready for adoption! I weigh 60 lbs.",
            SocrataProvider.CleanMemo(memo));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    [InlineData("Just a plain bio with no markup.", "Just a plain bio with no markup.")]
    [InlineData("<b>Bold</b> bio  text", "Bold bio text")]
    public void CleanMemo_HandlesPlainAndMarkupText(string? memo, string expected) =>
        Assert.Equal(expected, SocrataProvider.CleanMemo(memo));

    [Fact]
    public void BuildId_KeysOnTheShelterAnimalRef_NotThePositionInTheFeed()
    {
        // The regression this replaces: the id embedded the row index, so adopting
        // one dog re-numbered every dog after it — breaking saved favorites and
        // recently-viewed, which are keyed on the id in localStorage.
        var before = SocrataProvider.BuildId("Montgomery County Animal Services", "A542024", "Count Chocula");
        var afterOthersLeftTheFeed = SocrataProvider.BuildId("Montgomery County Animal Services", "A542024", "Count Chocula");

        Assert.Equal(before, afterOthersLeftTheFeed);
        Assert.Equal("montgomery-county-animal-services-a542024", before);
    }

    [Fact]
    public void BuildId_FallsBackToTheNameWhenTheFeedHasNoAnimalRef()
    {
        var id = SocrataProvider.BuildId("King County Pet Adoption", animalRef: null, "Count Chocula");

        Assert.Equal("king-county-pet-adoption-count-chocula", id);
    }

    [Fact]
    public void BuildId_DisambiguatesCollisions()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var first = SocrataProvider.BuildId("Shelter", null, "Bella", used);
        var second = SocrataProvider.BuildId("Shelter", null, "Bella", used);
        var third = SocrataProvider.BuildId("Shelter", null, "Bella", used);

        Assert.Equal(["shelter-bella", "shelter-bella-2", "shelter-bella-3"], new[] { first, second, third });
    }

    [Theory]
    // Shelter names carry punctuation and bookkeeping markers; ids stay URL-safe.
    [InlineData("Shelter", "*BELLARINA", "shelter-bellarina")]
    [InlineData("Shelter", "Mary-Jane   Smith", "shelter-mary-jane-smith")]
    [InlineData("Shelter", "!!!", "shelter-unknown")]
    public void BuildId_ProducesUrlSafeIds(string source, string name, string expected) =>
        Assert.Equal(expected, SocrataProvider.BuildId(source, null, name));
}
