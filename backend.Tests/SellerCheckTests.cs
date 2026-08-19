using PuppyFinder.Api.Services;

namespace PuppyFinder.Api.Tests;

public class SellerCheckTests
{
    private static SellerVerdict Check(SellerDelivery delivery, SellerLicence licence) =>
        SellerCheck.Evaluate(delivery, licence);

    // ---- the rule this whole check turns on ----

    [Fact]
    public void ShippingSightUnseenAndRefusingALicenceIsTheOnlyWarning()
    {
        // Both answers are trivially easy to give: a licensed breeder reads the number off the
        // certificate, an exempt one says "I keep three breeding females". There is no innocent
        // silence, which is what makes this the one branch that warns.
        var verdict = Check(SellerDelivery.SightUnseen, SellerLicence.Refused);

        Assert.Equal("Required", verdict.Level);
        Assert.True(verdict.IsWarning);
    }

    [Theory]
    [InlineData(SellerLicence.Given)]
    [InlineData(SellerLicence.ClaimedExempt)]
    [InlineData(SellerLicence.Unspecified)]
    public void NothingElseIsPresentedAsAWarning(SellerLicence licence) =>
        // An unlicensed seller is not thereby a scammer — the small-breeder exemption is real and
        // most good hobby breeders sit inside it. Crying wolf here would be the costlier error.
        Assert.False(Check(SellerDelivery.SightUnseen, licence).IsWarning);

    [Fact]
    public void AnExemptionClaimIsNarrowedToTheOnlyOneThatCanApply()
    {
        // The line that does the work: a puppy shipped to you is not a face-to-face sale, so the
        // retail exemption cannot be what they are relying on.
        var verdict = Check(SellerDelivery.SightUnseen, SellerLicence.ClaimedExempt);

        Assert.Equal("Unverifiable", verdict.Level);
        Assert.Contains("not a face-to-face sale", verdict.Detail);
        Assert.Contains(verdict.Actions!, a => a.Text.Contains("four or fewer breeding females"));
    }

    [Fact]
    public void AnExemptionClaimIsTestedAgainstTheirOwnAdvertising()
    {
        // "Four or fewer breeding females" and "several breeds, always available" cannot both be
        // true, and the buyer can check the second without asking anyone.
        var verdict = Check(SellerDelivery.SightUnseen, SellerLicence.ClaimedExempt);

        Assert.Contains(verdict.Actions!, a => a.Text.Contains("several breeds"));
    }

    // ---- the other direction, which matters just as much ----

    [Fact]
    public void SeeingThePuppyInPersonTakesLicensingOffTheTable()
    {
        // The honest answer is "this check does not apply". Manufacturing a warning here would
        // point a buyer away from exactly the breeders the app wants them to use.
        var verdict = Check(SellerDelivery.InPerson, SellerLicence.Refused);

        Assert.Equal("Exempt", verdict.Level);
        Assert.False(verdict.IsWarning);
        Assert.Contains("proves nothing", verdict.Detail);
    }

    [Fact]
    public void InPersonRedirectsToWhatActuallyMattersThere()
    {
        var verdict = Check(SellerDelivery.InPerson, SellerLicence.Unspecified);

        Assert.Contains("mother", verdict.Detail);
        Assert.Contains("health-test", verdict.Detail);
    }

    // ---- a licence is a floor, never an endorsement ----

    [Fact]
    public void AGivenNumberIsSomethingToVerifyRatherThanToTrust()
    {
        var verdict = Check(SellerDelivery.SightUnseen, SellerLicence.Given);

        Assert.Equal("Verify", verdict.Level);
        var lookup = Assert.Single(verdict.Actions!, a => a.Href is not null);
        Assert.Contains("aphis", lookup.Href!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(SellerDelivery.SightUnseen)]
    [InlineData(SellerDelivery.InPerson)]
    public void HoldingALicenceIsNeverPresentedAsAnEndorsement(SellerDelivery delivery)
    {
        // Minimum standards enforced by inspection — and some of the operations a buyer most
        // wants to avoid hold one. Saying so is the same rule the price check follows for a
        // plausible price.
        var verdict = Check(delivery, SellerLicence.Given);

        Assert.Contains("floor", verdict.Detail);
    }

    [Fact]
    public void ANumberCanBeCopiedSoTheNameAndAddressAreCheckedToo()
    {
        var verdict = Check(SellerDelivery.SightUnseen, SellerLicence.Given);

        Assert.Contains("name and address match", verdict.Detail);
        Assert.Contains("inspection reports", verdict.Detail, StringComparison.OrdinalIgnoreCase);
    }

    // ---- unanswered ----

    [Fact]
    public void WithNoDeliveryAnswerItExplainsTheRuleRatherThanGuessing()
    {
        var verdict = Check(SellerDelivery.Unspecified, SellerLicence.Unspecified);

        Assert.Equal("Unknown", verdict.Level);
        Assert.False(verdict.IsWarning);
        Assert.Contains("four breeding females", verdict.Detail);
    }

    [Fact]
    public void ShippingWithNoLicenceAnswerYetAsksTheQuestion()
    {
        var verdict = Check(SellerDelivery.SightUnseen, SellerLicence.Unspecified);

        Assert.Equal("Unknown", verdict.Level);
        Assert.Contains("Ask", verdict.Headline);
    }

    [Fact]
    public void EveryVerdictOffersSomethingToDo()
    {
        foreach (var delivery in new[] { SellerDelivery.SightUnseen, SellerDelivery.InPerson })
        {
            foreach (var licence in Enum.GetValues<SellerLicence>())
            {
                var verdict = Check(delivery, licence);
                Assert.NotEmpty(verdict.Actions!);
                Assert.All(verdict.Actions!, a => Assert.NotEmpty(a.Text));
            }
        }
    }

    // ---- parsing ----

    [Theory]
    [InlineData("sight-unseen", SellerDelivery.SightUnseen)]
    [InlineData("in-person", SellerDelivery.InPerson)]
    [InlineData("IN-PERSON", SellerDelivery.InPerson)]
    [InlineData("nonsense", SellerDelivery.Unspecified)]
    [InlineData(null, SellerDelivery.Unspecified)]
    public void ParsesDeliveryAndRefusesToGuess(string? raw, SellerDelivery expected) =>
        Assert.Equal(expected, SellerCheck.ParseDelivery(raw));

    [Theory]
    [InlineData("given", SellerLicence.Given)]
    [InlineData("exempt", SellerLicence.ClaimedExempt)]
    [InlineData("refused", SellerLicence.Refused)]
    [InlineData("maybe", SellerLicence.Unspecified)]
    [InlineData(null, SellerLicence.Unspecified)]
    public void ParsesLicenceAndRefusesToGuess(string? raw, SellerLicence expected) =>
        Assert.Equal(expected, SellerCheck.ParseLicence(raw));
}
