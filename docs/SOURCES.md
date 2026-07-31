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

- **Scope normalization.** Every figure is tagged `pet_standard` / `show_or_pedigree` /
  `rare_colour` / `rescue` / `unscoped`. **Only `pet_standard` feeds the published
  range.** A source giving one undifferentiated number is `unscoped` and excluded — that
  is the point, not a limitation.
- **Tiered sources.** Tier A = editorially accountable publishers (insurance, financial,
  veterinary). Tier B = breed-content sites, which do real research but run on affiliate
  revenue. **Tier B alone can never reach `verified`.**
- **Excluded as price authority.** Anyone *selling* the breed (conflicted), and the
  classifieds we caution users about — Lancaster, Greenfield, Puppies.com, Craigslist.
  Their listing prices are *what the scam check screens against*; letting them set the
  floor would drag it down and quietly disarm the feature.
- **Independence.** Two domains reporting byte-identical ranges is copied content, so it
  collapses to one source rather than counting as corroboration.
- **Extraction, never estimation.** No source URL and verbatim quote → no write. A breed
  with nothing citable stays `unverified`. An empty result is a correct answer.
- **Confidence is a property of the data**, surfaced through `/api/breeds` and
  `/api/price-sources`, so the UI reads it rather than asserting "verified" itself. The
  hero badge counts only `verified` breeds and therefore cannot overstate.

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
