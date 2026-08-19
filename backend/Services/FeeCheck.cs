using System.Globalization;
using System.Text.RegularExpressions;
using PuppyFinder.Api.Data;

namespace PuppyFinder.Api.Services;

/// <summary>Who is asking for the money.</summary>
public enum FeeAsker
{
    /// <summary>Not answered. Never assumed — see <see cref="FeeCheck"/>.</summary>
    Unspecified,

    /// <summary>The seller or breeder themselves.</summary>
    Seller,

    /// <summary>
    /// A transport company that made contact on its own. This is the scam's second act, and it
    /// is a finding independent of what the fee is called.
    /// </summary>
    TransporterContactedMe,

    /// <summary>A transporter the buyer found and booked. A real company sending a real invoice.</summary>
    TransporterIBooked,
}

/// <summary>
/// One thing the reader can do next. Concrete and checkable, never "be careful".
///
/// <para>
/// Shared with <see cref="SellerCheck"/>: both answer "what do I do now", and duplicating the
/// type would let the two drift into rendering differently for no reason.
/// </para>
/// </summary>
public record SafetyAction(string Text, string? Href = null);

/// <summary>What a seller's request for money means.</summary>
/// <param name="Level">StopPaying | Invented | Handoff | Papers | Real | Unrecognised</param>
/// <param name="Matched">The fee we recognised, or null. Named back so the reader can tell we understood them.</param>
/// <param name="Amount">The dollar figure in their text, if there was one. Echoed, never judged — there is no "safe" amount.</param>
public record FeeVerdict(
    string Level,
    string Headline,
    string Detail,
    bool IsWarning,
    string? Matched = null,
    int? Amount = null,
    bool Refundable = false,
    IReadOnlyList<SafetyAction>? Actions = null);

/// <summary>
/// Screens what a seller is asking money for, without needing a price range.
///
/// <para>
/// The price check answers "is this quote plausible for this breed", which requires a sourced
/// range and is therefore live for 50 of 174 breeds. This answers a different question — "they
/// are asking me for $350, should I send it" — and needs no range at all, so it works for every
/// breed and, more importantly, for the person who has already paid.
/// </para>
///
/// <para>
/// Three inputs, and the fee's name is the least decisive of them.
/// </para>
///
/// <para>
/// <b>Sequence.</b> BBB's finding is that once a second payment is requested after a deposit,
/// there is no puppy. So a recognised scam fee is damning on its own, and <i>any</i> unexpected
/// fee that appears after money has moved is the pattern regardless of what it is called —
/// including one this catalog has never seen, because the inventory of invented fees is not
/// fixed and a tool that only knew the published ones would reassure people about the next one.
/// </para>
///
/// <para>
/// <b>Who is asking.</b> The scam has two actors. BBB's script is specific: after the first
/// payment the buyer is contacted again, by someone posing as a shipping company, and it is that
/// second party who demands the crate, the insurance and the permit. A transport company that
/// made contact on its own — one the buyer never chose — is the handoff itself, and that is a
/// finding whatever the fee is called. It is also why the question distinguishes a transporter
/// who contacted you from one you found and booked: the second is a real company sending a real
/// invoice, and must not be swept up with the first.
/// </para>
///
/// <para>
/// Symmetry matters as much here as in <see cref="PriceCheck"/>: a legitimate deposit must not
/// come back as "this is a scam". Being wrong in that direction costs someone a real dog and
/// teaches them to ignore the next warning.
/// </para>
/// </summary>
public static class FeeCheck
{
    /// <summary>
    /// Invented fees are matched first, then papers, then real costs; within a kind, the longest
    /// phrase wins.
    ///
    /// <para>
    /// Kind has to outrank length or a generic real cost shadows a specific scam one in the same
    /// sentence: "shipping cost for the crate" contains both, and ranking by length alone answers
    /// "transport is a real expense" about a request for crate money. The documented fees have
    /// narrow, specific names and someone is typing what they are being asked to pay for, so a
    /// mention is the finding. Length still decides inside a kind, which is what keeps "shipping
    /// insurance" from resolving to the shorter "insurance for the flight".
    /// </para>
    /// </summary>
    private static readonly IReadOnlyList<(FeeType Type, string Phrase)> Phrases =
        FeeCatalog.Types
            .SelectMany(t => t.Phrases.Select(p => (Type: t, Phrase: p)))
            .OrderBy(x => x.Type.Kind switch
            {
                FeeKind.Invented => 0,
                FeeKind.ExtraForWhatShouldBeIncluded => 1,
                _ => 2,
            })
            .ThenByDescending(x => x.Phrase.Length)
            .ToList();

    private static readonly Regex Money = new(
        @"\$\s*(\d{1,3}(?:,\d{3})+|\d+)(?:\.\d{2})?", RegexOptions.Compiled);

    /// <summary>
    /// The test that ends it fastest, and the one the app was missing entirely. A real puppy can
    /// be collected; a puppy that does not exist cannot. IPATA's own advice to buyers is to offer
    /// to fly or drive and pick the animal up — a seller who will not arrange that has told you
    /// what you needed to know, without any analysis of the fee.
    /// </summary>
    private static readonly SafetyAction PickupTest = new(
        "Offer to collect the dog yourself — say you will fly or drive to them this week and take "
        + "it home in your own car. A real puppy can be picked up. This ends the conversation "
        + "faster than arguing about the fee.");

    /// <summary>
    /// Fake transport sites are real ones with the company name swapped, so their own words are
    /// the thing that gives them away.
    /// </summary>
    private static readonly SafetyAction CopiedTextTest = new(
        "Paste a full sentence from their email or website into a search engine. These companies "
        + "are built by copying a real transporter's site and changing the name, so their own "
        + "words usually turn up on somebody else's page.");

    private static readonly SafetyAction VerifyWithIpata = new(
        "Look the company up in IPATA's member directory, not through any link they sent you. "
        + "Real pet shippers are members, scammers copy the IPATA logo — and no genuine shipping "
        + "company has \"IPATA\" in its own name.",
        "https://www.ipata.org/");

    private static readonly SafetyAction UnrecoverableRails = new(
        "Send nothing by wire, Western Union, MoneyGram, gift card, Zelle, Cash App or crypto. "
        + "Those are chosen because they cannot be reversed, not because they are convenient.");

    public static FeeVerdict Evaluate(string? text, bool alreadyPaid, FeeAsker asker = FeeAsker.Unspecified)
    {
        var normalised = (text ?? string.Empty).ToLowerInvariant().Trim();
        if (normalised.Length == 0)
        {
            return new FeeVerdict(
                "Unrecognised",
                "Tell us what they're asking for",
                "Type what the seller wants money for — \"a $350 refundable crate deposit\", \"shipping "
                + "insurance\", \"a permit\". You don't need to know the breed's price for this; the "
                + "question is whether the fee itself is real.",
                IsWarning: false);
        }

        var amount = ParseAmount(normalised);
        var refundable = FeeCatalog.RefundablePhrases.Any(normalised.Contains);
        var fee = Phrases.FirstOrDefault(x => normalised.Contains(x.Phrase)).Type;
        var handoff = asker == FeeAsker.TransporterContactedMe;

        // The handoff outranks the fee's name, because it is the structural finding: a company
        // you never chose is now asking you for money about a dog you have not seen.
        if (handoff)
        {
            return Handoff(fee, amount, refundable, alreadyPaid);
        }

        return fee?.Kind switch
        {
            FeeKind.Invented => Invented(fee, amount, refundable, alreadyPaid),
            FeeKind.ExtraForWhatShouldBeIncluded => Papers(fee, amount),
            FeeKind.Real => Real(fee, amount, refundable, alreadyPaid, asker),
            // Nothing matched. The sequence still decides, and it decides without us knowing the name.
            _ => alreadyPaid ? UnrecognisedAfterPaying(amount) : UnrecognisedBeforePaying(),
        };
    }

    private static FeeVerdict Handoff(FeeType? fee, int? amount, bool refundable, bool alreadyPaid)
    {
        var detail =
            "This is the part of the scam that is hardest to see from inside it. The seller takes a "
            + "deposit, and then a second party appears — a shipping company you did not choose, did "
            + "not find, and cannot check — and every fee from here comes from them. Presenting the "
            + "money as somebody else's requirement is what makes it feel unavoidable rather than "
            + "like a demand from the person you are already paying. In the documented cases the two "
            + "are the same people, and the transport company exists only as a website.";

        if (fee is not null)
        {
            detail += $" What they are asking for is {fee.Label}, which is its own answer: {fee.Detail}";
        }

        if (refundable)
        {
            detail += " \"Refundable\" is the tell rather than the reassurance: it is what makes the "
                + "payment feel like a formality instead of a loss, and in these cases the money never "
                + "comes back.";
        }

        detail += alreadyPaid
            ? " You have already sent money, so treat this as the moment it stops. What you have paid "
              + "is gone whether or not you pay again, the requests continue until you stop, and the "
              + "threats that follow — the dog will die, you will be charged with abandonment — are "
              + "part of the script and do not happen. Save the conversation, the receipts and every "
              + "document they sent you first: the forgeries are evidence a bank dispute or a police "
              + "report needs."
            : " Verify the company before anything moves, and do it through a directory you found "
              + "yourself rather than a link, a logo or a certificate they sent you.";

        return new FeeVerdict(
            alreadyPaid ? "StopPaying" : "Handoff",
            alreadyPaid
                ? "Stop paying. A shipper you never hired is the scam's second act."
                : "A transport company you did not choose is the scam's second act",
            detail,
            IsWarning: true,
            fee?.Label,
            amount,
            refundable,
            [VerifyWithIpata, PickupTest, CopiedTextTest, UnrecoverableRails]);
    }

    private static FeeVerdict Invented(FeeType fee, int? amount, bool refundable, bool alreadyPaid)
    {
        // The one instruction that matters, lifted above everything explaining it — the same
        // shape the safety guide uses, because the other sentences exist to justify this one.
        var headline = alreadyPaid
            ? "Stop paying. This fee does not exist."
            : "This fee does not exist. Don't send it.";

        var detail = fee.Detail;

        if (refundable)
        {
            detail += " \"Refundable\" is the tell rather than the reassurance: it is what makes the "
                + "payment feel like a formality instead of a loss, and in these cases the money never "
                + "comes back.";

            // Not merely suspicious — self-contradictory. IPATA states it flatly, and the pairing
            // is common enough in reports to be worth naming as an impossibility rather than a smell.
            if (fee.Id == "shipping-insurance")
            {
                detail += " Insurance is not refundable, by definition: a premium buys cover for a "
                    + "period, and there is nothing to give back at the end of it. \"Refundable "
                    + "insurance\" is not a suspicious product, it is not a product.";
            }
        }

        detail += alreadyPaid
            ? " What you have already sent is gone whether or not you send this. The requests continue "
              + "until you stop, and the threats that follow — the dog will die, you will be charged "
              + "with abandonment — are part of the script and do not happen. Before you go quiet, "
              + "save the conversation, the receipts and every document they sent you: the forgeries "
              + "are evidence a bank dispute or a police report needs."
            : " Treat this as the end of the conversation rather than something to negotiate. A seller "
              + "who invents one fee has more of them.";

        return new FeeVerdict(
            alreadyPaid ? "StopPaying" : "Invented",
            headline, detail, IsWarning: true, fee.Label, amount, refundable,
            [PickupTest, UnrecoverableRails]);
    }

    private static FeeVerdict Papers(FeeType fee, int? amount) => new(
        "Papers",
        "Paying extra for papers is a red flag by itself",
        fee.Detail,
        IsWarning: true,
        fee.Label,
        amount,
        Actions: [PickupTest]);

    private static FeeVerdict Real(FeeType fee, int? amount, bool refundable, bool alreadyPaid, FeeAsker asker)
    {
        // Deliberately not an all-clear, for the same reason a plausible price isn't one: the fee
        // being real says nothing about this seller.
        var detail = fee.Detail
            + " That this cost is real does not make this request safe — a scammer names real costs "
            + "too, and these are the ones they name.";

        if (refundable)
        {
            detail += " Be wary of \"refundable\" here. It is the word that makes an invented fee feel "
                + "like a formality, and it is doing the same work whatever the fee is attached to.";
        }

        if (asker == FeeAsker.TransporterIBooked)
        {
            detail += " You found and booked this company yourself, which is the right way round and "
                + "removes the part that does the damage — an invoice from a company you chose is a "
                + "different thing from one that arrived unasked. Pay it directly to them, by card.";
        }

        if (alreadyPaid)
        {
            detail += " And this one arrived after you had already sent money, which is the part that "
                + "matters most: a cost a real breeder has is a cost they name in the total, up front. "
                + "Ask why it was not in the original price, and get the answer before anything else "
                + "moves.";
        }

        var actions = alreadyPaid || asker != FeeAsker.TransporterIBooked
            ? new[] { PickupTest, UnrecoverableRails }
            : [UnrecoverableRails];

        return new FeeVerdict(
            "Real",
            alreadyPaid
                ? "This is a real cost — but it should have been in the price you agreed"
                : "This is a real cost. What matters is who you pay and when",
            detail,
            IsWarning: false,
            fee.Label,
            amount,
            refundable,
            actions);
    }

    private static FeeVerdict UnrecognisedAfterPaying(int? amount) => new(
        "Unrecognised",
        "We don't recognise that fee — and after a deposit, that is the warning",
        "We match against the fees documented in published scam reports and this is not one of them, "
        + "which is not the same as it being safe: the inventory of invented fees is not fixed, and a "
        + "new name is the cheapest thing to change. What decides it is the sequence. A second request "
        + "for money after a deposit, for something that was not in the price you agreed, is the "
        + "pattern itself — whatever it is called. Ask why it was not in the original total, and get "
        + "the answer before anything else moves.",
        IsWarning: true,
        Amount: amount,
        Actions: [PickupTest, CopiedTextTest, UnrecoverableRails]);

    private static FeeVerdict UnrecognisedBeforePaying() => new(
        "Unrecognised",
        "We don't recognise that one — here's how to test it",
        "We match against the fees documented in published scam reports and this is not one of them. "
        + "That is not a clearance: ask for the complete total in writing, with every cost named, "
        + "before you send anything. Then hold them to it. Any fee that appears after you commit is "
        + "the scam's whole shape, whatever it is called — and pay by credit card, because a puppy "
        + "that never arrives is a billing error you can dispute, while a transfer you sent yourself "
        + "is not.",
        IsWarning: false,
        Actions: [PickupTest, CopiedTextTest]);

    /// <summary>
    /// The dollar figure in their text, echoed back so the reader can see we read it. Never
    /// judged: there is no amount that makes an invented fee real, and no amount too small to
    /// be the first of four.
    /// </summary>
    private static int? ParseAmount(string text)
    {
        var match = Money.Match(text);
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(
            match.Groups[1].Value.Replace(",", string.Empty),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value) ? value : null;
    }

    /// <summary>
    /// Query-string value to <see cref="FeeAsker"/>. Anything unrecognised is Unspecified rather
    /// than a guess — see the note in <see cref="FeeCheck"/> about never assuming this one.
    /// </summary>
    public static FeeAsker ParseAsker(string? value) => value?.ToLowerInvariant() switch
    {
        "seller" => FeeAsker.Seller,
        "transporter-contacted-me" => FeeAsker.TransporterContactedMe,
        "transporter-i-booked" => FeeAsker.TransporterIBooked,
        _ => FeeAsker.Unspecified,
    };
}
