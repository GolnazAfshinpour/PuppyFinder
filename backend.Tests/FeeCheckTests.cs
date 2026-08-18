using PuppyFinder.Api.Data;
using PuppyFinder.Api.Services;

namespace PuppyFinder.Api.Tests;

public class FeeCheckTests
{
    // ---- the invented fees, which are the whole point ----

    [Theory]
    [InlineData("they want $350 for a refundable crate rental")]
    [InlineData("seller says I need to pay shipping insurance")]
    [InlineData("asking for a city permit before the flight")]
    [InlineData("says the puppy got sick and there's an emergency vet bill")]
    [InlineData("customs fee at the border")]
    [InlineData("free to a good home, just pay shipping")]
    public void RecognisesTheDocumentedScamFees(string text)
    {
        var verdict = FeeCheck.Evaluate(text, alreadyPaid: false);
        Assert.Equal("Invented", verdict.Level);
        Assert.True(verdict.IsWarning);
        Assert.NotNull(verdict.Matched);
    }

    [Fact]
    public void AnInventedFeeAfterPaymentBecomesStopPaying()
    {
        // The instruction has to change with the sequence: someone deciding needs "don't send
        // it", someone already in it needs "stop", and those are different sentences.
        var deciding = FeeCheck.Evaluate("a $350 crate deposit", alreadyPaid: false);
        var mid = FeeCheck.Evaluate("a $350 crate deposit", alreadyPaid: true);

        Assert.Equal("Invented", deciding.Level);
        Assert.Equal("StopPaying", mid.Level);
        Assert.Contains("Stop paying", mid.Headline);
        Assert.True(mid.IsWarning);
    }

    [Fact]
    public void TellsSomeoneMidScamWhatToSaveBeforeGoingQuiet()
    {
        // A bank dispute and a police report both need the forgeries, and "stop paying" on its
        // own loses them.
        var verdict = FeeCheck.Evaluate("they want another $299 for shipping insurance", alreadyPaid: true);
        Assert.Contains("save", verdict.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("evidence", verdict.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NamesTheThreatsAsScriptedForSomeoneAlreadyPaying()
    {
        var verdict = FeeCheck.Evaluate("emergency vet bill", alreadyPaid: true);
        Assert.Contains("abandonment", verdict.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RefundableIsCalledOutAsTheTellRatherThanReassurance()
    {
        var verdict = FeeCheck.Evaluate("a refundable $350 crate deposit", alreadyPaid: false);
        Assert.True(verdict.Refundable);
        Assert.Contains("tell", verdict.Detail, StringComparison.OrdinalIgnoreCase);
    }

    // ---- the other direction, which matters just as much ----

    [Fact]
    public void ALegitimateDepositIsNotCalledAScam()
    {
        // Being wrong this way costs someone a real dog and teaches them to ignore the next
        // warning. It must not come back as a warning.
        var verdict = FeeCheck.Evaluate("a deposit to hold a puppy from the next litter", alreadyPaid: false);
        Assert.Equal("Real", verdict.Level);
        Assert.False(verdict.IsWarning);
    }

    [Theory]
    [InlineData("adoption fee at the shelter")]
    [InlineData("flight nanny to bring her here")]
    [InlineData("health certificate for the flight")]
    public void RecognisesRealCosts(string text) =>
        Assert.Equal("Real", FeeCheck.Evaluate(text, alreadyPaid: false).Level);

    [Fact]
    public void ARealCostIsNeverPresentedAsAnAllClear()
    {
        // Same rule the price check follows: a plausible answer is not a safety signal, and
        // scammers name real costs precisely because they are real.
        var verdict = FeeCheck.Evaluate("a deposit to hold a puppy", alreadyPaid: false);
        Assert.Contains("does not make this request safe", verdict.Detail);
    }

    [Fact]
    public void ARealCostArrivingAfterPaymentIsStillQuestioned()
    {
        var verdict = FeeCheck.Evaluate("ground transport", alreadyPaid: true);
        Assert.Equal("Real", verdict.Level);
        Assert.Contains("up front", verdict.Detail);
    }

    [Fact]
    public void ChargingExtraForPapersIsItsOwnRedFlag()
    {
        var verdict = FeeCheck.Evaluate("says the akc papers are $200 extra", alreadyPaid: false);
        Assert.Equal("Papers", verdict.Level);
        Assert.True(verdict.IsWarning);
    }

    // ---- unknown fees, where the sequence has to carry it alone ----

    [Fact]
    public void AnUnknownFeeAfterPaymentIsStillTheWarning()
    {
        // The catalog is a list of fees people have already reported. A scammer changes the
        // name for free, so "we don't recognise it" must not read as "it's fine".
        var verdict = FeeCheck.Evaluate("a $600 lineage verification charge", alreadyPaid: true);
        Assert.Equal("Unrecognised", verdict.Level);
        Assert.True(verdict.IsWarning);
        Assert.Contains("sequence", verdict.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnUnknownFeeBeforePaymentGetsATestRatherThanAVerdict()
    {
        var verdict = FeeCheck.Evaluate("a $600 lineage verification charge", alreadyPaid: false);
        Assert.Equal("Unrecognised", verdict.Level);
        Assert.False(verdict.IsWarning);
        Assert.Contains("in writing", verdict.Detail);
    }

    [Fact]
    public void EmptyInputAsksTheQuestionInsteadOfAnswering()
    {
        var verdict = FeeCheck.Evaluate("   ", alreadyPaid: false);
        Assert.Equal("Unrecognised", verdict.Level);
        Assert.False(verdict.IsWarning);
        Assert.Null(verdict.Matched);
    }

    // ---- details ----

    [Theory]
    [InlineData("a $350 crate fee", 350)]
    [InlineData("they want $1,200 for shipping insurance", 1200)]
    [InlineData("$99.99 permit", 99)]
    [InlineData("a crate fee", null)]
    public void EchoesTheAmountBackWithoutJudgingIt(string text, int? expected) =>
        Assert.Equal(expected, FeeCheck.Evaluate(text, alreadyPaid: false).Amount);

    [Fact]
    public void PrefersTheMoreSpecificFeeWhenTwoPhrasesMatch()
    {
        // "shipping insurance" is invented; "shipping cost" is real. Matching the shorter one
        // first would wave the scam through as a real transport charge.
        var verdict = FeeCheck.Evaluate("shipping insurance on top of the shipping cost", alreadyPaid: false);
        Assert.Equal("Invented", verdict.Level);
    }

    [Fact]
    public void AnInventedFeeWinsOverARealOneEvenWhenItsNameIsShorter()
    {
        // "shipping cost" (real, 13 chars) would beat "crate" (invented, 5) on length alone,
        // answering "transport is a real expense" to a request for crate money.
        var verdict = FeeCheck.Evaluate("the shipping cost includes a crate they want paid separately", alreadyPaid: false);
        Assert.Equal("Invented", verdict.Level);
        Assert.Contains("crate", verdict.Matched);
    }

    [Fact]
    public void MatchingIsCaseInsensitive() =>
        Assert.Equal("Invented", FeeCheck.Evaluate("REFUNDABLE CRATE DEPOSIT", alreadyPaid: false).Level);

    [Fact]
    public void EveryCatalogEntryHasPhrasesAndAnExplanation()
    {
        Assert.NotEmpty(FeeCatalog.Types);
        foreach (var type in FeeCatalog.Types)
        {
            Assert.NotEmpty(type.Phrases);
            Assert.NotEmpty(type.Detail);
            Assert.NotEmpty(type.Label);
            // Phrases are matched against lowercased input, so an uppercase one can never hit.
            Assert.All(type.Phrases, p => Assert.Equal(p.ToLowerInvariant(), p));
        }
    }

    [Fact]
    public void CatalogIdsAreUnique() =>
        Assert.Equal(FeeCatalog.Types.Count, FeeCatalog.Types.Select(t => t.Id).Distinct().Count());

    // ---- who is asking: the scam's second actor ----

    [Fact]
    public void ATransporterThatMadeContactIsTheFindingWhateverTheFeeIsCalled()
    {
        // BBB's script: deposit, then a second party appears posing as a shipper, and every fee
        // from there comes from them. The name of the fee stops mattering at that point — so a
        // request this catalog has never seen still resolves to the handoff.
        var verdict = FeeCheck.Evaluate(
            "a $240 lineage verification charge", alreadyPaid: false, FeeAsker.TransporterContactedMe);

        Assert.Equal("Handoff", verdict.Level);
        Assert.True(verdict.IsWarning);
    }

    [Fact]
    public void TheHandoffAfterPaymentBecomesStopPaying()
    {
        var verdict = FeeCheck.Evaluate(
            "shipping insurance", alreadyPaid: true, FeeAsker.TransporterContactedMe);

        Assert.Equal("StopPaying", verdict.Level);
        Assert.Contains("Stop paying", verdict.Headline);
    }

    [Fact]
    public void TheHandoffStillNamesTheFeeItRecognised()
    {
        // Leading with the structural finding must not throw away the specific one.
        var verdict = FeeCheck.Evaluate(
            "a refundable crate rental", alreadyPaid: false, FeeAsker.TransporterContactedMe);

        Assert.Equal("Handoff", verdict.Level);
        Assert.Contains("crate", verdict.Matched);
        Assert.True(verdict.Refundable);
    }

    [Fact]
    public void ATransporterTheBuyerBookedIsNotTheHandoff()
    {
        // The distinction is the whole point of asking. An invoice from a company you found is a
        // different thing from one that arrived unasked, and conflating them would call every
        // real pet shipper a scammer.
        var verdict = FeeCheck.Evaluate(
            "ground transport", alreadyPaid: false, FeeAsker.TransporterIBooked);

        Assert.Equal("Real", verdict.Level);
        Assert.False(verdict.IsWarning);
        Assert.Contains("right way round", verdict.Detail);
    }

    [Fact]
    public void AnAskerIsNeverAssumed()
    {
        // Unanswered must behave like the seller case, not like the safest-sounding one: guessing
        // "you booked them yourself" would hand the calm answer to the person in the handoff.
        var unanswered = FeeCheck.Evaluate("a crate fee", alreadyPaid: false);
        var seller = FeeCheck.Evaluate("a crate fee", alreadyPaid: false, FeeAsker.Seller);

        Assert.Equal(seller.Level, unanswered.Level);
        Assert.Equal(seller.Headline, unanswered.Headline);
    }

    [Theory]
    [InlineData("seller", FeeAsker.Seller)]
    [InlineData("transporter-contacted-me", FeeAsker.TransporterContactedMe)]
    [InlineData("transporter-i-booked", FeeAsker.TransporterIBooked)]
    [InlineData("TRANSPORTER-I-BOOKED", FeeAsker.TransporterIBooked)]
    [InlineData("nonsense", FeeAsker.Unspecified)]
    [InlineData(null, FeeAsker.Unspecified)]
    public void ParsesTheAskerAndRefusesToGuess(string? raw, FeeAsker expected) =>
        Assert.Equal(expected, FeeCheck.ParseAsker(raw));

    // ---- the newly documented fees ----

    [Theory]
    [InlineData("they say she's stuck at the airport and need a release fee")]
    [InlineData("quarantine charge before they'll hand her over")]
    [InlineData("held in customs, storage fee accruing daily")]
    public void RecognisesTheReleaseStage(string text)
    {
        var verdict = FeeCheck.Evaluate(text, alreadyPaid: true);
        Assert.Equal("StopPaying", verdict.Level);
        Assert.Contains("quarantine", verdict.Matched, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RefundableInsuranceIsNamedAsAnImpossibilityNotASmell()
    {
        // A premium buys cover for a period; there is nothing to hand back. Saying only that it is
        // "suspicious" understates a claim that cannot be true of any real product.
        var verdict = FeeCheck.Evaluate("refundable insurance for the flight", alreadyPaid: false);
        Assert.Equal("Invented", verdict.Level);
        Assert.Contains("it is not a product", verdict.Detail);
    }

    // ---- next actions ----

    [Fact]
    public void EveryWarningOffersTheTestThatEndsItFastest()
    {
        // A real puppy can be collected. Nothing else in the app said so, and it settles the
        // question without any analysis of the fee.
        foreach (var verdict in new[]
        {
            FeeCheck.Evaluate("a crate deposit", alreadyPaid: true),
            FeeCheck.Evaluate("shipping insurance", alreadyPaid: false),
            FeeCheck.Evaluate("a $99 widget charge", alreadyPaid: true),
            FeeCheck.Evaluate("anything", alreadyPaid: false, FeeAsker.TransporterContactedMe),
        })
        {
            Assert.NotNull(verdict.Actions);
            Assert.Contains(verdict.Actions!, a => a.Text.Contains("collect the dog yourself"));
        }
    }

    [Fact]
    public void TheHandoffPointsAtTheDirectoryRatherThanAtTheirPaperwork()
    {
        var verdict = FeeCheck.Evaluate("crate fee", alreadyPaid: false, FeeAsker.TransporterContactedMe);
        var ipata = Assert.Single(verdict.Actions!, a => a.Href == "https://www.ipata.org/");
        // The specific tell, which is decisive on its own and costs nothing to check.
        Assert.Contains("IPATA", ipata.Text);
        Assert.Contains(verdict.Actions!, a => a.Text.Contains("search engine"));
    }

    [Fact]
    public void UnrecoverableRailsAreNamedInFull()
    {
        // Western Union and MoneyGram are the two BBB and IPATA name most often, and they were
        // missing from every list in the app.
        var verdict = FeeCheck.Evaluate("a crate deposit", alreadyPaid: false);
        var rails = Assert.Single(verdict.Actions!, a => a.Text.Contains("Western Union"));
        Assert.Contains("MoneyGram", rails.Text);
    }

    [Fact]
    public void EveryActionSaysSomethingDoable()
    {
        foreach (var type in new[] { FeeAsker.Seller, FeeAsker.TransporterContactedMe, FeeAsker.TransporterIBooked })
        {
            foreach (var paid in new[] { true, false })
            {
                var verdict = FeeCheck.Evaluate("a crate deposit", paid, type);
                Assert.All(verdict.Actions ?? [], a => Assert.NotEmpty(a.Text));
            }
        }
    }
}
