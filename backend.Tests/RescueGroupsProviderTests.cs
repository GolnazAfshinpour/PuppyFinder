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

    // ---- adoption fee ----

    [Theory]
    // Rescues type this by hand; all of these came off one live page.
    [InlineData("$175.00", "$175")]
    [InlineData("175.00", "$175")]
    [InlineData("375", "$375")]
    [InlineData("795", "$795")]
    [InlineData("1250", "$1,250")]
    [InlineData(" $500 ", "$500")]
    public void FormatsABareAmountConsistently(string raw, string expected) =>
        Assert.Equal(expected, RescueGroupsProvider.NormalizeFee(raw));

    [Theory]
    // Not a number, and not ours to turn into one — these are all real answers.
    [InlineData("$300-$450")]
    [InlineData("Varies")]
    [InlineData("Waived for seniors")]
    [InlineData("Call for details")]
    public void PassesThroughAnythingThatIsNotABareAmount(string raw) =>
        Assert.Equal(raw, RescueGroupsProvider.NormalizeFee(raw));

    [Fact]
    public void KeepsCentsOnlyWhenTheRescueSpecifiedThem() =>
        Assert.Equal("$175.50", RescueGroupsProvider.NormalizeFee("175.50"));

    [Theory]
    // The hand-typed way of leaving the field blank — "n/a" appeared three times in live data.
    [InlineData("n/a")]
    [InlineData("N/A")]
    [InlineData("none")]
    [InlineData("TBD")]
    [InlineData("-")]
    public void TreatsAHandTypedBlankAsUnstated(string raw) =>
        Assert.Null(RescueGroupsProvider.NormalizeFee(raw));

    [Fact]
    public void KeepsARescuesOwnWordsWhenTheyActuallySaySomething() =>
        // Not a number and not a placeholder — it is the rescue answering the question.
        Assert.Equal(
            "No Fee-readopted by original owner",
            RescueGroupsProvider.NormalizeFee("No Fee-readopted by original owner"));

    [Theory]
    [InlineData("0")]
    [InlineData("$0.00")]
    [InlineData("")]
    [InlineData(null)]
    public void TreatsAnAbsentOrZeroFeeAsUnstated(string? raw) =>
        // "Adoption fee $0" is a claim on the rescue's behalf. A rescue that means free writes
        // "Waived"; an unedited numeric field defaulting to zero is the likelier explanation, and
        // null sends the reader to the "ask what it covers" prompt, which is true either way.
        Assert.Null(RescueGroupsProvider.NormalizeFee(raw));
}
