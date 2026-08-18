// The safety guide's content, as data.
//
// It used to live inside a modal, which meant the most valuable writing in the app had no
// URL: it could not be linked, shared, bookmarked, cited by a rescue, or indexed. The person
// who needs the escalating-fee section is searching "refundable crate deposit puppy" at 11pm
// with $350 already gone, and there was no page for them to land on.
//
// The modal is gone rather than kept alongside the page. Its one advantage was reading the
// guide without losing your search, and that turned out to be worth nothing: the search lives
// entirely in the query string, so Back restores it exactly.
//
// It is ONE page, /safe, with every section on it and an id per section; links point at
// /safe#<slug>. The eight-separate-pages version ranked each section for its own search, which
// is the better SEO answer, but it fragmented a guide that reads as a sequence — spot it, stop
// paying, recover — into eight pages that each had to re-explain where the reader was. The old
// /safe/<slug> URLs still resolve: they redirect to the anchor, so nothing already shared
// breaks and there is still exactly one canonical URL.

/**
 * What you can get back, by method. Three states, each always paired with a word — colour
 * never carries the meaning alone.
 *
 * The mechanism, because it is the opposite of most people's intuition: credit-card rights
 * (Reg Z) turn on *what you bought*, and cover goods "not delivered as agreed". Bank-transfer
 * and app rights (Reg E) turn on *who initiated the payment* — if you sent it yourself, you
 * authorised it, and the protection largely does not reach you however thoroughly you were
 * deceived.
 */
export const PAYMENTS = [
  {
    method: 'Credit card',
    state: 'good',
    verdict: 'Usually recoverable',
    detail: 'A puppy that never arrives is "goods not delivered as agreed", which US law treats as a '
      + 'billing error. Dispute it in writing to the billing-inquiries address on your statement '
      + 'within 60 days of the first statement showing the charge. While it is disputed you need not '
      + 'pay that amount, and they cannot report it delinquent.',
  },
  {
    method: 'Debit card',
    state: 'warning',
    verdict: 'Much weaker than credit',
    detail: 'Same piece of plastic, different rules. Debit falls under the bank-transfer regime, which '
      + 'protects you when someone else moves your money — not when you were persuaded to move it '
      + 'yourself. Report it anyway and immediately; some banks go beyond the legal minimum.',
  },
  {
    method: 'Zelle, Cash App, Venmo',
    state: 'critical',
    verdict: 'Rarely recoverable',
    detail: 'This is the trap. People refuse a wire because it feels risky, then pay by app believing '
      + 'it is protected. If you knowingly sent the money, it counts as authorised and the protection '
      + 'for "unauthorised" transfers does not apply. It is different if someone took over your '
      + 'account or stole your login — that is unauthorised, and you should dispute it.',
  },
  {
    method: 'PayPal',
    state: 'warning',
    verdict: 'Only if you pay for Goods and Services',
    detail: 'PayPal\'s own buyer protection covers Goods and Services payments, not "Friends and '
      + 'Family" — which is exactly what a scammer will ask for, framed as saving you the fee. '
      + 'Paying by card through PayPal keeps your card rights as well.',
  },
  {
    method: 'Western Union, MoneyGram',
    state: 'critical',
    verdict: 'Gone on collection',
    detail: 'These are the two named most often in pet-scam reports, and for a reason: the money can '
      + 'be collected in cash, anywhere, minutes after you send it, by someone showing a reference '
      + 'number. There is no account to trace and no transaction to reverse. Call the company '
      + 'immediately anyway — on the rare occasion a transfer has not been picked up yet, it can be '
      + 'stopped.',
  },
  {
    method: 'Wire transfer',
    state: 'critical',
    verdict: 'Minutes, then gone',
    detail: 'A wire can sometimes be recalled if you call the bank before it settles. After that it '
      + 'has been collected in cash and there is nothing to claw back. Speed is the entire reason '
      + 'scammers ask for it.',
  },
  {
    method: 'Gift cards',
    state: 'critical',
    verdict: 'Almost never',
    detail: 'Still worth calling the card issuer straight away and reading them the numbers — very '
      + 'occasionally an unspent balance can be frozen. No legitimate breeder has ever been paid in '
      + 'gift cards.',
  },
  {
    method: 'Crypto',
    state: 'critical',
    verdict: 'Irreversible',
    detail: 'There is no dispute process and no one to appeal to. A transfer cannot be undone by '
      + 'anybody, including the exchange you sent it from.',
  },
]

// Measured, not chosen by eye: at 12px the soft error badge came out at 4.01:1 against the
// light surface, under the 4.5 WCAG 1.4.3 asks for normal-size text — and it was carrying the
// four rows that matter most. Solid error measures 4.80 light / 5.15 dark; solid success fails
// the other way at 2.91 light. So soft, soft, solid, which also gives the irreversible methods
// the most visual weight.
export const PAYMENT_STYLE = {
  good: 'badge-soft badge-success',
  warning: 'badge-soft badge-warning',
  critical: 'badge-error',
}

// Split rather than one sentence because both renderers bold the first half, and doing that
// with a string split in two components is how the two quietly stop matching.
export const STANDING_RULE = {
  label: 'The one rule that beats every scam:',
  body: 'never send money for a puppy you (or someone you trust) haven\'t seen in person. '
    + 'Video calls are the minimum; in person is the standard.',
}

export const DISCLAIMER = 'PuppyFinder links to third-party sites and can\'t vet individual sellers '
  + '— these checks are yours to run.'

/**
 * The guide, in the order the decision actually happens: spot the scam → stop paying →
 * understand what your payment method can recover → vet → paperwork → fees → report.
 *
 * `slug` is the section's anchor (/safe#<slug>) and must not be renamed once shared. `summary`
 * is the one-line lede under its heading, so it has to state what the section decides, not
 * what it is "about" — on a page this long it is what someone skimming reads instead.
 */
export const SAFETY_SECTIONS = [
  {
    slug: 'red-flags',
    emoji: '🚩',
    title: 'Red flags that mean walk away',
    summary: 'The seven signs that a puppy listing is a scam — bargain pricing, untraceable payment '
      + 'methods, surprise fees after a deposit, refused video calls, and stock photography.',
    open: true,
    items: [
      'A price far below the typical range for the breed (our breed cards show typical ranges) — bargain purebreds are the classic scam bait.',
      'Payment by wire transfer, Western Union, MoneyGram, gift cards, Zelle, Venmo, or crypto to someone you have never met. No legitimate breeder asks for these.',
      'Any surprise fee after you pay — "shipping insurance", "climate-controlled crate", "vaccine deposit". This is the standard scam script; the puppy does not exist. If this is happening to you now, read the next section first.',
      'Seller refuses a live video call, or will only send pre-recorded clips. A refusal is still damning — but a call happening is no longer proof by itself (see below).',
      'Photos that look professional or stock-like. Reverse-image-search them: a hit is damning. A clean result no longer clears anyone, because an AI-generated photo appears nowhere else.',
      'Pressure and urgency: "three other families are coming today", "price goes up tomorrow".',
      'Many breeds always available from one seller, or puppies always "ready to ship today" — responsible breeders have waitlists, not inventory.',
    ],
  },
  {
    // Added August 2026, and the first thing in this guide aimed at someone who has already
    // lost money rather than someone deciding whether to. BBB's finding is that the scam is
    // profitable because of a "multi-tiered setup" that lets them "go back to a consumer several
    // times to ask for money" — so the loss accumulates across payments the app never saw.
    // Every fee, threat and figure below is from their published case material.
    slug: 'escalating-fees',
    emoji: '💸',
    title: 'They are asking for more money',
    summary: 'A breeder asking for a refundable crate deposit, shipping insurance, a permit or an '
      + 'emergency vet bill after your deposit means there is no puppy. Stop paying — here is why, '
      + 'and what to save before you go quiet.',
    // The rest of this section exists to explain why this one line is true, so it does not
    // belong in the same list at the same weight.
    lead: 'Stop paying. Once a second payment is requested after your deposit, there is no puppy — the requests continue until you stop, and nothing you send next arrives as a dog.',
    items: [
      'Recognise your fee here: a temperature-controlled or "special" crate, shipping insurance, a city or import permit, customs, microchipping, a vaccine, a quarantine or "release" fee once the dog is supposedly stuck at the airport, or a sudden emergency vet bill. These are the same inventions in report after report.',
      'Notice who is asking. After the deposit a second party usually appears — a "shipping company" that contacted you, with a website, a logo and paperwork. Presenting the money as somebody else\'s requirement is what makes it feel unavoidable rather than like a demand from the person you are already paying. In the documented cases they are the same people, and the transport company exists only as a website. Real pet shippers are listed in IPATA\'s member directory, which you look up yourself rather than through any link they send; no genuine shipping company has "IPATA" in its own name.',
      '"Refundable" is the tell, and crate deposits are the usual version. One reported sequence ran $350 refundable crate rental, then $299 shipping insurance, then a $499 city permit, then an $800 emergency vet bill.',
      'Official-looking documents prove nothing — airline emails and cargo paperwork in these cases are forged. One victim was shown a fake Delta Air Cargo notice to justify the crate.',
      'The threats are part of the script. People are told the dog will die and it will be their fault, or that they will be charged with animal abandonment for refusing. Neither happens. It is pressure to produce one more payment.',
      'Being invested is the trap, not a reason to continue. BBB puts it plainly: people feel bad and are invested in the pet in the photo, so they keep sending money. What you have already paid is gone whether or not you pay again.',
      'Before you go quiet, save everything: screenshots of the conversation, receipts, and every document and email they sent. The forgeries are evidence, and a bank dispute or police report needs them.',
      'Then work out what you can recover — the next section covers which payment methods can be reversed, and the last one covers where to report it.',
    ],
  },
  {
    // Second, after the red flags: the guide reads in the order the decision happens — spot the
    // scam, understand what your payment method can and can't recover, then vet, then paperwork,
    // then recover. The app already said which methods to avoid; it never said what you can get
    // back, and BBB documents a victim who refused a wire as too risky and then paid by Zelle
    // believing it was protected.
    slug: 'payments',
    emoji: '💳',
    title: 'What you can actually get back',
    summary: 'Which payment methods can be reversed after a puppy scam, and why credit cards are '
      + 'covered when Zelle, Venmo, wires and gift cards are not — the rule is the opposite of most '
      + 'people\'s intuition.',
    kind: 'payments',
    intro: 'The rule is the opposite of most people\'s intuition. Credit-card protection depends on '
      + 'what you bought, so a puppy that never arrives is covered. Bank transfers and payment apps '
      + 'depend on who moved the money — and if you sent it yourself, you authorised it, however '
      + 'thoroughly you were deceived.',
    note: 'General information, not legal advice, and it describes US rules. Whatever you paid '
      + 'with, report it — the FTC and IC3 links are in the last section.',
    items: [],
  },
  {
    // Added August 2026. "Have a video call" was this guide's central recommendation and BBB
    // now warns that advice "may be going away" because generated video can satisfy it. The
    // answer isn't to drop the call — it's to make it interactive on the buyer's terms, which
    // a pre-rendered or replayed video cannot survive.
    slug: 'video-call',
    emoji: '📹',
    title: 'Make the video call prove something',
    summary: 'AI-generated video can pass an ordinary video call. Six tests a scammer\'s footage '
      + 'cannot survive, because they have to be given as instructions during the call.',
    items: [
      'Name the test yourself, during the call: ask them to pick the puppy up, turn it over, and show its belly and paws. Generated and recycled footage cannot take instructions.',
      'Ask them to hold up something you choose on the spot — today\'s date on a handwritten note, a specific number of fingers, a spoon.',
      'Ask for one continuous pan from the puppy to the mother to the room, without cutting. Scam footage is short, tightly cropped, and never shows the surroundings.',
      'Watch for the tells of a replayed clip: no response to what you just asked, audio that does not match the mouth, a loop, or a "bad connection" the moment you make a specific request.',
      'Do it twice, days apart, and ask for something different each time. One good call can be staged or borrowed; two on your terms is much harder.',
      'Offer to collect the dog yourself — say you will fly or drive to them this week and take it home in your own car. This is the test a video call is only a substitute for, and it needs no analysis of anything: a real puppy can be picked up, and a seller who will not arrange it has answered the question.',
      'Best of all, still visit in person. Everything above exists because that is not always possible — it is a substitute, not an equal.',
    ],
  },
  {
    slug: 'vet-a-breeder',
    emoji: '✅',
    title: 'How to vet a breeder',
    summary: 'Six checks that separate a responsible breeder from a puppy mill or a broker: meeting '
      + 'the mother, health-test results, being interviewed yourself, and a take-back clause in writing.',
    items: [
      'Visit in person. See where the puppies actually live, and meet the mother — her temperament and condition tell you more than any listing.',
      'Ask for the parents\' health-test results (OFA, PennHIP, Embark), not just "vet checked". Reputable breeders volunteer these.',
      'Expect the breeder to interview YOU. Good breeders care where their puppies go; no questions asked is a bad sign.',
      'Get a written contract with a health guarantee and a take-back clause — responsible breeders take their dogs back at any age, no questions.',
      'Verify registration claims: AKC-registered litters can be confirmed with the AKC. "Registration papers available for extra cost" is a red flag.',
      'Ask for references — their vet, and families from previous litters — and actually call them.',
    ],
  },
  {
    slug: 'paperwork',
    emoji: '📋',
    title: 'What real paperwork looks like',
    summary: 'What genuine vaccination records, microchip details and age documentation look like — '
      + 'and the eight-week minimum that is law in most US states.',
    items: [
      'Vaccination and deworming records on a veterinary clinic\'s letterhead with dates, product names, and the vet\'s signature — not a handwritten list.',
      'Puppies must be at least 8 weeks old before going home (this is the law in most US states).',
      'A microchip number you can verify, or a written commitment about who registers it.',
      'For adoptions: spay/neuter status, known history, and behavioral notes from the shelter or foster.',
    ],
  },
  {
    slug: 'adoption-fees',
    emoji: '🤝',
    title: 'Adoption & rehoming fees',
    summary: 'What a legitimate shelter adoption fee covers, why a small rehoming fee on classifieds '
      + 'protects the animal, and why a four-figure "rehoming fee" is a sale wearing a costume.',
    items: [
      'Legitimate shelter and rescue adoption fees run roughly $50–$500 and include vaccinations, microchip, and usually spay/neuter — that is not "buying a dog", it is covering care costs.',
      'On classifieds (especially Craigslist), a small rehoming fee ($50–$200) is normal and actually protects the animal from being taken for free by bad actors.',
      'A four-figure "rehoming fee" is a sale wearing a costume — on Craigslist it also violates the site\'s own rules. Treat it with full breeder-level scrutiny or walk away.',
    ],
  },
  {
    slug: 'report',
    emoji: '🆘',
    title: 'If you were scammed',
    summary: 'Where to report a puppy scam — the FTC, the FBI\'s IC3, the site that carried the '
      + 'listing, and petscams.com — and what to do about the payment.',
    items: [
      'Report it to the FTC at reportfraud.ftc.gov and the FBI\'s IC3 at ic3.gov.',
      'Report the listing to the site it appeared on, and to petscams.com, which tracks fraudulent pet sellers.',
      'If you paid by card, dispute the charge with your bank immediately. Wire transfers and gift cards are usually unrecoverable — which is exactly why scammers insist on them.',
    ],
  },
]

/** The section with this slug, or null. An unknown slug scrolls nowhere rather than erroring. */
export function findSection(slug) {
  return SAFETY_SECTIONS.find((s) => s.slug === slug) ?? null
}

const GUIDE_TITLE = 'Buy & adopt a puppy safely'

/** The page's title and description. One page, so one of each. */
export const GUIDE_META = {
  heading: GUIDE_TITLE,
  title: `${GUIDE_TITLE} — PuppyFinder`,
  description: 'How to tell a real breeder from a scam, what to do when they ask for more '
    + 'money after your deposit, and which payment methods you can actually get money back from.',
}

/** Where a section lives: the one page, at its own anchor. */
export function safetyPath(slug) {
  return slug ? `/safe#${slug}` : '/safe'
}
