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
}
