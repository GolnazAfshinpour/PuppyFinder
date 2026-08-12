using PuppyFinder.Api.Services;

namespace PuppyFinder.Api.Tests;

/// <summary>
/// The outbound link is the whole point of an adoption card — it is how someone actually reaches
/// the dog. 207 of 297 live records pointed at rescuegroups.org's homepage because the animal's
/// own url is usually absent and the provider fell back to the site root, so "Meet Ace" opened a
/// corporate homepage with no way to find Ace.
///
/// There is no canonical per-animal URL to substitute: /animals/detail?AnimalID= at the site root
/// 404s, the animal's slug 404s there too, and the host in trackerimageUrl does not resolve. So
/// the link degrades through the rescue's own site instead, and these tests pin each step.
/// </summary>
public class RescueGroupsProviderTests
{
    [Fact]
    public void PrefersTheAnimalsOwnPageWhenTheApiGivesOne()
    {
        var url = RescueGroupsProvider.DetailUrl(
            "https://ehrdogs.rescuegroups.org/animals/detail?AnimalID=123",
            "https://example.org",
            "123");

        Assert.Equal("https://ehrdogs.rescuegroups.org/animals/detail?AnimalID=123", url);
    }

    [Fact]
    public void BuildsTheDetailPathWhenTheRescueIsHostedByRescueGroups()
    {
        // Same shape as the links the API does populate, so it still reaches the individual dog.
        var url = RescueGroupsProvider.DetailUrl(null, "http://underdogrescuemn.rescuegroups.org", "15102069");

        Assert.Equal(
            "https://underdogrescuemn.rescuegroups.org/animals/detail?AnimalID=15102069", url);
    }

    [Fact]
    public void FallsBackToTheRescuesOwnSite()
    {
        // Not the dog's page, but the organisation that has the dog — and the card names the dog.
        // The alternative was a corporate homepage, which tells the reader nothing.
        var url = RescueGroupsProvider.DetailUrl(null, "http://www.orangeburgspca.org", "13982862");

        Assert.Equal("https://www.orangeburgspca.org/", url);
    }

    [Fact]
    public void UpgradesHttpToHttps()
    {
        Assert.StartsWith("https://", RescueGroupsProvider.DetailUrl(
            "http://reachoutrescue.org/animals/detail?AnimalID=1", null, "1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    public void OnlyReachesForTheHomepageWhenThereIsGenuinelyNothingElse(string? orgUrl)
    {
        // 27 of 297 records have neither, and this is the honest answer for them rather than a
        // default nobody examined.
        Assert.Equal("https://rescuegroups.org", RescueGroupsProvider.DetailUrl(null, orgUrl, "1"));
    }
}
