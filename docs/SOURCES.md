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

Deliberately excluded: **Craigslist** (68% of puppy-scam reports originate there), **Petland**
(retail chain, longstanding welfare controversy), NextDayPets/CKC marketplace (low reputation/relevance).

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

## Roadmap

1. **Phase 1 (now):** Add the free Petfinder key → live adoption listings flow.
2. **Phase 2:** Request a RescueGroups key → implement `RescueGroupsProvider` (`IListingProvider`).
   Dedupe overlap: some rescues post to both networks — match on name + city/state.
3. **Phase 3 (optional):** Curated breeder link-out section; Adopt-a-Pet paid partnership if productized.
