namespace PuppyFinder.Api.Data;

/// <summary>What kind of thing a seller is asking money for.</summary>
public enum FeeKind
{
    /// <summary>A fee that does not exist. Named in report after report; the puppy does not exist either.</summary>
    Invented,

    /// <summary>Real, but charging extra for it is itself a documented red flag.</summary>
    ExtraForWhatShouldBeIncluded,

    /// <summary>A cost legitimate breeders, rescues and shelters really do have.</summary>
    Real,
}

/// <summary>
/// One thing a seller might ask for, and what it means.
/// </summary>
/// <param name="Id">Stable key, used in tests and by the UI's example chips.</param>
/// <param name="Label">How the fee is named back to the reader.</param>
/// <param name="Kind">Invented, extra-for-included, or real.</param>
/// <param name="Phrases">
/// Lowercase substrings that identify it. Dictionary matching only — deliberately not a model.
/// The rule that governs the rest of this app applies here with more force than anywhere else:
/// a wrong answer either accuses a legitimate breeder or waves a real scam through.
/// </param>
/// <param name="Detail">Why it is what it is. Every invented fee's text comes from published case material.</param>
public record FeeType(
    string Id,
    string Label,
    FeeKind Kind,
    IReadOnlyList<string> Phrases,
    string Detail);

/// <summary>
/// The fee taxonomy behind <see cref="Services.FeeCheck"/>.
///
/// <para>
/// Every check this app offered fired before the first payment — the price screen, the red
/// flags, the video call, the paperwork. BBB's finding is that the scam is profitable because a
/// "multi-tiered setup" lets the seller come back for money several times, so most of the loss
/// lands on payments two, three and four, and none of those payments had a check pointed at them.
/// This is that check.
/// </para>
///
/// <para>
/// It also needs no price range, which matters: the price screen is live for 50 of 175 breeds
/// and silent for the rest, while the fee a seller invents is the same fee whatever the breed.
/// </para>
///
/// <para>
/// The <b>real</b> entries are not padding. A tool that only knows scam fees would answer
/// "that's a scam" to a legitimate deposit, and being wrong in that direction is not the safe
/// side of the error — it costs someone the dog they were actually going to get, and it teaches
/// them to distrust the next warning too.
/// </para>
/// </summary>
public static class FeeCatalog
{
    /// <summary>
    /// The tell that outranks the fee's name. "Refundable" is what makes a victim agree: the
    /// money is framed as a formality that comes straight back, so it feels like proof of good
    /// faith rather than a payment. It never comes back.
    /// </summary>
    public static readonly IReadOnlyList<string> RefundablePhrases =
        ["refundable", "fully refunded", "you'll get it back", "you will get it back", "returned to you", "deposit back"];

    public static readonly IReadOnlyList<FeeType> Types =
    [
        new("crate", "a crate rental, deposit, or \"special\" crate", FeeKind.Invented,
            ["crate", "kennel rental", "carrier fee", "travel box", "climate-controlled box"],
            "This is the most reported version of the scam, and the \"refundable\" framing is part of "
            + "it. One documented sequence began with a $350 refundable crate rental and continued "
            + "through shipping insurance, a city permit, and an emergency vet bill. Airlines do not "
            + "rent crates to buyers, and no crate costs what these do."),

        new("shipping-insurance", "shipping or transport insurance", FeeKind.Invented,
            ["refundable insurance", "shipping insurance", "transport insurance", "travel insurance",
             "insurance for the flight", "flight insurance", "pet insurance for shipping",
             "cargo insurance", "insurance for the crate"],
            "There is no such product for a live animal being shipped to a buyer. It appears in these "
            + "cases specifically because it sounds prudent — the seller is inviting you to protect "
            + "an animal that does not exist."),

        new("permit", "a city, import, or travel permit", FeeKind.Invented,
            ["permit", "license fee", "licence fee", "import fee", "export fee", "clearance fee",
             "customs", "duty", "border fee"],
            "No permit is bought from a seller. Where paperwork is genuinely required for an animal to "
            + "travel, it is arranged by the shipper or a vet and it is not a payment you wire to the "
            + "person selling you the dog. One reported sequence used a $499 \"city permit\"."),

        new("emergency-vet", "an emergency or unexpected vet bill", FeeKind.Invented,
            ["emergency vet", "vet emergency", "sick at the airport", "fell ill", "got sick", "urgent care",
             "emergency treatment", "vet bill", "medical emergency"],
            "This one arrives with the threat attached: the dog will die and it will be your fault. It "
            + "is the last stage of the script, used when earlier fees have stopped working. One "
            + "reported sequence ended with an $800 emergency vet bill. There is no dog and there is "
            + "no vet."),

        new("vaccine", "a vaccination or microchip fee after you paid", FeeKind.Invented,
            ["vaccine deposit", "vaccination fee", "vaccine fee", "shots fee", "microchip fee",
             "microchipping fee", "deworming fee", "health check fee"],
            "Vaccination, deworming and microchipping happen before a puppy goes home and are part of "
            + "the price a real breeder quoted you at the start. Billed separately after you commit, "
            + "it is the same invention as the crate — a plausible-sounding line item attached to a "
            + "puppy nobody has."),

        new("quarantine", "a fee to release the dog from quarantine or the airport", FeeKind.Invented,
            ["quarantine", "stuck at the airport", "held at the airport", "stuck in customs",
             "held in customs", "release fee", "release the puppy", "release the dog", "detained",
             "won't release", "will not release", "storage fee"],
            "This is the stage where the dog becomes a hostage: non-delivery is blamed on customs or "
            + "quarantine, and money is demanded to \"release\" an animal that was never shipped. The "
            + "threat usually arrives with it — that the dog will die, or that you will be reported "
            + "for animal abandonment. Neither happens, and no authority has ever charged a buyer for "
            + "refusing to pay a scammer. There is no dog at an airport."),

        new("just-pay-shipping", "\"free, just pay shipping\"", FeeKind.Invented,
            ["free to a good home", "just pay shipping", "only pay shipping", "just cover shipping",
             "free puppy", "just pay for delivery"],
            "The shipping fee is the product. This is one of the oldest scripts there is: nobody gives "
            + "away a dog worth four figures by accident, and the \"free\" dog exists only to make the "
            + "fee feel like a bargain."),

        new("papers", "extra payment for registration papers", FeeKind.ExtraForWhatShouldBeIncluded,
            ["registration papers", "akc papers", "papers cost", "papers extra", "pedigree fee",
             "registration fee", "for the papers"],
            "Papers for a registered litter come with the puppy. \"Registration papers available for an "
            + "extra cost\" is a red flag in its own right — it usually means the litter is not "
            + "registered at all. An AKC-registered litter can be confirmed with the AKC directly, "
            + "before you pay anything."),

        new("deposit", "a deposit to hold a puppy", FeeKind.Real,
            ["deposit to hold", "hold a puppy", "holding fee", "reserve a puppy", "reservation fee",
             "put down a deposit", "waitlist deposit", "puppy deposit"],
            "Real breeders do take deposits, usually to reserve a place on a waitlist. What makes one "
            + "safe is not the amount: it is that you have met the breeder or seen the puppy and its "
            + "mother live, the total price and what the deposit counts toward are in writing, and you "
            + "paid by a method you can dispute. A deposit to someone you have not seen is just money "
            + "you cannot get back."),

        new("adoption-fee", "a shelter or rescue adoption fee", FeeKind.Real,
            ["adoption fee", "rescue fee", "shelter fee", "rehoming fee", "surrender fee"],
            "Shelter and rescue adoption fees run roughly $50–$500 and cover vaccinations, microchip "
            + "and usually spay or neuter — that is care costs, not a sale. A small rehoming fee "
            + "($50–$200) on classifieds is normal too, and protects the animal from being taken for "
            + "free. A four-figure \"rehoming fee\" is a sale wearing a costume; treat it with full "
            + "breeder-level scrutiny."),

        new("transport", "transport, ground delivery, or a flight nanny", FeeKind.Real,
            ["flight nanny", "ground transport", "pet transport", "delivery fee", "shipping cost",
             "airline fee", "cargo fee", "driver"],
            "Getting a dog to you genuinely costs money. The difference is who holds it: book and pay "
            + "the transporter yourself, directly, so there is a real company with a name and a "
            + "booking. Money sent to the seller to arrange transport is the single most common way "
            + "this fee turns into the scam."),

        new("health-certificate", "a health certificate for travel", FeeKind.Real,
            ["health certificate", "cvi", "certificate of veterinary inspection", "fit to fly", "vet certificate"],
            "A vet-issued health certificate really is required for a dog to fly or cross state lines, "
            + "and it really does cost money. It is issued by a named veterinary clinic — ask which "
            + "one, and call them. A certificate you are asked to pay the seller for, from a clinic "
            + "you cannot reach, is paperwork that does not exist; forged airline and cargo documents "
            + "are documented in these cases."),
    ];
}
