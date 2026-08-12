# PuppyFinder — Source Site Research & Roadmap

> **Decision (July 2026):** PuppyFinder shows real listings in its own UI, aggregated from **keyless government open-data feeds** (plus RescueGroups when a key is added), with deep-link footer chips to the big consumer sites.

> ⚠ **Correction:** the Petfinder v2 API was **discontinued on Dec 2, 2025** (site rebuild; no new keys since 2024). The Tier-1 entry below is retained as history but is no longer actionable.

*Research date: July 2026. Which big, legitimate US dog/puppy sites can we aggregate listings from, and how?*

PuppyFinder aggregates individual dog listings through `IListingProvider` implementations
(`backend/Services/`). `ListingAggregator` merges all enabled providers, caches for 10 minutes,
and reports per-source status via `GET /api/sources`. Adding a source = implementing the
interface + registering it in `Program.cs`.

---

## Tier 0 — Government open data (no key, in production)

| Dataset | Endpoint | Status |
|---|---|---|
| Montgomery County MD "Adoptable Pets" (refreshed every 2 h) | `https://data.montgomerycountymd.gov/resource/e54u-qx42.json` | ✅ Live via `SocrataProvider` |
| King County WA "Lost, found, adoptable pets" | `https://data.kingcounty.gov/resource/yaai-7frk.json?record_type=ADOPTABLE&animal_type=Dog` | ✅ Live via `SocrataProvider` |

Public Socrata JSON — no auth, generous anonymous limits. More city/county feeds can be found via
[data.gov (tag: pets)](https://catalog.data.gov/dataset/?tags=pets) and added as one more `SocrataDataset` config in `Program.cs`.

## Tier 1 — Public/free APIs (aggregation-ready)

| Site | API | Auth | Status |
|---|---|---|---|
| [Petfinder](https://www.petfinder.com) | ~~v2 REST API~~ | — | ❌ **API discontinued Dec 2, 2025**; provider removed |
| [RescueGroups.org](https://rescuegroups.org) | [v5 REST API](https://rescuegroups.org/services/adoptable-pet-data-api/) | Free API key (request form) | ✅ Provider implemented, dormant until key arrives |

### Petfinder (historical)
- Largest US adoption search engine, but its public v2 API was shut down Dec 2, 2025 during a site
  rebuild; new key issuance had already stopped in 2024. Replacement is a WordPress-only widget.
- Petfinder remains in the deep-link footer (their consumer search URLs still work).

### RescueGroups.org
- Adoptable-pet data platform used by shelters/rescues since 2006; explicitly built for third-party developers.
- v5 REST/JSON API with dog search (`/public/animals/search/available/dogs/`), radius/location filters,
  and fields not found elsewhere (color, pattern, per-animal location). No published rate caps.
- Key request: https://rescuegroups.org/services/adoptable-pet-data-api/
- Docs: https://userguide.rescuegroups.org/display/APIDG/API+Developers+Guide+Home
- API ToS: https://rescuegroups.org/api-terms-of-service/

#### Live since 6 August 2026

A public API key was issued after two unanswered requests; the third named the project and linked
this repository, which is what their reviewers ask for — they publish organisation name, site URL
and key status to their member rescues, so a request with no identifiable service has nothing to
approve. Coverage went from **48 dogs in 2 states to 345 in 34**, which was the largest single
gap in the product.

**These terms are permissive, and worth contrasting with the Puppies.com section below.**
Caching is allowed, commercial use is allowed, and attribution is explicitly *not* required.
What they do require:

| Obligation | Status |
|---|---|
| Refresh cached data at least weekly, daily preferred | ✅ `ListingAggregator.CacheTtl` is 10 minutes |
| Remove an organisation's data within 1 business day on request | ✅ nothing is persisted; cache is in-memory |
| Delete all copies, including backups, if access ends | ✅ same — there is no copy to delete |
| Don't flood the API; 429 is a documented response | ✅ 100 per page, 3 pages max, behind a 10-minute cache |
| Don't share the data with another service, or reuse the key for one | ⚠️ one key, one app — a second app needs its own |
| **Pet Adoption Tracker image on every pet detail page** | ❌ **not implemented — required only for public-facing use** |
| Key status must be `Public` if the service shows data publicly | ⚠️ currently `Private`, which is accurate: localhost only |

The last two fall due **on deployment, not now**. Hosting this publicly without them would breach
the terms, so they are listed as blocking that step rather than as a nice-to-have.

**What the live data needed, none of which the docs mention.** The provider was written against
the documentation and had never run with a key; every item below was found by measuring the first
real responses, and each has a matching guard in `RescueGroupsProvider`:

- **Application placeholders are mixed in with the animals.** `1Dog Not Listed`,
  `-A Dog Not Yet Posted-` and `Foster - Apply to be a Foster Home` all arrived in one fetch, some
  carrying the rescue's logo as the photo. There is no flag distinguishing them, and the wording
  differs per rescue, so both the name and the description are checked.
- **Location is on the organisation, not the animal.** 9 of the first 25 dogs had no `locations`
  relationship at all — the API omits null relationships rather than returning them empty. Adding
  `orgs` as the fallback took city/state from 16/25 to 297/297. A dog with no state cannot be
  reached by the state filter, which is one of the primary controls.
- **`sizeGroup` was unmapped**, so every dog from this source was invisible to the size filter.
  Its `X-Large` collapses into the app's `Large` bucket.
- **Shelter IDs appear as names** (`A030173`), which rendered as "Meet A030173". Treated as a
  missing name.
- **State casing is inconsistent** — `CA`/`Ca`, `TX`/`Tx`, `OK`/`ok`, `ON`/`On` in one response.
  Harmless so far, since the filter compares case-insensitively and the state count already
  dedupes that way, but normalised at the source.
- **Canadian provinces appear** (`ON` is Ontario). RescueGroups covers North America, while the
  filter is labelled "Anywhere in the US". Left as-is — an Ontario dog is a real adoptable dog —
  but the copy and the data disagree, and that is a product decision rather than a bug.

## Tier 2 — Restricted / partner-only APIs

| Site | Access path | Verdict |
|---|---|---|
| [Adopt-a-Pet](https://www.adoptapet.com) | [Pet List API](https://www.adoptapet.com/public/apis/pet_list.html) is per-shelter only; full [search/syndication API](https://partner-apis.adoptapet.com/) requires a signed paid partnership (helpdesk@adoptapet.com) | Revisit if PuppyFinder becomes a real product |
| Petango / [24Petconnect](https://24petconnect.com) | Legacy read-only web services (`ws.petango.com`); Petango was absorbed into 24Petconnect and endpoints may be mid-deprecation | Too shaky to build on |

## Tier 3 — Big & legit, but no API (link-out only)

| Site | Why no integration |
|---|---|
| [AKC Marketplace](https://marketplace.akc.org) | Breeder-paid listing program from AKC-registered litters; no public API; listing pages render client-side (scraping ruled out: fragile + ToS) |
| [Good Dog](https://www.gooddog.com) | Screens breeders against health standards; no public API or feed found |
| [PuppySpot](https://www.puppyspot.com) | USDA-inspected breeder network, AKC partnership; no public API; Cloudflare bot protection |
| ASPCA / Best Friends | Their adoptable dogs largely flow into Petfinder / Adopt-a-Pet already |

**Takeaway for "buying" (breeder) listings:** there is currently no legitimate API path to individual
breeder listings. Options: a curated "Browse breeder sites" link-out section in the UI, or pursue
formal partnerships later.

## Catalog expansion (July 2026)

Traffic/reputation research added the remaining most-used sites, per the policy of
**including high-traffic sites with honest caution labels** rather than omitting them:

| Site | Why included | Caution? |
|---|---|---|
| [Puppies.com](https://www.puppies.com) | Oldest/largest classifieds (since 2003), millions of visitors | ⚠ minimal vetting |
| [Pawrade](https://www.pawrade.com) | Growing broker with background-check claims + health guarantee | note in vetting field |
| [Lancaster Puppies](https://www.lancasterpuppies.com) | Very high traffic (PA/OH classifieds) | ⚠⚠ documented sick-puppy / puppy-mill complaints (BBB, Trustpilot) |
| [Greenfield Puppies](https://www.greenfieldpuppies.com) | High-traffic East-coast sibling of Lancaster | ⚠⚠ same model |
| [Rescue Me!](https://www.rescueme.org) | 1M+ adoptions, breed-by-state rescue browsing | none |

Deliberately excluded: **Petland** (retail chain, longstanding welfare controversy),
NextDayPets/CKC marketplace (low reputation/relevance).

**Craigslist** was initially excluded (68% of puppy-scam reports originate there) but added
July 2026 by owner decision, under the same include-with-honest-caution policy: listed last,
strongest caution label, adopt/rehoming framing (pet *sales* violate Craigslist's rules).
Verified URL patterns: metro search `www.craigslist.org/search/area/{subdomain}?cat=pet&query={q}`
(server-side filtering confirmed; legacy `{subdomain}.craigslist.org/search/pet?query=` 301s there),
state chooser `geo.craigslist.org/iso/us/{state}`, metro subdomains from craigslist.org/about/sites
(verified map in `SiteCatalog.CraigslistMetros`, state-guarded so e.g. Portland ME never lands on
Oregon's site).

Deep-link patterns (search-index verified): `puppies.com/find-a-puppy/{breed}[/{state-name}]`,
`lancasterpuppies.com/breeds/{breed}/puppy[?state=XX]`, `greenfieldpuppies.com/{breed}-puppies-for-sale/`,
`pawrade.com/puppies/{breed}/`, `{breed-no-hyphens}.rescueme.org/{state-name-no-hyphens}`.

## Filter deep-link audit (July 2026)

Which listing filters are URL-addressable per site (everything else is JS/UI-only and
cannot be deep-linked — notably **age**, which no site exposes in URLs):

| Site | Sex | Price | Other verified |
|---|---|---|---|
| AKC Marketplace | ✅ `?gender=male\|female` (verified live: results filter) | — | — |
| Lancaster Puppies | ✅ `?sex=` | ✅ `?price={min}%2C{max}` (blank min = under-$X); combinable with sex + breed/state paths; all-breeds base `/sale/puppies/[near-me/united-states/{state}/]` | `?keyword=` free-text (color workaround) |
| Greenfield Puppies | UI-only | fixed all-breed tiers `/puppies-for-sale-under-{300\|500}/` only | `/find-puppy/` rejects all query params (404) |
| PuppySpot | ⚠ `?gender=` on `/puppies-for-sale` (Google-indexed; unverifiable behind Cloudflare) | — | breed+state `/find-puppies/{breed}/{state-name}` (indexed) |
| Good Dog | ✗ (`/{breed}/male` 404s) | UI-only | size pages `/{breed}/size/{toy\|miniature\|standard}`; size+location can't combine (404) |
| Pawrade | UI-only — **never append query params** (degrades to empty search) | — | state+breed `/puppies-for-sale/{statenoword-gaps}/{breed}/` (state slugs concatenated: `newyork`; verified live + sitemap) |
| Puppies.com | UI-only (params echoed but ignored server-side) | UI-only | — |
| Adopt-a-Pet / Petfinder / Rescue Me / AKC Rescue | UI-only or absent | — | Petfinder search URLs render filter chips but always "0 results / set location" — never link to `/search/` |

**App decision (July 2026):** the UI offers only the near-universal filters
(breed / state / city) so that what a user sets always carries to the site they open.
Sex and price were prototyped and reverted — they carry to only 2–3 of 13 sites (table
above), which reads as "the filter didn't work" everywhere else. Size and trait toggles
(kids/apartment/low-shed) remain, but they only narrow the app's own breed dropdown via
the quiz trait scores — no site-side filtering implied. Revisit sex/price only with
per-card "filters applied here" transparency in the UI.

## Full API landscape audit (July 2026)

Re-run from scratch because coverage, not UI, is the binding constraint: the app has
**60 live dogs in two counties** (MD 42, WA 18), so every other improvement is
cosmetic until this is fixed.

| Source | Free | Self-serve | Coverage | Verdict |
|---|---|---|---|---|
| Petfinder API | — | — | was national | ☠️ **Dead.** Deprecated Dec 2, 2025; replaced by a display-only WordPress widget. Key issuance had already stopped in 2024. |
| **RescueGroups.org v5** | ✅ free, non-profit, **no "powered by" requirement** | ✅ request form, no contract | multi-state, org-opt-in | ✅ **The only viable spine.** Native postal code + radius, age, size, breed, colour/pattern; per-animal lat/lon; multi-resolution images. |
| Adopt-a-Pet search/syndication | ❌ paid partnership, signed contract, mandatory attribution | ❌ | national | Revisit only if productized. |
| Adopt-a-Pet Pet List API | ✅ | ❌ shelter accounts only; returns one shelter's pets | single org | Not available to us. |
| Shelterluv | ~ | ❌ key generated by their support *for that org* | single org | Would require being each shelter. |
| Petango / 24Petconnect (`ws.petango.com`) | ✅ | ❌ per-org `authkey` exposed in shelters' embed code | large network | ⛔ HTML not JSON, no docs or ToS, legacy infra mid-migration, and using another org's authkey is a gray area. |
| PetHarbor | — | — | — | No developer feed; pipeline is inbound-only from Chameleon/Adopt-a-Pet. Homepage redirects to 24Petconnect, but `pet.asp?uaid=` deep links and `get_image.asp` photos still resolve (verified July 2026) — our Montgomery links are fine. |
| Animal Shelter Manager (ASM) | ✅ | ❌ needs an ASM user account per shelter | single org | Long-tail only, high effort per shelter. |
| Municipal open data | ✅ keyless | ✅ | ❌ per-jurisdiction | **And usually the wrong data**: Austin, Bloomington and Long Beach publish *historical intake/outcome* records, not live adoptables. Montgomery + King County are the exception, not a pattern to scale. |
| Shelter Animals Count | ✅ | ✅ | national | Aggregate statistics only, no individual animals. Useful for honest coverage copy. |

**There is no free API with all-50-state coverage, and there won't be.** National
coverage is opt-in per organization, and only ~20% of states require shelters to
report adoption data at all. Petfinder at its peak — the largest network that has
ever existed — had ~14,000 orgs, and even that was never "all". The product
consequence: partial coverage is a permanent first-class state, which is why the UI
states where our feeds reach and hands off to one national site elsewhere.

## Price provenance (July 2026)

> **How the search itself is designed** — the prompt rules, scope normalization, the
> allowlist and tier policy, the listing extraction, the floor guard, and every threshold —
> lives in [PRICE-SEARCH.md](PRICE-SEARCH.md). This section is the *source research*: what
> exists, what we may use, and what the real numbers came out as.


**The gap this closes:** the breed price ranges in `SiteCatalog.cs` were added in the
first feature commit (`9ce94b1`) with no citation, and this file — which records
URL-pattern verification and API research in detail — said nothing about where they came
from. They were almost certainly model-generated from training data. The UI then called
them "verified" in four places and built the scam check on top.

Prices now live in SQLite (`backend/data/prices.db`, gitignored — derived data) with an
append-only observation table. Every figure carries a source URL, a verbatim supporting
quote, and a retrieval date. `SiteCatalog`'s numbers are a **seed only**.

**The original 25 are imported as `unverified`**, with an observation whose publisher
reads `legacy hardcoded (unsourced)`. That is deliberate: trusting them is the bug, so
grandfathering them in as verified would defeat the exercise. They keep working; they
stop claiming.

### Screening is off until the data is sourced (owner decision, July 2026)

`PriceCheck.Evaluate` returns an `Unavailable` verdict unless the breed's range is
`verified`, and the UI hides both the checker and the headline range. Today that means
screening is off for all 179 breeds.

Enforced in the service rather than only in the UI, so an API consumer or a future job
can't produce a fraud verdict from an unattributable number. It is keyed on data, not a
feature flag: each breed begins screening the moment its range reaches `verified`, with
nothing to switch on by hand and no way to accidentally ship unsourced screening again.

The range is hidden alongside the check deliberately — showing "$2,500–$5,000" while
refusing to check quotes against it just invites the reader to run the comparison
themselves, without the caveat.

### There is no authoritative source, and that shapes everything

Checked before designing the research job:

| Candidate | Outcome |
|---|---|
| Good Dog breed pages | Publish **no** price or range at all (verified by fetching `/breeds/french-bulldog`), and no listing prices |
| Breed parent clubs | Deliberately avoid quoting figures — their codes of ethics make pricing the breeder's responsibility to set |
| AKC | Buyer guidance, no prices. AKC Marketplace has real asking prices but no API, Cloudflare, client-side rendering — scraping already ruled out above |

What exists is pet-insurance/financial publishers and affiliate breed-content sites. Raw
figures look wildly inconsistent (Akita: $650–2,000 / $1,000–2,500 / $1,500–3,500) — but
**most of that is conflated scope, not disagreement.** For French Bulldog, $1,500–4,500
was pet-quality standard colour, $5,000–10,000 was rare colours, and ~$5,000 was an
average folding both together. Three different questions reported as one.

### Rules the research job follows

Implemented in `backend/Services/PriceObservationValidator.cs` (pure, no I/O) and
`backend/Data/PriceSources.cs`. Three of these were wrong on the first attempt and were
corrected by running the research by hand before writing the code:

- **Scope normalization.** Every figure is tagged `pet_standard` / `show_or_pedigree` /
  `rare_colour` / `regional` / `rescue` / `unscoped`. **Only `pet_standard` feeds the
  published range.** `regional` was added after a Beagle search returned Northeast
  $1,200–2,500 mixed in with national figures — real data on the wrong axis.
- **`figure_kind`.** Tier A publishers often give an average, not a band ("about
  $5,000"). Averages corroborate but never widen; one falling outside the aggregated
  range forces `contested`, which is how genuine disagreement surfaces instead of being
  averaged away.
- **Spread on midpoints, not lows.** The first metric was max(low) ÷ min(low), which
  scored Beagle at 3.33 over a $700 absolute difference and would have flagged a sensible
  $700–$1,500 band as contested. It measured "one source quoted a wide band", not
  "sources disagree".
- **Outliers by MAD, not Tukey.** The 1.5 × IQR rule silently fails on the case it was
  added for: with five points and one extreme, the extreme sits in the upper half and
  inflates Q3 past its own fence. Median absolute deviation has no such feedback loop.
- **Tier is re-derived, never trusted.** A stored `publisher_tier` is recomputed from the
  reviewed list on every read, so a row from an older build — or a model that mislabelled
  itself — can't grant itself Tier A standing.
- **Tiered sources.** Tier A = editorially accountable publishers. Tier B = breed-content
  sites, which do real research but run on affiliate revenue. **Tier B alone can never
  reach `verified`.**
- **Excluded as price authority.** Anyone *selling* the breed (conflicted), and the
  classifieds we caution users about — Lancaster, Greenfield, Puppies.com, Craigslist.
  Their listing prices are *what the scam check screens against*; letting them set the
  floor would drag it down and quietly disarm the feature.
- **Independence, two ways.** Two *domains* reporting byte-identical figures collapse to
  one source — copied content, not corroboration (PetMD and A-Z Animals both give Golden
  Retriever as exactly $1,000–$3,500). And one *page* stating several pet-quality figures
  is still one editorial voice: Insurify's Frenchie page gives both "around $5,000 … on
  average" and a $2,000–$8,000 table range, which used to supply two of the three sources
  needed to unlock screening. One vote per publisher, representative chosen conservatively
  — a range beats an average, and the widest range wins, because a too-wide band makes the
  check quieter while a too-narrow one accuses honest breeders.
- **Band width, separate from agreement.** `MaxSpreadRatio` asks whether sources agree with
  each other on midpoints; it says nothing about the band they produce. Dachshund had three
  sources agreeing within 1.88x and still yielded $500–$3,500 — 7x wide, where PriceCheck's
  0.5x rule would only flag a quote under $250. The check would have read as working while
  catching nothing. `MaxVerifiedBandRatio` (4.0) caps high ÷ low for `verified`. In practice
  this is the *binding* constraint, more often than source count.
- **Freshness.** A source that dates itself and is older than 36 months is rejected — puppy
  prices moved sharply through and after the pandemic, so an older figure describes a market
  that no longer exists. Caught PetMD's Frenchie page ($1,500–$5,000, updated March 2023) on
  the rule's first real use. Undated sources aren't punished for age; they're judged on tier
  and corroboration.
- **Drift blocks, it doesn't just warn.** A >40% midpoint move used to be flagged for review
  while still being stored as `verified` — and `CanScreen` arms on `verified`, so the change
  went live before anyone read the flag. A large move is now held at `contested`, but *only*
  when it overwrites an already-`verified` range. Moving off the unsourced seeds is the point
  of the exercise: German Shepherd's 50% jump from hardcoded $1,000–$3,000 to sourced
  $2,000–$4,000 must go live, and does.
- **Extraction, never estimation.** No source URL and verbatim quote → no write. A breed
  with nothing citable stays `unverified`. An empty result is a correct answer.
  In practice the strictest version of this rule bites often: several pages state figures
  only in tables or headings, with no sentence containing the number (Hepper on German
  Shepherd and Golden Retriever, Dogster on Beagle and Poodle). Those are skipped rather
  than quoted from an adjacent sentence that doesn't support the figure.

### Listing prices are now the primary source (August 2026)

**Owner decision, taken after reading the terms below.** Ranges for 17 breeds are derived
from live asking prices on Puppies.com rather than from published articles. This is done at
the product owner's direction and on their risk; the terms conflict is real and set out in
the next section, which is retained unedited.

> **Collection paused, 6 August 2026, when this repository was made public.**
> `Prices:ListingsEnabled` is now `false`, so nothing is fetched from Puppies.com any more. The
> 49 ranges already derived from collected listings stay published and the app keeps screening
> against them — aggregation is a pure function over stored rows, so it needs no network.
>
> The reason for pausing is the combination rather than the collection alone. The HTTP client
> identifies itself with this repository's URL (`PuppyFinder/1.0 (+https://github.com/...)`),
> which was a deliberate choice so the operator could block us if they wanted. Once the
> repository is public, that URL — already in their server logs — leads to this page stating
> that we collected knowing their terms forbid it. Running the collector while publishing that
> account is a different proposition from doing either one on its own.
>
> Re-enabling is one setting. It should not be re-enabled without a fresh decision, and the two
> limits below still apply if it ever is.

Two limits are deliberate and should not be relaxed:

- **Structured data only.** We read the schema.org `ld+json` block the site publishes for
  machine consumption — not rendered markup. Also far more stable: a `"price":N` regex would
  drift silently the day their markup changed.
- **Never defeat an access control.** A host answering 403 stays unread. PuppySpot is
  unread for this reason even though its robots.txt would permit it.

The client identifies itself as `PuppyFinder/1.0` with a repository URL rather than
impersonating a browser, one request per 1.5 s. If the operator wants to block or rate-limit
us they should be able to, and a spoofed user-agent would be working around that choice.

**How the range is derived.** The middle half (p25–p75) of the sample, not min–max: on live
listings the extremes are exactly the scam-priced and rare-colour outliers. Nearest-rank
percentiles, so every published figure is a price somebody is actually asking.

**Three things the real data forced, none of which were in the design:**

1. **Crossbreeds are in breed results, in quantity** — 15 of 50 Bernese and Shih Tzu
   results, 14 of 50 for German Shepherd, Poodle and Australian Shepherd. A mix is usually
   cheaper, so counting them drags a purebred range down: the same failure as counting scam
   listings, reached by a different route. Filtered on exact title match, which also handles
   mixes without a separate rule, since "Boston Terrier and French Bulldog" simply isn't
   "French Bulldog".
2. **Naming mismatches fail silently as "this breed has no listings."** Three breeds returned
   0 usable prices from 50 results on the first run: our "Bulldog" is their "English
   Bulldog", our "German Shepherd" their "German Shepherd Dog", our "Poodle (Standard)"
   their "Poodle - Standard". `ListingSources.VendorNames` records each, read off live
   titles. Related trap: `" - "` separates the *size variety* as well as the sex marker, so
   stripping everything after the last one turned "Poodle - Standard - F" into "Poodle -
   Standard" in one place and "Poodle" in another.
3. **The floor guard never fired for most breeds, because it had nothing to compare against.**
   It used the researched editorial range, and only 7 breeds have one — so the other 18
   published unguarded, putting Australian Shepherd at $450–$1,000 and Siberian Husky at
   $425–$1,200, both well under any published figure. The guard now falls back to the
   *unsourced seed* range as a smell test. The seed is never published — it is only allowed
   to refuse — which is a defensible use of a number too unreliable to show.

**The guard earns its place on live data.** Three breeds are refused:

| Breed | Listing middle half | Compared against | Outcome |
|---|---|---|---|
| German Shepherd | $800–$1,250 (median $1,000) | published $2,000–$4,000 | refused; keeps the editorial range |
| Cavalier King Charles | $1,200–$2,000 | seed low $1,800 | refused; keeps the seed, stays unverified |
| Siberian Husky | $350–$1,000 (median $600) | seed low $800 | refused |

German Shepherd is the clearest case: PetMD and Insurify put a reputable-breeder puppy at
$2,000–$4,500, and the marketplace median is $1,000. Both can be true — they describe
different populations — and the guard correctly refuses to let the classifieds define the
benchmark that screens classifieds quotes.

**Provenance had to change with it.** `/api/price-sources` initially returned a
listings-derived range alongside a list of *editorial* citations: the count said 49 and the
sources said Canine Bible, whose article gives a different band. Citing the wrong source is
barely better than citing none, so the endpoint now reports the evidence that actually
produced the range — sample size, host, median, full span, retrieval date — and published
figures appear under "Published estimates for comparison", never as the source. `breed_price`
carries a `basis` column (`editorial` | `listings`) because "49 sources" means 49 articles in
one case and 49 puppies for sale in the other.

**Result: 18 of 179 breeds screening, up from 1.** 17 from listings, German Shepherd from
published sources. Bands are mostly 1.3x–2.5x, tight enough for the scam check to mean
something.

### Why we could not take them (recorded before the decision above)

The obvious better idea: skip the editorial middlemen and take asking prices straight off
legitimate puppy marketplaces. More data, current, and it's what buyers actually face. It
was tested properly, and the answer is **no — on terms, not on technology.**

| Site | Vets breeders | robots on listings | Prices fetchable | Terms |
|---|---|---|---|---|
| AKC Marketplace | yes | allowed (path form; only `?query` facets disallowed) | **no** — client-rendered, 0 prices in HTML | **forbids** any automated means to access or copy content |
| PuppySpot | yes | allowed, even `Allow: /api/search` | **no** — 403 on every request, edge protection | not reached |
| Good Dog | yes | **`Disallow: /puppy/`, `/explore/`** | n/a | n/a |
| Puppies.com | no — classifieds | allowed | **yes** — 40 prices per 4 pages, in the HTML | **forbids** scraping "including through bots, spiders, automated scripts, or AI-assisted tools"; also bans commercial use |

The one source that is technically open is the only one whose terms name AI-assisted tools
explicitly. `robots.txt` permitting a path is not the same as the ToS permitting collection;
where they conflict the ToS governs. Both have an express-written-permission carve-out, so
**asking is the route**, not building around it.

**`akc.org` was removed from Tier A for this reason.** Their ban covers "any other automated
means to access, collect, copy or record" their content — which includes a model reading a
page through a search tool, not just bulk scraping. Having declined Puppies.com on those
grounds, keeping AKC would be one standard for a classifieds site and a softer one for AKC.
No observation had ever been sourced from it. `PriceObservationValidatorTests` pins all three
domains as unusable so they don't get re-added.

Worth recording what the probe *showed*, since it argues for pursuing permission:

- **French Bulldog, 40 live listings:** p25–p75 = **$1,500–$3,000**, median $2,375 — a 2.0x
  band, *narrower* than the editorial $1,500–$4,000, on 40 points instead of 4. The scam tail
  was plainly visible: the cheapest listings were $400 and $450, matching Canine Bible's
  warning that "$400–$800 is a common tactic used to lure buyers before disappearing with a
  deposit."
- **Beagle, 40 live listings:** p25–p75 = **$400–$900**, median $600 — materially *below* the
  editorial $400–$1,200. Classifieds Beagles skew cheap, so calibrating to them would teach
  the check that a $400 Beagle is typical. This is the circularity risk in concrete form:
  screening classifieds quotes against a classifieds-derived baseline.

That is why the design, if permission is ever granted, is **listings for the central mass with
the editorial range as a floor guard** — not listings alone.

### Allowlisted domains that can't actually be cited

Discovered by trying. `thesprucepets.com` blocks our crawler outright; `lemonade.com` and
`rover.com` return 403. All three are on the Tier A list and no run can ever quote them —
the API's web-search tool would hit the same wall. Left in place so the list stays a record
of what was reviewed, but they are not usable capacity.

Per-breed cost pages exist on predictable URL patterns for `insuranceopedia.com`
(`/pet-insurance/<breed>-cost`), `articles.hepper.com` (`/<breed>-cost/`),
`caninebible.com` (`/<breed>-prices-and-costs/`) and `petmd.com` (`/dog/breeds/<breed>`) —
but coverage is patchy per breed, and MetLife's breed spotlights often carry no price at all
(verified on Labrador). Tier A coverage, not Tier B, is the scarce resource: Labrador and
Poodle have no citable Tier A figure at all, which caps them at `contested` however many
Tier B sources agree.

### Gathering and judging are separate steps

`price_observation` is the durable artifact; `breed_price.confidence` is a pure function
over it. The research job only ever writes observations. That split is what makes the
`verified` bar cheap to re-tune — `POST /api/admin/price-reaggregate` re-derives every
breed from stored rows in seconds, with no re-research and no API spend — and it's why the
threshold decision could be deferred until there were real numbers to look at.

Thresholds live in config: `Prices:MinSources` (3), `Prices:RequireTierA` (true),
`Prices:MaxSpreadRatio` (2.0), `Prices:MaxVerifiedBandRatio` (4.0),
`Prices:DriftReviewPercent` (40).

### Where the bar sits, and why (August 2026)

Seven breeds researched by hand — the Anthropic account is a work org that can't mint an
API key, so the job itself has never run. Gathered through
`POST /api/admin/price-observations`, which takes the same payload the model emits and runs
it through the same validator, so hand-entered rows face identical rules. Those rows are
marked `model = "manual"`.

| Breed | Range | Band | Confidence | Why not verified |
|---|---|---|---|---|
| German Shepherd | $2,000–$4,000 | 2.00x | **verified** | — screening is live |
| French Bulldog | $1,500–$4,000 | 2.67x | contested | Insurify's $5,000 average sits outside |
| Labrador Retriever | $800–$2,500 | 3.12x | contested | no citable Tier A source exists |
| Beagle | $400–$1,200 | 3.00x | contested | sources disagree 2.20x on midpoint |
| Dachshund | $500–$3,500 | 7.00x | contested | band too wide to screen against |
| Golden Retriever | $750–$3,250 | 4.33x | contested | 2 sources (PetMD ≡ A-Z Animals) |
| Poodle | $650–$2,500 | 3.85x | contested | 2 sources, no Tier A |

`GET /api/admin/price-report` over the same stored observations:

| minSrc | Tier A | spread | band | verified |
|---|---|---|---|---|
| 2 | no | 3.0 | 8.0 | 6 |
| 2 | yes | 2.0 | 5.0 | 2 |
| 3 | yes | 3.0 | 5.0 | 2 |
| **3** | **yes** | **2.0** | **4.0** | **1** ← current |
| 3 | yes | 2.0 | 8.0 | 2 |
| 3 | yes | 2.0 | 3.0 | 1 |
| 4 | yes | 1.5 | 3.0 | 0 |

Reaching 6 of 7 requires 2 sources, no Tier A requirement, 3x spread *and* an 8x band —
close to accepting whatever comes back. **The honest reading is that this source pool
supports very few verified ranges, and the answer is wider coverage rather than a lower
bar.** One in seven is the system working, not a setup failure.


## Roadmap

1. **Phase 1 (blocked on the key, requested July 2026):** RescueGroups v5 →
   `RescueGroupsProvider` activates on `RescueGroups:ApiKey`. Build on **v5** (v2 is
   deprecated), keep it server-side (**no CORS**), keep the 10-minute aggregator
   cache (responses run ~10 s), expect **429** on bursts.
   *First task once it works: measure coverage* — distinct states, orgs, dogs per
   major metro, and how often `age`/`size`/`good-with` are actually populated —
   before designing more UI on top of it. Dedupe overlap with the county feeds on
   name + city/state.
2. **Phase 2:** ZIP + radius search and a true distance sort, using the per-listing
   lat/lon RescueGroups supplies. Biggest remaining UX gap (see DESIGN.md §7).
3. **Phase 3:** Adoption fee on the card, real "good with kids/dogs/cats" filters —
   both are RescueGroups fields we can't populate today.
4. **Phase 4 (optional):** Adopt-a-Pet paid partnership if this becomes a funded
   product. Do **not** scrape Petango/PetHarbor, and stop adding Socrata feeds —
   that path mostly adds datasets of dogs that already left.
