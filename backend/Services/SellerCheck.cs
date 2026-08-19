namespace PuppyFinder.Api.Services;

/// <summary>How the buyer would take delivery — the input the licensing rule actually turns on.</summary>
public enum SellerDelivery
{
    Unspecified,

    /// <summary>Shipped, flown, driven, or handed over without the buyer having seen the puppy.</summary>
    SightUnseen,

    /// <summary>The buyer will see this puppy in person before paying.</summary>
    InPerson,
}

/// <summary>What the seller said when asked for a USDA licence number.</summary>
public enum SellerLicence
{
    Unspecified,

    /// <summary>They gave a number. It is verifiable, which is the point.</summary>
    Given,

    /// <summary>They say they don't need one.</summary>
    ClaimedExempt,

    /// <summary>They won't give one, changed the subject, or went quiet.</summary>
    Refused,
}

/// <param name="Level">Required | Unverifiable | Verify | Exempt | Unknown</param>
public record SellerVerdict(
    string Level,
    string Headline,
    string Detail,
    bool IsWarning,
    IReadOnlyList<SafetyAction>? Actions = null);

/// <summary>
/// Whether a seller is legally required to hold a USDA licence, and what to do about the answer.
///
/// <para>
/// This is the one check in the app that ends in a public database rather than in advice. Under
/// the Animal Welfare Act a breeder needs a USDA Class A licence when <b>both</b> conditions
/// hold: more than four breeding females, <b>and</b> selling sight-unseen — online, by phone, by
/// mail, or through a broker or pet store. The 2013 Retail Pet Store Rule is what pulled
/// internet sellers in: the retail exemption now reaches only sales where the buyer can
/// personally observe the animal before buying.
/// </para>
///
/// <para>
/// The line that does the work: <b>a puppy shipped to you is not a face-to-face sale.</b> A
/// seller who will not let you see the dog first cannot be leaning on that exemption, so either
/// they keep four or fewer breeding females or they are required to be licensed. That is a
/// question with two possible answers, both checkable, and it is asked before any money moves.
/// </para>
///
/// <para>
/// What this must never become is a seal of approval. A licence means minimum standards for
/// housing, sanitation, vet care and records, enforced by unannounced inspection — it says
/// nothing about whether this is a good breeder, and plenty of licensed operations are exactly
/// what a buyer is trying to avoid. Equally, an unlicensed seller is not thereby a scammer: the
/// small-breeder exemption is real and most good hobby breeders sit inside it. Both directions
/// of that error are stated in the verdicts rather than left for the reader to infer.
/// </para>
/// </summary>
public static class SellerCheck
{
    private const string PublicSearch = "https://aphis.my.site.com/PublicSearchTool/s/";

    private static readonly SafetyAction LookUpTheLicence = new(
        "Look the number up in USDA APHIS's public search tool and check the name and address "
        + "match what the seller told you. Inspection reports are attached to the record — read "
        + "them, because a licence on its own says nothing about what the inspector found.",
        PublicSearch);

    private static readonly SafetyAction AskWhichExemption = new(
        "Ask which exemption they are claiming. There are only two that can apply to a pet sale: "
        + "four or fewer breeding females, or selling face-to-face only. Shipping a puppy to you "
        + "is not face-to-face, so the answer has to be the first one.");

    private static readonly SafetyAction CountTheLitters = new(
        "Test the \"four or fewer breeding females\" claim against their own listings. A seller "
        + "advertising several breeds, or puppies always available, is not running a four-female "
        + "operation — those two statements cannot both be true.");

    private static readonly SafetyAction SeeItInPerson = new(
        "Arrange to see the puppy and its mother in person, or offer to collect it yourself. That "
        + "removes the licensing question entirely and answers a dozen others at the same time.");

    public static SellerVerdict Evaluate(SellerDelivery delivery, SellerLicence licence)
    {
        if (delivery == SellerDelivery.Unspecified)
        {
            return new SellerVerdict(
                "Unknown",
                "Start with how you'd get the puppy",
                "USDA licensing turns on exactly that. A breeder needs a licence when they keep more "
                + "than four breeding females and sell sight-unseen — online, by phone, by mail, or "
                + "through a broker. If you are collecting the puppy in person, the rule most likely "
                + "does not apply to them at all, and its absence tells you nothing.",
                IsWarning: false);
        }

        if (delivery == SellerDelivery.InPerson)
        {
            // The honest answer here is "this check doesn't apply", and saying so is worth more
            // than manufacturing a warning. Most good hobby breeders are legitimately exempt.
            return new SellerVerdict(
                "Exempt",
                "Seeing it in person takes licensing off the table",
                "The retail exemption covers sales where the buyer sees the animal before buying, so "
                + "a small breeder selling to you face-to-face is not required to hold a USDA licence "
                + "and its absence proves nothing. That is most good hobby breeders. What matters "
                + "instead is what you see when you get there: the mother, the conditions the puppies "
                + "actually live in, and health-test results on paper for both parents."
                + (licence == SellerLicence.Given
                    ? " They gave you a number anyway, which you can still verify — a licence is a "
                      + "floor, not a recommendation."
                    : string.Empty),
                IsWarning: false,
                licence == SellerLicence.Given ? [LookUpTheLicence, SeeItInPerson] : [SeeItInPerson]);
        }

        // Sight-unseen from here down, which is where the rule bites.
        return licence switch
        {
            SellerLicence.Given => new SellerVerdict(
                "Verify",
                "Good — now verify the number rather than trusting it",
                "A number is only worth what checking it proves. Look it up in USDA APHIS's public "
                + "search tool and confirm the name and address match what you were told; a number "
                + "copied off somebody else's website is the cheapest thing in this whole scam to "
                + "fake. And read the inspection reports attached to the record. A licence is a "
                + "floor — minimum standards for housing, sanitation, vet care and records, enforced "
                + "by unannounced inspection. It is not USDA vouching for this breeder, and some of "
                + "the operations a buyer most wants to avoid hold one.",
                IsWarning: false,
                [LookUpTheLicence, SeeItInPerson]),

            SellerLicence.ClaimedExempt => new SellerVerdict(
                "Unverifiable",
                "Only one exemption can apply if they're shipping to you",
                "There are two exemptions for a pet sale: four or fewer breeding females, or selling "
                + "face-to-face only. Shipping a puppy to you is not a face-to-face sale — the "
                + "retail exemption stops at buyers who can see the animal before buying, which is "
                + "the whole point of the 2013 rule that brought internet sellers under the Act. So "
                + "the claim has to be the four-females one, and that is testable against their own "
                + "advertising rather than taken on trust.",
                IsWarning: false,
                [AskWhichExemption, CountTheLitters, SeeItInPerson]),

            SellerLicence.Refused => new SellerVerdict(
                "Required",
                "They ship sight-unseen and won't produce a licence number",
                "In this exact situation a licence is either required or provably unnecessary, and "
                + "both answers are easy to give. A licensed breeder reads their number off the "
                + "certificate; an exempt one says \"I keep three breeding females\". Refusing, "
                + "deflecting or going quiet is not a third answer — it is the answer. Nothing here "
                + "proves a scam on its own, but this is a question with no innocent silence, and "
                + "you are being asked to send money to someone who will not answer it.",
                IsWarning: true,
                [AskWhichExemption, CountTheLitters, SeeItInPerson]),

            _ => new SellerVerdict(
                "Unknown",
                "Ask them for their USDA licence number",
                "They are selling sight-unseen, so the rule is live: more than four breeding females "
                + "plus a sight-unseen sale means a USDA Class A licence is required. Ask for the "
                + "number. Whatever comes back — a number, an exemption claim, or a change of "
                + "subject — tells you something, and the question costs nothing to ask before any "
                + "money moves.",
                IsWarning: false,
                [AskWhichExemption, SeeItInPerson]),
        };
    }

    public static SellerDelivery ParseDelivery(string? value) => value?.ToLowerInvariant() switch
    {
        "sight-unseen" => SellerDelivery.SightUnseen,
        "in-person" => SellerDelivery.InPerson,
        _ => SellerDelivery.Unspecified,
    };

    public static SellerLicence ParseLicence(string? value) => value?.ToLowerInvariant() switch
    {
        "given" => SellerLicence.Given,
        "exempt" => SellerLicence.ClaimedExempt,
        "refused" => SellerLicence.Refused,
        _ => SellerLicence.Unspecified,
    };
}
