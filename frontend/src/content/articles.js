// The scam-guide landing pages, as data — one copy, two renderers (ArticlePage, prerender),
// the same shape the safety guide established.
//
// Why these four pages exist: SERP research (Aug 2026) found no interactive tool ranking for
// any of these searches — only advice articles, and for "teacup puppy scam" barely those. An
// AI answer box can summarise an article; it can't run a check. Each page pairs the writing
// with the tool that answers its question, which is the part a search engine can't replace.
//
// Every figure below is cited in the page's sources list. The rule is the price pipeline's:
// no number without a named, linkable source — an invented statistic on a scam page would make
// a victim conclude their situation is different.

/**
 * Block kinds:
 *  h2      { text }
 *  p       { text }
 *  list    { items: string[] }
 *  callout { tone: 'warning'|'info', text }         — the one-sentence instruction
 *  tool    { tool: 'price-check'|'fee-check'|'seller-check', lead }
 */
export const ARTICLES = [
  {
    slug: 'puppy-price-check',
    meta: {
      title: 'Is This Puppy Price a Scam? Check It Against Real Listings',
      description: 'Paste the price you were quoted and check it against the middle half of '
        + 'real asking prices for the breed — sourced from live listings, not estimates.',
    },
    h1: 'Is this puppy price a scam?',
    lede: 'A too-good price is the single strongest warning sign of a puppy scam — and the '
      + 'easiest one to check. This page compares your quote against the middle half of real '
      + 'asking prices for the breed, collected from live marketplace listings.',
    blocks: [
      {
        kind: 'tool',
        tool: 'price-check',
        lead: 'Pick the breed, type the price you were quoted, and see where it lands.',
      },
      { kind: 'h2', text: 'Why the price is the tell' },
      {
        kind: 'p',
        text: 'Scammers advertise below market because the discount is the hook. The Better '
          + 'Business Bureau\'s study of thousands of reports found French Bulldogs — a breed '
          + 'that typically costs around $3,000 from a breeder — advertised by scammers for '
          + 'less than $1,000. The victims in the largest academic study of pet scams often '
          + 'noticed the price was low, and talked themselves past it.',
      },
      {
        kind: 'p',
        text: 'The average reported loss reached $1,293 in 2025, up 34% in a year — because the '
          + 'advertised price is only the first payment. The cheap puppy is the entry fee; the '
          + 'shipping, insurance and permit demands that follow are where the money goes.',
      },
      { kind: 'h2', text: 'How our ranges are built' },
      {
        kind: 'p',
        text: 'Each range is the middle half of real asking prices collected from live '
          + 'marketplace listings within the last 90 days, with crossbreeds excluded and the '
          + 'cheap scam tail deliberately kept out of the benchmark. Breeds without enough '
          + 'listings get no range at all — we say "unavailable" rather than guessing. Every '
          + 'range states its sample size and where it came from.',
      },
      {
        kind: 'callout',
        tone: 'warning',
        text: 'A believable price proves nothing on its own — plenty of scammers price '
          + 'realistically. A far-below-market price, though, is close to diagnostic. Treat '
          + 'this check as one-directional.',
      },
      { kind: 'h2', text: 'If the price passes, check these next' },
      {
        kind: 'list',
        items: [
          'Payment method. The FTC\'s rule of thumb: "only scammers say you must pay with '
            + 'gift cards, a payment app, cryptocurrency, or a wire transfer service."',
          'Live video, with a challenge. Ask to see the puppy on a live call doing something '
            + 'you choose on the spot — recorded clips and AI video are now used to fake this.',
          'Reverse-search the photos and a sentence of the ad. Scam listings reuse both '
            + 'across many sites.',
          'Offer to pick the puppy up yourself. A real puppy can be collected; a seller who '
            + 'will not arrange it has answered the question.',
        ],
      },
    ],
    related: ['teacup-puppy-scam', 'puppy-shipping-fee-scam'],
    safeAnchors: ['red-flags', 'video-call'],
    sources: [
      {
        name: 'BBB puppy scam study (full study)',
        url: 'https://www.bbb.org/all/scamstudies/puppy-scams/puppy-scams-full-study',
      },
      {
        name: 'BBB 2025 study update',
        url: 'https://www.bbb.org/all/scamstudies/puppy-scams/2025-study-update-puppy-scams',
      },
      {
        name: 'FTC: Getting a pet? How to avoid scams',
        url: 'https://consumer.ftc.gov/consumer-alerts/2024/12/getting-pet-avoid-scams',
      },
      {
        name: 'Whittaker & Button, "Understanding pet scams" (ANZ Journal of Criminology, 2020)',
        url: 'https://journals.sagepub.com/doi/full/10.1177/0004865820957077',
      },
    ],
  },

  {
    slug: 'puppy-shipping-fee-scam',
    meta: {
      title: 'Puppy Shipping Fee Scams: Check What They\'re Asking You to Pay',
      description: 'Crate rental, shipping insurance, a "refundable" deposit, a permit — check '
        + 'the fee you\'ve been asked for against the fees documented in real scam reports.',
    },
    h1: 'The puppy shipping fee that keeps growing',
    lede: 'You paid for the puppy. Now a shipping company says the crate is the wrong size, or '
      + 'the insurance is mandatory, or a permit is missing — and every fee is "refundable on '
      + 'delivery". This is the documented script of the puppy shipping scam, and the fee\'s '
      + 'name is the least important part of it.',
    blocks: [
      {
        kind: 'tool',
        tool: 'fee-check',
        lead: 'Type what they\'re asking money for. The check compares it against the fees '
          + 'documented in published scam reports — and real costs are in the same catalog, so '
          + 'a legitimate charge is never called a scam.',
      },
      { kind: 'h2', text: 'The script, as the reports describe it' },
      {
        kind: 'p',
        text: 'The BBB calls it a "multi-tiered setup": after your first payment, a second '
          + 'party appears — a shipping or transport company, complete with a website and a '
          + 'tracking number — and from then on every demand comes from them. IPATA, the real '
          + 'pet shippers\' association, documents the sequence: a climate-controlled crate, '
          + 'shipping insurance, vaccination paperwork, customs clearance — each $100 to '
          + '$2,000, each "the last one", continuing until you stop responding.',
      },
      {
        kind: 'list',
        items: [
          'The word "refundable" is a signature. Real shippers do not take deposits they '
            + 'promise back on delivery.',
          'Customs fees on a shipment inside your own country are logically impossible — '
            + 'an automatic red flag.',
          'A transporter who contacted you is the finding, whatever the fee is called. A '
            + 'transporter you found and booked yourself is a real company sending a real '
            + 'invoice.',
          'No genuine shipping company has "IPATA" in its name — scammers impersonate the '
            + 'association itself. Look the shipper up in IPATA\'s own member directory, '
            + 'never through a link or logo they sent you.',
        ],
      },
      {
        kind: 'callout',
        tone: 'warning',
        text: 'Once money has moved, any new fee is the pattern — whatever it is called and '
          + 'however official the invoice looks. The name changes; the sequence doesn\'t.',
      },
      { kind: 'h2', text: 'The test that settles it' },
      {
        kind: 'p',
        text: 'Offer to collect the puppy yourself, today, at their address. A real puppy can '
          + 'be picked up. A seller or shipper who will not arrange it — any excuse — has '
          + 'answered the question without any analysis of the fee.',
      },
    ],
    related: ['paid-deposit-puppy-scam', 'puppy-price-check'],
    safeAnchors: ['escalating-fees', 'payments'],
    sources: [
      {
        name: 'IPATA: current pet scams and how they work',
        url: 'https://www.ipata.org/pet-scams',
      },
      {
        name: 'BBB puppy scam study (the "multi-tiered setup")',
        url: 'https://www.bbb.org/all/scamstudies/puppy-scams/puppy-scams-full-study',
      },
      {
        name: 'FTC: Getting a pet? How to avoid scams',
        url: 'https://consumer.ftc.gov/consumer-alerts/2024/12/getting-pet-avoid-scams',
      },
    ],
  },

  {
    slug: 'paid-deposit-puppy-scam',
    meta: {
      title: 'Paid a Deposit for a Puppy and Now They Want More? Do This',
      description: 'Sent a deposit for a puppy and now they want more money? The one '
        + 'instruction that matters, what you can get back by payment method, and where to '
        + 'report it.',
    },
    h1: 'You paid a deposit. Now they want more.',
    lede: 'If you are reading this with money already sent and a new demand on your screen, '
      + 'you are at the exact point where most of the loss happens — the reports show the '
      + 'money is made on payments two, three and four, not the first one. One instruction '
      + 'matters more than everything else on this page.',
    blocks: [
      {
        kind: 'callout',
        tone: 'warning',
        text: 'Stop paying. Every further fee — however small, however "refundable", however '
          + 'official the demand — goes to the same place the deposit went. No payment you '
          + 'send from here brings the puppy closer.',
      },
      {
        kind: 'tool',
        tool: 'fee-check',
        lead: 'Check the fee they\'re asking for now. Tell the check you\'ve already paid — '
          + 'the advice changes when money has moved.',
      },
      { kind: 'h2', text: 'Why stopping is safe' },
      {
        kind: 'p',
        text: 'The threat that keeps people paying is usually the last step of the script: '
          + 'pay or the puppy is abandoned, or you\'ll be charged with animal abandonment. '
          + 'State attorneys general have documented this exact threat as part of the scam. '
          + 'There is no puppy in a crate at the airport. Nothing bad happens when you stop — '
          + 'except to the scammer\'s revenue.',
      },
      { kind: 'h2', text: 'What you can get back depends on how you paid' },
      {
        kind: 'list',
        items: [
          'Credit card: usually recoverable. A puppy that never arrives is "goods not '
            + 'delivered as agreed" — dispute it in writing within 60 days of the statement.',
          'Debit card: weaker, but report it immediately — some banks go beyond the legal '
            + 'minimum.',
          'Zelle, Cash App, Venmo: rarely recoverable when you sent it yourself, because '
            + '"authorised" payments fall outside the fraud protection. Report anyway.',
          'Wire, gift cards, crypto: effectively gone — which is why they were demanded. '
            + 'Report it regardless; reports are how these operations get mapped.',
        ],
      },
      { kind: 'h2', text: 'Report it — five minutes, three places' },
      {
        kind: 'list',
        items: [
          'FTC at ReportFraud.ftc.gov — the report feeds the database law enforcement uses.',
          'BBB Scam Tracker — their puppy-scam studies are built from these reports.',
          'Petscams.com — they catalogue and take down fraudulent pet-seller sites.',
        ],
      },
      {
        kind: 'p',
        text: 'Under 5% of victims report. The figures on every page like this one exist '
          + 'because the few who did — your report is what warns the next person searching '
          + 'the same fee name at 11pm.',
      },
    ],
    related: ['puppy-shipping-fee-scam', 'puppy-price-check'],
    safeAnchors: ['escalating-fees', 'payments', 'report'],
    sources: [
      {
        name: 'BBB 2025 study update (loss figures, reporting rate)',
        url: 'https://www.bbb.org/all/scamstudies/puppy-scams/2025-study-update-puppy-scams',
      },
      {
        name: 'Michigan Attorney General: puppy scam consumer alert (the abandonment threat)',
        url: 'https://www.michigan.gov/consumerprotection/protect-yourself/consumer-alerts/scams/puppy-scams',
      },
      {
        name: 'IPATA: current pet scams',
        url: 'https://www.ipata.org/pet-scams',
      },
      {
        name: 'FTC: ReportFraud.ftc.gov',
        url: 'https://reportfraud.ftc.gov/',
      },
    ],
  },

  {
    slug: 'teacup-puppy-scam',
    meta: {
      title: 'Teacup Puppy Scams: Why "Teacup" Is the Word to Watch',
      description: 'No kennel club recognises "teacup" as a breed or a size — it\'s a '
        + 'marketing word that attracts scammers. How to check a teacup listing before any '
        + 'money moves.',
    },
    h1: 'The word "teacup" is doing a lot of work',
    lede: 'No kennel club recognises "teacup" as a breed or a size class. It is a marketing '
      + 'word — and because it commands four-figure prices for tiny puppies that photograph '
      + 'beautifully, it attracts two different problems: outright scams that never ship a '
      + 'puppy, and real breeding for extreme smallness that ships a sick one.',
    blocks: [
      { kind: 'h2', text: 'Problem one: the puppy that doesn\'t exist' },
      {
        kind: 'p',
        text: 'Tiny breeds are the scammer\'s favourite inventory: Yorkies, Dachshunds and '
          + 'French Bulldogs alone account for roughly 30% of tracked puppy scams, and '
          + '"teacup" versions of them are the premium bait. The photos are lifted or '
          + 'AI-generated, the price is a little too good, and the shipping fees begin after '
          + 'the deposit.',
      },
      {
        kind: 'tool',
        tool: 'price-check',
        lead: 'Check the quoted price against real asking prices for the base breed — a '
          + '"teacup" discount on an already-tiny breed is the classic hook.',
      },
      { kind: 'h2', text: 'Problem two: the puppy that exists and shouldn\'t' },
      {
        kind: 'p',
        text: 'When the puppy is real, "teacup" often means bred for extreme smallness — or '
          + 'simply sold weeks too young so it photographs smaller. No kennel club recognises '
          + 'the label, and dogs bred for extreme smallness carry well-documented risks — '
          + 'hypoglycaemia, heart defects, fragile bones. A seller using the label as a '
          + 'premium is telling you how they breed.',
      },
      {
        kind: 'list',
        items: [
          'Ask the puppy\'s exact age and weight, and the parents\' weights. A reputable '
            + 'breeder answers precisely; "teacup" sellers tend to answer in adjectives.',
          'No puppy should leave its mother before 8 weeks — in many US states that is the '
            + 'law, not a preference.',
          'Insist on a live video call with the puppy and its mother in the same shot, '
            + 'doing something you choose on the spot.',
          'Then run the same checks as any purchase: payment method, pickup offer, breeder '
            + 'licence if they ship sight-unseen.',
        ],
      },
      {
        kind: 'callout',
        tone: 'info',
        text: 'A rescue is the honest way to get a very small dog: tiny adult dogs are common '
          + 'in shelters, their adult size is a fact rather than a promise, and the adoption '
          + 'fee is a fraction of a "teacup" price.',
      },
    ],
    related: ['puppy-price-check', 'puppy-shipping-fee-scam'],
    safeAnchors: ['red-flags', 'video-call', 'vet-a-breeder'],
    sources: [
      {
        name: 'BBB 2025 study update (bait breeds)',
        url: 'https://www.bbb.org/all/scamstudies/puppy-scams/2025-study-update-puppy-scams',
      },
      {
        name: 'AKC: how to spot a puppy scam',
        url: 'https://www.akc.org/expert-advice/puppy-information/spot-puppy-scam/',
      },
      {
        name: 'FTC: Getting a pet? How to avoid scams',
        url: 'https://consumer.ftc.gov/consumer-alerts/2024/12/getting-pet-avoid-scams',
      },
    ],
  },
]

// The rescue-widget pitch page's head, shared by EmbedPage and the prerender so the static
// HTML and the SPA can't describe the page differently — same rule as GUIDE_META.
export const EMBED_META = {
  title: 'Free Puppy-Scam Check Widget for Rescues & Shelters',
  description: 'Add PuppyFinder\'s fee scam check to your rescue or shelter website with one '
    + 'iframe — free, no signup, no tracking, built from published BBB and IPATA scam reports.',
}

export function findArticle(slug) {
  return ARTICLES.find((a) => a.slug === slug) ?? null
}

export function articlePath(slug) {
  return `/${slug}`
}
