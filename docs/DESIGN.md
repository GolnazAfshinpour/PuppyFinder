# PuppyFinder Design System — "Golden Hour"

> One brand, two modes. Warm ground, one confident accent, photography does the
> emotional work, numbers do the trust work. If a change fights one of those
> four sentences, it's off-brand.

Adopted July 2026, based on two research tracks: a teardown of how the leading
pet brands design for trust + warmth (Petfinder, Good Dog, PuppySpot,
Adopt-a-Pet, Rover, Chewy, The Farmer's Dog) and a 2026 design-trend brief.
The shared finding: successful pet UIs use a **cream canvas + a single
saturated accent + real animal photography + numeric trust claims** — never
rainbows, script fonts, or paw-print wallpaper.

## 1. Color tokens

Defined once as the daisyUI themes `goldenhour` / `goldenhour-dark` in
`frontend/src/style.css`. **Always use semantic classes** (`bg-base-200`,
`text-primary`, `badge-secondary`) — never raw hex in components.

| Token | Light | Dark | Role |
|---|---|---|---|
| `base-200` | `#FAF6EF` cream | `#221A14` espresso | page canvas |
| `base-100` | `#FFFDF9` | `#2B211A` | card surfaces |
| `base-300` | `#EFE5D6` | `#3A2D22` | borders, image placeholders |
| `base-content` | `#2D2117` espresso ink | `#F2E8DC` | text (never pure black/white) |
| `primary` | `#C75B39` terracotta | `#D97A55` | THE accent: CTAs, links, active states |
| `secondary` | `#E8A33D` honey | `#E3AA52` | highlights, "buy" badges, alert card |
| `accent` | `#7C8B6F` sage | `#93A385` | "adopt" badges, quiet success |

Rule of one: terracotta is the only color that asks for attention on a screen.
Honey and sage support; they never compete.

## 2. Typography

| Level | Font | Class recipe | Usage |
|---|---|---|---|
| Hero (one per page) | Fraunces 600 | `font-display text-4xl sm:text-6xl leading-[1.1] tracking-tight` | the single headline (content pages step down to `text-3xl sm:text-4xl`) |
| Wordmark | Fraunces 600 | `font-display text-xl` | nav only |
| Section heading | Fraunces 600 | `font-display text-2xl font-semibold tracking-tight` | "Adoptable dogs right now" |
| Card title | Fraunces 600 | `font-display text-xl font-semibold` | dog names, site names |
| Body | Nunito Sans 400 | `text-sm` / `text-base` | everything else |
| Micro label | Nunito Sans 700 | `text-xs font-bold uppercase tracking-wide opacity-60` | filter group labels |

Fraunces = brand voice, used **only** for the hero, section headings, card
titles, and the wordmark — body text, labels, and controls stay Nunito Sans,
which is what keeps the serif special. Never larger than `text-6xl`
(3.75rem), and only the hero gets that size.

## 3. Shape, depth, motion

- Radii come from theme tokens: cards/modals `--radius-box` (1rem), inputs and
  buttons `--radius-field` (0.75rem), badges `--radius-selector` (1rem).
- Elevation: photo/content cards use `.card-lift` (warm-tinted shadow, 3px
  hover lift, gentle photo zoom). Nothing else casts strong shadows.
- The sticky nav is glass: `bg-base-200/80 backdrop-blur-md`.
- Motion is purposeful and short (≤ 0.4s ease); no bouncing, no parallax.

## 4. Iconography & imagery

- Icons are **Heroicons outline** (inline SVG, `stroke-width 1.8`), tinted
  `text-primary/80` or `text-secondary`. Emoji are allowed only as *content
  warmth* (🐶 in a tab label, 🐾 on the quiz button) — never as field icons,
  bullets, or in headings.
- Photos: 4:3 on listing cards (`aspect-[4/3]`, `object-cover`), `base-300`
  placeholder behind them, 🐶 fallback when a shelter has no photo. Dogs are
  never illustrated as humans and photos are never tinted.

## 5. Component conventions

- **Dog card:** photo with favorite heart top-right (base-100/85 blur circle,
  primary heart, `aria-pressed`) → Fraunces name (stretched link: the name's hit area is
  the whole card, other interactive elements sit above via `relative z-10`,
  card shows `focus-within` ring) + fit badge → ONE muted metadata line
  ("Male · Neutered · 2 Years · Medium") → breed → location → contact →
  one primary CTA ("Meet {name} ↗"). Max 1-2 badges per card, ever
  (Baymard/NN/g: badge piles kill 3-second scanning).
- **Dog detail (`?dog=<id>`):** photo `object-contain` on a fixed-height warm
  panel — shelter photos come in every aspect ratio and a fixed crop reliably
  decapitated the dog. Then name + meta line → age/breed badges → full bio →
  boxed shelter contact with the animal ID to quote → ONE outbound CTA. Cards link
  here rather than to the source site: ejecting at peak intent lost people to
  petharbor.com. Card links stay real `<a href>`s (middle-click works) with the
  click intercepted. Escape and backdrop close; body scroll locks; a dog that left
  the feed gets a "found a home" message, never an error.
- **Site card:** Fraunces name + kind badge → description → icon rows
  (vetting/price/delivery/best-for) → caution alert (if any) → link-depth
  badges → one primary CTA.
- **Price card (buying path):** photo (4:3, `sm:` and up) + label + the range as the one large
  number + a one-line provenance summary with "how we know →" → the **price meter** → quote
  input → verdict alert → two collapsed accordions. One card, not two: the verdict belongs *on*
  the price, the way CarGurus puts its deal rating on the listing. Everything secondary is
  deferred — the five price drivers behind an accordion were most of the card's bulk and none
  of its point.
- **Price meter:** a recessive `base-300` track, the sourced band in `primary/70` with rounded
  ends, a 2px `error/45` rule at the 0.5x far-below boundary (the rule that decides "scam" used
  to exist only inside a sentence), and the quote as a status-coloured dot with a 2px surface
  ring. Zone labels above, selective direct labels below — never a number on every tick.
  **Only one status colour is ever on screen**, so status hues never sit adjacent; the sub-3:1
  warning tone is legitimate only because the flag, headline and detail sentence always
  accompany it. `role="img"` with one spoken sentence, since the geometry is meaningless read
  out piecewise. Position maths lives in `frontend/src/priceMeter.js` so it is unit-testable
  without mounting.
- **Running text is capped at `max-w-prose`** (~65ch). Measured before this rule existed: 13 of
  13 prose blocks on the buying page ran 91–117 chars, past the 80 of WCAG 1.4.8, which Baymard
  finds readers experience as "intimidating and overwhelming".
- **The hero follows the mode.** "Buy a puppy. Don't get scammed." over a grid of rescue dogs
  contradicted the page under it; headline, subhead and chip row all switch on `goal`.
- **Results page rather than dump.** The grid reveals 24 at a time and the heading always
  states the true total. 53 cards in one scroll measured 10,539px. The honest-coverage rule
  applies to a "show more" button as much as to an empty state.
- **Saved dogs live behind one nav control, never a buried panel.** Measured before the
  change: 24 heart buttons invited saving on a single page while the saved list sat at 90% of
  a 5,625px page, collapsed; "recently viewed" rendered only in the empty-results branch, so it
  appeared exactly when you had found nothing. Both are now in one dialog opened from the
  sticky nav, reachable at any scroll position and in either mode. The control is hidden until
  there is something in it, shows the saved count, and carries the full meaning in its
  `aria-label` because the text collapses to an icon under `sm:`.
- **Badges:** `badge-soft` for data (sex, size), `badge-outline` for meta
  (trust chips), `badge-primary badge-soft` for interactive chips (removable
  filters, fit %). Text never wraps inside a badge (`whitespace-nowrap`).
- **Trust is numeric:** "14 sites", "56 live dogs", "$50 adoption fee" — no
  bare adjectives ("trusted", "best") without a number or mechanism nearby.

## 6. Voice

Second-person, warm verbs, contractions, clinical words only inside warm
sentences: "know exactly who to trust before any money changes hands."
Honesty beats marketing — cautions and coverage limits are stated plainly.

## 7. Search UX rules

- **Buying is the primary path** (owner decision, July 2026), so `buy` is the default
  goal and never appears in the URL. Buy mode runs in decision order: what the breed
  costs → is this quote sane → where to look → who to trust. Adoption stays a real
  secondary path, offered right after the price panels — the moment a four-figure
  range makes it most persuasive — never as a guilt trip.
- **A safety feature that can't be sourced is switched off, not caveated.** The price
  scam check returns `Unavailable` unless the breed's range came from independent cited
  sources. A caveat is the right tool for a *soft* claim ("size not listed"); it is the
  wrong tool for "this seller is defrauding you", where being wrong in either direction
  does real harm — a legitimate breeder accused, or a real scam waved through. The gate
  lives in `PriceCheck.Evaluate`, not just the UI, so no caller can bypass it, and it's
  keyed on data rather than a feature flag so each breed switches on by itself.
- **When a feature is off, replace it with something that works.** Price screening off
  doesn't mean a blank panel: the buy path shows the five checks that need no price at
  all (three quotes, live video, health testing on paper, never wire/gift-card/crypto,
  walk away from post-commitment fees).
- **Distance leads the location group, because location leads everything.** The filter order below
  puts Breed first, which contradicted the research already cited here — Adopt-a-Pet's 6.5M searches
  put distance ahead of breed, and Petfinder *opens* on a 50-mile radius rather than offering
  distance as a refinement. A ZIP + radius control now sits above State, with State kept underneath
  because "anywhere in Texas" is a real request that a circle cannot express. "Use my location"
  moved onto it from the State label: geolocation's actual product is coordinates, and attaching it
  to a state dropdown threw away the precision it had just obtained.
- **Distance reports before it filters.** An origin alone adds a mileage to every card and unlocks
  the nearest sort; only a radius removes anything. Both are useful separately, and a radius that
  silently discarded dogs would be the worse default. A dog whose rescue recorded no location stays
  in the results — the same "unknown is not no" rule size and age already follow.
- **Advice has to reach the person who already paid.** Every check in the app fired before the
  first payment — the price screen, the red flags, the video call, the paperwork. BBB's finding
  is that the scam is profitable because a "multi-tiered setup" lets the seller come back for
  money several times, so most of the loss lands on payments two, three and four, which nothing
  in the app addressed. The safety guide's second section is written for someone mid-scam, and
  its one instruction ("stop paying") is lifted out of the bullet list into a callout, because
  the other seven bullets exist only to explain why that one is true. Ordered before the
  payment-recourse section: whether to send the next payment outranks which method to have used.
  Every fee name, threat and figure in it comes from BBB's published case material rather than
  from reasoning about what a scam would plausibly look like — an invented fee name would make a
  victim conclude their situation is different.
- **Advice that can't be linked can't be found.** The safety guide's eight sections lived only
  inside a modal, which has no URL: they could not be shared, bookmarked, cited by a rescue, or
  indexed — and the person who needs the escalating-fee section is searching *"refundable crate
  deposit puppy"* at 11pm with $350 already gone, not browsing an adoption app. The guide is now
  **one page at `/safe`**, every section on it with an `id`, linked as `/safe#<slug>` and
  prerendered to real HTML at build time so a crawler sees the text rather than an empty `#app`.
  **The modal is deleted, not kept alongside it.** Its one advantage was reading the guide
  without losing your search, and that was worth nothing here: the search lives entirely in the
  query string, so Back restores it exactly — while keeping both would have left two homes for
  one body of writing and made every safety CTA in the app a dead end instead of a URL.
- **One page, not eight.** The sections were briefly eight separate pages, which is the better
  SEO answer — each ranks for its own search. One page won anyway, because the guide reads as a
  sequence (spot it → stop paying → recover) and eight pages fragmented it into eight arrivals
  that each had to re-establish where the reader was. The cost is real and was taken knowingly.
  The old `/safe/<slug>` URLs still resolve and rewrite themselves to `/safe#<slug>`, so nothing
  already shared breaks and nine URLs never serve the same content. Every safety button is a
  plain `<a href>` aimed at the section that answers the question it was asked from: a price
  verdict goes to `#red-flags`, a site's weak-vetting caution to `#vet-a-breeder`, the rest to
  the top. A footer on the app links all eight anchors, because a page nothing links to is an
  orphan however good it is — and the e2e suite asserts every footer anchor names a section that
  exists, since the link and the `id` live in different files. Content lives in
  `frontend/src/content/safety.js` — one copy, two renderers (page, prerender). Slugs are
  permanent once shared. Canonical tags and `sitemap.xml` are emitted only when `SITE_URL` is
  set: a canonical pointing at the wrong origin is worse than none, which is the same rule the
  price ranges follow.
- **Advice has to reach the person who has already paid — including as a check, not just prose.**
  The safety guide's escalating-fee section was written for someone mid-scam; the fee check is
  that section as something you can use. Every other check in the app fires before payment one
  (price, red flags, video call, paperwork), while BBB's finding is that a "multi-tiered setup"
  puts most of the loss on payments two, three and four. It takes what the seller is asking for
  plus one question — *have you already sent this seller money?* — and the second input is what
  decides the verdict, because "don't send it" and "stop paying" are different instructions to
  different people. Unanswered by default rather than defaulted to "not yet": guessing there
  would hand the calmer advice to the reader who least needs it.
- **The fee check needs no price range, which is the point.** Price screening is live for 50 of
  174 breeds; an invented crate fee is the same invented crate fee whatever the breed. It is a
  separate endpoint rather than a branch of `/api/price-check` for exactly that reason.
- **A fee catalog has to hold the real costs too.** A tool that only knew scam fees would answer
  "that's a scam" to a legitimate deposit — and being wrong in that direction is not the safe
  side of the error: it costs someone the dog they were going to get and teaches them to
  distrust the next warning. Real entries (deposits, transport, health certificates, adoption
  fees) are in the same catalog, and none of them returns an all-clear — "that this cost is real
  does not make this request safe" is the same rule the price check follows for a plausible
  price. Invented fees outrank real ones on match, since "shipping cost for the crate" is a
  request for crate money.
- **The scam has two actors, so the check asks who is speaking.** BBB's script is specific: after
  the first payment the buyer is contacted again by someone posing as a shipping company, and it
  is that second party who demands the crate, the insurance and the permit. Presenting the money
  as somebody else's requirement is what makes it feel unavoidable rather than like a demand from
  the person you are already paying. A transport company that made contact on its own is
  therefore the finding whatever the fee is called — including one the catalog has never seen.
  The question distinguishes it from *a transporter I found and booked*, because that one is a
  real company sending a real invoice and conflating them would call every legitimate pet shipper
  a scammer.
- **A verdict ends in things to go and do.** The detail explains; the actions are separate, because
  burying a test that settles the question inside a paragraph is how it gets skipped. The best of
  them was missing from the app entirely: **offer to collect the dog yourself.** A real puppy can
  be picked up, and a seller who will not arrange it has answered the question without any
  analysis of the fee. The others are equally cheap — paste a sentence of their email into a
  search engine (fake transport sites are real ones with the name swapped), and look the shipper
  up in IPATA's directory rather than through a link or logo they sent, since no genuine shipping
  company has "IPATA" in its own name.
- **"We don't recognise it" must not read as "it's fine".** The catalog lists fees people have
  already reported, and a scammer renames one for free. Once money has moved, an unrecognised
  fee still warns — the sequence decides, not the name.
- **One check ends in a database rather than in advice, and it is worth more than the rest.**
  Everything else here tells a buyer what to look for; the seller check tells them a fact they can
  verify against USDA APHIS. A licence is required when a breeder keeps more than four breeding
  females *and* sells sight-unseen, and the load-bearing line is that **a puppy shipped to you is
  not a face-to-face sale** — so a shipper's exemption claim can only be the four-females one,
  which is testable against their own advertising. Only the sight-unseen-and-won't-answer pairing
  warns, because both honest answers take one sentence to give and there is no innocent silence.
- **A licence is a floor, and its absence is not an accusation.** Both errors are stated in the
  verdicts rather than left to inference: holding one means minimum standards enforced by
  unannounced inspection and says nothing about quality, while a small breeder selling
  face-to-face needs none at all — that is most good hobby breeders, and a warning there would
  push buyers away from exactly the sellers this app wants them using. The in-person branch says
  plainly that the check doesn't apply and redirects to what does (the mother, the conditions,
  health tests on paper).
- **A per-breed scam risk score was researched and rejected.** BBB's "Yorkies, Dachshunds and
  French Bulldogs are ~30% of puppy scams" is 2022 data, and their 2025 update drops Yorkies for
  German Shepherds and reports raw counts with no denominator. Without a popularity baseline the
  figure mostly restates which breeds are in demand, over 40 breeds appear in reports, and a
  badge on three breeds would function as an all-clear on the other 172 — which BBB explicitly
  contradicts. Not built; the bait breeds stay as guidance in the red-flags section, where they
  are context rather than a per-breed verdict.
- **A safety check must not read as an all-clear.** The price check says out loud that
  a believable price proves nothing, because scammers price realistically. Warnings
  use `alert-error`; everything else stays calm, since a page that alarms at every
  number trains people to ignore it.
- **The page is dogs, not websites** (adoption path). A search returns listings; the 14-site
  directory sits below them as the fallback for everywhere our feeds don't reach.
  Every primary CTA on the default screen used to be an exit link — that inverted
  the product into a directory of search engines.
- Filter groups ordered by decision-relevance (Baymard/NN/g): Goal → Breed →
  Age → State → City → Size → breed-list narrowing. Goal leads because it isn't a
  refiner — it decides whether the page shows adoptable dogs or breeder
  marketplaces. Refiners never above decision-drivers.
- Filter priority follows observed adopter behaviour (Adopt-a-Pet, 6.5M searches,
  Mar 2024): distance/location > breed (39% of searches) > age > size (16%) >
  sex > good-with > colour/coat (last; never volunteered by users).
- **A filter must filter the results.** Never ship one that quietly does something
  else — the breed-list narrowers are labelled "Narrow the breed list", not
  "Must-haves", because that is all they do.
- Options carry availability context where cheap ("MD · 42 live dogs").
- Active filters render as removable chips above results + "Clear all"
  (clear-all stays a quiet text link, physically apart from primary CTAs).
- Desktop filters update live; the sidebar is sticky with internal scroll
  (`max-h` + `overflow-y-auto`) so an expanded panel never clips.
- Mobile: filters collapse behind a toggle; results stay above the fold.
- Loading = skeleton cards matching the real layout, never spinners.
- **Missing data is "unknown", not "no".** Industry-wide only a fraction of shelter
  listings have complete profiles, so a filter that drops every blank field deletes
  most of the inventory and the user concludes there are no dogs. Unknowns stay in,
  labelled on the card, ranked below confirmed matches, with a "show only confirmed
  matches" opt-out. (Live example: `size=Small` returns 6 dogs, 5 with no size on
  file — the old hard-drop returned 1.)
- Zero results auto-broaden: relax one constraint at a time (city → size → age →
  breed → state → all), state exactly what was relaxed, offer one-tap clear
  and the saved-search alert. The user should never have to rework a failed
  search themselves. Broadened results never claim to be matches — the heading
  reads "Showing N dogs", not "N adoptable dogs".
- **One handoff, not fourteen.** Where our feeds fall short, recommend a single
  site, ranked by no-caution first, then by how much of the user's search its URL
  actually carries. "Open all 14 sites" handed the work straight back to the user.
- Multi-session memory is anonymous: favorites + recently-viewed live in
  localStorage as listing snapshots (they must survive dogs leaving the feed).
- The smart search box parses free text into these same filters (dictionary
  matching only — never a model that can hallucinate inventory); contradictions
  resolve toward the more specific intent with an explanatory hint. "Near me"
  geolocates to state+city with a manual fallback on denial.
- **Adoption fee and good-with are on the listing now, and both are sparse.** These were the top
  two gaps on this list: 81% of adopters rank the fee the most important item on a profile and
  the app showed it nowhere, and real "good with kids/dogs/cats" fields carry +56% adoption
  likelihood. Live coverage is fee 24%, dogs 41%, kids 25%, cats 21% — so the honest handling
  matters more than the fields. The fee is a badge on the card and the detail view when the
  rescue published one; when they didn't, the detail view says so and tells the reader what to
  ask, rather than leaving a silent gap. Good-with is **three states, never two** (yes / no /
  the rescue didn't say): the feed omits null attributes entirely, so a blank is genuinely
  unknown and rendering it as "no" would rule a dog out over an empty field — the same mistake
  size and age were fixed for. Negatives are stated plainly rather than hidden, because someone
  with a cat needs "not good with cats" most of all. The card stays silent when nothing was
  recorded; "not recorded" on three quarters of a grid is noise, not honesty.
- **Fees are normalised, not parsed away.** Rescues type the field by hand — one page returned
  "$175.00", "175.00", "375", "795", "150.00" and "500". A bare amount is formatted to "$175";
  anything else passes through verbatim, because "$300-$450", "Varies" and "Waived for seniors"
  are real answers a number cannot hold. Hand-typed blanks ("n/a", "none", "TBD") and a bare
  zero become null: "Adoption fee $0" is a claim on the rescue's behalf we cannot back, and an
  unedited numeric field defaulting to zero is the likelier explanation.
- **Good-with filters the dogs; the breed narrowers filter the breed list.** Two controls that
  sound alike doing different jobs, so each says which: "Good with — from each rescue's own
  listing" sits above "Narrow the breed list", and the breed one was renamed to **"Kid-friendly
  breeds"**. That rename is not cosmetic — with both set, the removable chips above the results
  read "Good with kids" twice, meaning two different things. The good-with group is hidden in buy
  mode, where there are no listings for it to narrow, which keeps "a filter must filter the
  results" true in both modes.
- **One asymmetry, and it is deliberate: `false` is not `unknown`.** Every other filter treats a
  blank as "keep it, label it" and lets `includeUnlisted=false` opt out. Good-with keeps that for
  blanks — it has to, since 59-79% of listings record nothing and a hard filter would delete most
  of the inventory — but a rescue that wrote down **"not good with kids" is dropped
  unconditionally**, and the strict/loose toggle cannot reach it. Someone filtering on this has a
  child or a cat in the house; that one recorded fact is the thing in this dataset a convenience
  toggle must never override. Measured live: "good with cats" takes 350 dogs to 287 by removing
  63 explicit noes, keeps 253 unrecorded ones labelled, and strict mode lands on 34 confirmed. A
  naive hard filter would have shown 34.
- **The unconfirmed banner names the field it actually filtered on.** It was hardcoded to
  size/age with age as the fallback, so filtering on "good with cats" alone produced "didn't have
  a age listed" — wrong field and wrong grammar on the one sentence whose whole job is explaining
  why those dogs are in the list.
- **The breed control is a typeahead, and the reason is not polish.** A native `<select>` only
  jumps to names *beginning* with what you type, so "retriever" matched nothing at all and
  "shepherd" missed Australian Shepherd — nobody files a Labrador Retriever under L. The picker
  matches anywhere in the name, ranks exact → starts-with → word-starts-with → contains, is fully
  keyboard operable (arrows, Enter, Escape) with the ARIA combobox roles, clears in one click
  instead of a scroll back to "Any breed", and says "No breeds match" rather than showing an
  empty box. Ranking lives in `frontend/src/breedSearch.js` so it is testable without mounting,
  the same reason `priceMeter.js` exists.
- **It also found a duplicate breed.** "Shepherd German" and "German Shepherd" were both in the
  catalog — the same bug `DuplicateOfCurated` exists to fix, and one the alphabetical `<select>`
  hid by putting them 170 rows apart. It mattered: German Shepherd is one of the three breeds
  BBB names as most used in scams, and the curated entry carries a $2,000 range the duplicate had
  no access to, so the duplicate bypassed the price floor guard entirely. The e2e suite now
  asserts no two breeds share a word set, so the next one fails a test instead of waiting for a
  UI change to expose it.
- Known gaps (documented, not yet built): per-option result counts, good-with in
  saved-search alerts (the filter is search-only), and a compare view for saved dogs.
