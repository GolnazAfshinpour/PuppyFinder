# How the breed price search is designed

The scam check compares a quoted puppy price against the breed's typical range. That check
is only as good as the range behind it, so this document covers **how the range is found,
how it is judged, and what the search refuses to do** — the design, not the source research.

- Source-by-source research, terms analysis and the measured results: [SOURCES.md](SOURCES.md)
- Product decisions and UX: [DESIGN.md](DESIGN.md)

## The problem the design exists to solve

The first version of this feature had 25 hardcoded price ranges with **no citation
anywhere**, labelled "verified" in four places, with fraud detection built on top. They were
almost certainly model-generated from training data.

So the requirement is not "find a number". It is: **every published range must be able to say
where it came from, and refuse to exist when it can't.** Every rule below follows from that.

Two consequences shape everything:

1. **`PriceCheck.CanScreen` gates on `confidence == verified`.** Screening is a property of
   the data, not a feature flag — a breed starts being checked the moment its range is
   properly sourced, and there is no switch to flip by accident. 129 of 175 breeds currently
   return `Unavailable`, and that is the system working.
2. **Gathering and judging are separate steps.** The collectors only ever write evidence
   rows; confidence is a *pure function* over them (`PriceObservationValidator.Aggregate`,
   `ListingPriceAggregator.Aggregate`). Re-tuning the bar is therefore a re-aggregation —
   seconds, free, no network. That split has paid for itself repeatedly: every threshold
   below could be changed and re-applied without re-collecting anything.

## Two kinds of evidence, deliberately not merged

| | Editorial | Listings |
|---|---|---|
| What it is | A range published in an article ("$2,500–$4,000 for a standard Frenchie") | The asking price on one animal for sale |
| Where | Insurance, financial, veterinary publishers + breed-content sites | One marketplace's structured data |
| Quantity | 2–5 per breed | 35–49 per breed |
| Provenance | Publisher, URL, verbatim quote, publication date | Sample size, host, median, span, retrieval date |
| Aggregated by | Median of lows / median of highs | 25th–75th percentile |
| Recorded in | `price_observation` | `listing_price` |

They are stored in separate tables because they are *different questions*, not two grades of
the same answer. An editorial figure needs a scope, a tier and a quote; a listing has none of
those and needs an identity for deduplication instead. Forcing both into one table would mean
most columns are null most of the time and the aggregation rules would have to branch anyway.

`breed_price.basis` records which one produced the live range. Without it, "49 sources" means
49 articles in one case and 49 puppies for sale in the other — and the UI cannot phrase the
provenance line without knowing which.

---

## Search design 1 — published figures

Run by `PriceResearchService` (Claude with web search) or entered by hand through
`POST /api/admin/price-observations`. **Both paths run through the same parser and the same
validator**, so a hand-entered row faces identical rules; only `model`/`run_id` differ.

### The prompt is an extractor, not an estimator

`PriceResearchPrompt.SystemRules` states this as the one rule that matters: report only
figures a source actually publishes, with the exact words that support them. Never estimate,
never average sources together, never fill a gap from training knowledge. **An empty result
is a correct answer** — a breed with no published pricing must stay unpriced rather than
receive a plausible-looking guess.

Enforced structurally, not just asked for: `source_url` and `quote` are required schema
fields, and a row without a verbatim quote containing the figure is rejected. In practice
this bites often — several pages state prices only in tables or headings with no sentence
containing the number (Hepper on German Shepherd and Golden Retriever, Dogster on Beagle and
Poodle). Those are skipped rather than quoted from an adjacent sentence that doesn't support
the figure.

### Searching a reviewed list, not the open web

`PriceSources` holds a version-controlled allowlist in two tiers. Tier A is editorially
accountable (named editors, corrections policy); Tier B does real research but runs on
affiliate revenue. **Tier B alone can never reach `verified`.**

Excluded as price authority, and this matters: anyone *selling* the breed, and the
classifieds the app warns users about. Their listing prices are *what the scam check screens
against* — letting them set the editorial floor would drag it down and quietly disarm the
feature. (Listings are used, but through a separate path with its own guard; see below.)

Tier is **re-derived from the URL on every read**, never trusted from the stored row, so a
row written by an older build or a mislabelled response can't grant itself Tier A standing.

### Scope normalization — the rule that makes this work

Raw figures look wildly inconsistent until you notice most of the disagreement is *conflated
scope*. For French Bulldog: $1,500–4,500 was pet-quality standard colour, $5,000–10,000 was
rare colours, ~$5,000 was an average folding both together. Three different questions
reported as one.

Every figure is tagged, and **only `pet_standard` feeds the published range**:

| Scope | Handling |
|---|---|
| `pet_standard` | Pet-quality, standard colour, reputable breeder, national. **The only aggregated scope.** |
| `show_or_pedigree` | Show prospect, champion lines, breeding rights |
| `rare_colour` | Merle/lilac/blue premiums |
| `regional` | Explicitly scoped to a region — real data on the wrong axis |
| `rescue` | Adoption fee. Recorded for context, never mixed in |
| `unscoped` | The source didn't say. **Recorded, never aggregated — that's the point.** |

A source giving one undifferentiated number gets `unscoped` and doesn't count. 28 of 59
stored observations are `unscoped`, contributing nothing. That is not waste; it is the
pipeline refusing to guess.

### `figure_kind` — averages corroborate, never widen

Tier A publishers often give an average, not a band ("about $5,000"). Requiring a low+high
silently discarded that good data. So averages are kept: one falling **inside** the
aggregated range counts as a corroborating source; one falling **outside** forces
`contested`. That is how genuine disagreement surfaces instead of being averaged away —
Insurify's $5,000 against MetLife's $2,500–$4,000 is real conflict, and this rule shows it.

### Independence, in two directions

Both were needed, and each was found by real data:

- **One page is one voice.** Insurify's Frenchie page states both "around $5,000 … on
  average" and a $2,000–$8,000 table range, both pet-quality. Counting rows let a single
  editorial voice supply two of the three sources needed to unlock screening; three pages
  from one domain could have cleared the bar outright. `CollapseByPublisher` allows one vote
  per host — a range beats an average, and the widest range wins, because a too-wide band
  makes the check quieter while a too-narrow one accuses honest breeders.
- **One figure syndicated is one source.** PetMD and A-Z Animals both give Golden Retriever
  as exactly $1,000–$3,500. That is copied content, not corroboration, so identical
  (low, high, kind) triples across domains collapse to the Tier A representative.

### Freshness

A source that dates itself and is older than **36 months** is rejected. Puppy prices moved
sharply through and after the pandemic, so an older figure describes a market that no longer
exists — and the scam check would measure today's quotes against it. Caught PetMD's French
Bulldog page ($1,500–$5,000, updated March 2023) on the rule's first real use. Undated
sources aren't punished for age; they're judged on tier and corroboration instead.

### Hard rejects

Never stored as accepted: missing or non-http URL; blocked domain; domain not on the
allowlist; quote under 20 characters; unknown scope or kind; a `range` whose low ≥ high; an
`average` whose low ≠ high; anything outside $100–$25,000; a band wider than 10× (that isn't
a range, it's a shrug); a dated source older than 36 months.

Rejections are **kept as rows with their reason**. A rejection is evidence about a source,
not something to erase.

---

## Search design 2 — real asking prices

Run by `ListingPriceProvider` via `POST /api/admin/listing-prices`. **Off unless
`Prices:ListingsEnabled=true`** — the source's terms restrict automated collection, so this
must be a deliberate act by the operator and can never start on its own.

> **Terms caveat.** Puppies.com's terms forbid systematic collection "including through bots,
> spiders, automated scripts, or AI-assisted tools" without written permission, and restrict
> commercial use. This runs at the product owner's direction and on their risk. The full
> analysis, including AKC's broader prohibition, is in [SOURCES.md](SOURCES.md).

### Two limits that should not be relaxed

1. **Structured data only.** We read the schema.org `ld+json` block the site publishes for
   machine consumption — `ItemList` → `Product` → `Offer.price`. Not rendered markup. Also
   far more robust: a `"price":N` regex over raw HTML would drift silently the day their
   markup changed, leaving us aggregating whatever numbers happened to match.
2. **Never defeat an access control.** A host answering 403 stays unread. PuppySpot is unread
   for exactly this reason, even though its `robots.txt` would permit us and even advertises
   `Allow: /api/search`. A non-success response ends the fetch rather than triggering a
   retry with different headers.

The client identifies itself as `PuppyFinder/1.0` with a repository URL rather than
impersonating a browser, one request per 1.5 s. An operator who wants to block or rate-limit
us should be able to, and a spoofed user-agent would be working around that choice.

### Runs return near-disjoint samples, so samples are pooled

**The single most important property of this source.** Two runs forty minutes apart returned
**zero overlapping listings** for most breeds (French Bulldog 0 shared of ~49, Golden
Retriever 0 of ~45, Labrador 0 of ~45; Beagle 5, Boxer 8). The index hands out a different
slice of a much larger pool each time.

Consequence: any single run is a small random sample, and the published range moves with it.
Australian Shepherd swung from a verified **$800–$1,500** to a refused **$500** floor between
two runs forty minutes apart. That is not a benchmark a fraud check can rest on.

The same property makes pooling unusually effective — with no overlap, each run contributes
~40 genuinely new observations rather than re-confirming the last ones. So aggregation reads a
rolling window (`Prices:ListingWindowDays`, default 90) across runs, deduplicated by listing
URL, keeping each animal at its most recent price. The window bounds staleness; pooling buys
the sample size.

### Percentiles, not extremes

The band is the **25th–75th percentile**. On live listings the extremes are precisely the
scam-priced and rare-colour outliers: a single $400 Frenchie and a single $15,000 one would
define the whole range under min–max. The middle half is what an honest buyer encounters.

Nearest-rank, **not interpolated** — every published figure must be a price somebody is
actually asking, not a number computed between two of them.

Minimum sample **20** after filtering, so one scam listing can't move the 25th percentile.
Same listing seen in two runs counts once, deduplicated on its URL.

### Crossbreeds must be excluded

Breed searches return mixes in quantity — 15 of 50 Bernese Mountain Dog and Shih Tzu results,
14 of 50 for German Shepherd, Poodle and Australian Shepherd. A mix is usually cheaper, so
counting them drags a purebred range down: **the same failure as counting scam listings,
reached by a different route.**

Filtered on exact title match after removing the sex marker. Exact match also handles mixes
without a separate rule, because "Boston Terrier and French Bulldog" simply isn't "French
Bulldog".

Two traps here, both found by running it:

- **The sex marker must be recognised narrowly.** `" - "` also separates the size variety:
  "Poodle - Standard", "Poodle - Miniature". Stripping everything after the last one turned
  "Poodle - Standard - F" into "Poodle - Standard" in one place and "Poodle" in another. Only
  a trailing one-or-two-character segment is treated as sex (observed: M, F, N).
- **Naming mismatches fail silently as "this breed has no listings."** Three breeds returned
  0 usable prices from 50 results on the first run: our "Bulldog" is their "English Bulldog",
  our "German Shepherd" their "German Shepherd Dog", our "Poodle (Standard)" their
  "Poodle - Standard". `ListingSources.SlugOverrides` and `VendorNames` record each, read off
  live titles rather than guessed. **A wrong slug 404s loudly, but a *plausible* wrong slug
  returns another breed's prices quietly** — which is why these are verified by fetching.

### The floor guard — why this isn't circular

The danger in using a classifieds marketplace is precise: **its cheap tail is exactly what
the scam check exists to flag.** Calibrating the benchmark to it would mean screening
classifieds quotes against a classifieds-derived baseline.

So a listing range is refused when its 25th percentile falls below
`ListingFloorFactor` (0.75) × the published low. Compared against, in order:

1. the **researched editorial range**, where one exists;
2. otherwise the **unsourced seed** range as a smell test.

The fallback matters because only 7 breeds have a researched range — without it the guard
never fired for the rest, and the first run published Australian Shepherd at $450–$1,000 and
Siberian Husky at $425–$1,200, both well under any published figure. **The seed is never
published; it is only allowed to refuse.** That is a defensible use of a number too
unreliable to show.

The guard is **one-directional on purpose**. Listings running *higher* than published
articles is expected — articles lag a rising market — and a higher floor makes the check
stricter, not weaker.

It earns its place on live data, refusing three breeds:

| Breed | Listing middle half | Compared against | Outcome |
|---|---|---|---|
| German Shepherd | $800–$1,250 (median $1,000) | published $2,000–$4,000 | refused; keeps the editorial range |
| Cavalier King Charles | $1,200–$2,000 | seed low $1,800 | refused; stays unverified |
| Siberian Husky | $350–$1,000 (median $600) | seed low $800 | refused |

German Shepherd is the clearest case. PetMD and Insurify put a reputable-breeder puppy at
$2,000–$4,500; the marketplace median is $1,000. **Both are true** — they describe different
populations — and the guard correctly refuses to let the classifieds define the benchmark.

---

## How the decision is made

Every threshold is configuration, defaults are the strict values, and changing one is a
re-aggregation rather than a re-collection.

| Setting | Default | What it does |
|---|---|---|
| `Prices:MinSources` | 3 | Independent publishers needed for `verified` (editorial) |
| `Prices:RequireTierA` | true | At least one editorially accountable source |
| `Prices:MaxSpreadRatio` | 2.0 | Max disagreement between source midpoints |
| `Prices:MaxVerifiedBandRatio` | 4.0 | Max width of the published band itself |
| `Prices:DriftReviewPercent` | 40 | Move that flags a change for review |
| `Prices:MinListingSample` | 20 | Live listings needed before percentiles mean anything |
| `Prices:ListingFloorFactor` | 0.75 | How far below the published low a listing band may sit |

### Agreement is not usability

`MaxSpreadRatio` asks whether sources agree *with each other*; it says nothing about the band
they produce. Dachshund had three sources agreeing within 1.88× and still yielded
**$500–$3,500 — a 7× band**, which passed every rule and would have gone live labelled
"verified from 3 sources". With `PriceCheck`'s 0.5× far-below rule, only a quote under $250
would be flagged: the check would read as working while catching nothing. Hence
`MaxVerifiedBandRatio`. **In practice this is the binding constraint, more often than source
count.**

### Drift blocks, it doesn't just warn

A >40% midpoint move used to be flagged for review while still being stored as `verified` —
and `CanScreen` arms on `verified`, so the change went live before anyone read the flag. The
guard named the problem and then let it through. A large move is now held at `contested`,
**but only when it overwrites an already-`verified` range**. Moving off the unsourced seeds is
the entire point of the exercise: German Shepherd's 50% jump from a hardcoded $1,000–$3,000 to
a sourced $2,000–$4,000 must go live, and does. Drift is evidence of a problem only when it
contradicts something already trusted.

### Deciding the bar from evidence

`GET /api/admin/price-report` shows the confidence distribution plus a what-if column per
candidate threshold, all computed over the same stored rows. Pick the bar from the table, set
the config, re-aggregate. Measured results are in [SOURCES.md](SOURCES.md).

## Provenance must match the evidence

`/api/price-sources` initially returned a listings-derived range alongside a list of
*editorial* citations: the count said 49 and the sources said Canine Bible, whose article
gives a different band entirely. **Citing the wrong source is barely better than citing
none** — it is the same fault as the original hardcoded numbers, wearing a disguise.

It now reports the evidence that actually produced the range. For listings: sample size,
host, median, full span and retrieval date, with published figures shown separately under
"Published estimates for comparison". For editorial: publisher, URL and the verbatim quote.
The UI reads "the middle half of 49 puppies listed for sale right now" rather than crediting
an article that didn't produce the number.

## What the schedule will not do

Two guards, because each would spend money or change data without being asked:

- **A key alone starts nothing.** `PriceRefreshJob` needs `Prices:AutoRefresh=true` as well,
  default off. With no key it is *dormant*; with a key but no opt-in it is *idle*. This exists
  because the job originally inherited `AlertChecker`'s run-immediately loop, and adding an
  API key triggered a full 179-breed paid sweep on the next restart — against a prompt nobody
  had validated.
- **No run at startup, ever.** Even opted in, the first pass is one full interval away.
  Services restart for reasons unrelated to prices going stale.

## Running it

```sh
# Published figures (needs an Anthropic key), one breed at a time while calibrating
curl -XPOST -H "X-Admin-Secret: $S" ".../api/admin/price-research?breed=french-bulldog"

# Or record hand-gathered figures — same validator, same rules
curl -XPOST -H "X-Admin-Secret: $S" --json @breed.json ".../api/admin/price-observations"

# Real asking prices (needs Prices:ListingsEnabled=true)
curl -XPOST -H "X-Admin-Secret: $S" ".../api/admin/listing-prices?breed=french-bulldog"

# Re-derive every breed under current thresholds — free, no network
curl -XPOST -H "X-Admin-Secret: $S" ".../api/admin/price-reaggregate"
```

Admin endpoints **fail closed**: with no `Prices:AdminSecret` configured the whole group
returns 403 with an explanation rather than being open, and the comparison is fixed-time.

## Coverage, and what caps it

Collection targets the curated breeds plus `ListingSources.KnownToVendor`, a measured list of
54 catalog breeds the vendor was probed to carry with real inventory. Not "try everything": of
the 154 breeds that had no price, only 54 were worth collecting.

The other 100 split into two groups no mapping fixes:

- **No inventory.** Resolve fine, but with almost nothing to measure — Kerry Blue Terrier 4
  listings, Affenpinscher 1, Finnish Lapphund 0, Sealyham Terrier 0. You cannot compute a
  percentile band from those, and the 20-listing floor exists precisely so one scam price
  can't set a range.
- **Not sold, or not a breed.** Indian Bakharwal and Rajapalayam aren't in the US market;
  `dhole` is a wild canid and `blenheim-spaniel` is a Cavalier coat colour, because dog.ceo's
  list is not a breed list.

**Inventory is the ceiling, not effort.** Popular breeds have listings and rare ones don't,
and that correlation is permanent. The breeds that *do* qualify are the most-searched ones, so
coverage of actual traffic is much better than the raw fraction suggests.

Where it landed: **46 of 175 breeds screening** — 45 from listings, one (German Shepherd) from
published sources. Three breeds refused by the floor guard, five teacup aliases never
collected because they aren't breeds anywhere but here.

### Duplicate catalog entries were a correctness bug, not a wart

The curated list and the dog.ceo list overlapped, and the merge only skipped *exact* slug
matches — so "Shepherd Australian" and "Australian Shepherd" were two breeds, as were
"English Bulldog"/"Bulldog", "Standard Poodle"/"Poodle (Standard)", "Pembroke"/"Pembroke Welsh
Corgi".

That produced **two different prices for the same animal**, and worse, the duplicate
**bypassed the floor guard entirely**: only the curated entry has a seed range to check
against, so `australian-shepherd` was correctly refused at a $500 floor while
`shepherd-australian` published exactly that, unguarded. `SiteCatalog.DuplicateOfCurated`
drops the dog.ceo variant at the merge. Miniature and Toy Poodle are deliberately kept —
genuinely distinct breeds we don't otherwise carry.

### One writer to breed_price

There are two collectors and one row, so precedence lives in exactly one place:
`PriceRefreshJob.ReaggregateBreedAsync` derives both, prefers a listing range that clears its
own bar, and otherwise keeps the editorial one. It used to derive from observations alone and
upsert — which meant a single call to the free `/api/admin/price-reaggregate` **silently
reverted every listing-derived range** to its editorial value. The "re-tune a threshold for
free" operation was quietly throwing away the better data.

## Known gaps

- **`ObservationStatus.Pending` is never written.** The review queue at
  `/api/admin/price-review` reads it, so it returns empty by construction, and
  `PriceAggregation.NeedsReview` — computed correctly, including the drift case — is consumed
  by nothing. The *blocking* half of the drift guard works; the surfacing half does not.
- **Coverage is capped by inventory, not by effort.** Rare breeds have too few live listings
  to compute a band from (Kerry Blue Terrier 4, Affenpinscher 1, Finnish Lapphund 0), and no
  slug mapping fixes that. Popular breeds have inventory; rare ones don't, and the
  correlation is permanent.
- **The catalog contains non-breeds.** dog.ceo's list is not a breed list: `dhole` is a wild
  canid, `blenheim-spaniel` is a Cavalier coat colour.
