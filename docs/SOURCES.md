# PuppyFinder — Source Site Research & Roadmap

> **Decision (July 2026):** PuppyFinder uses **direct deep links** into each site's listing pages — no API calls. See README. This research is kept for reference if API aggregation is ever revisited.

*Research date: July 2026. Which big, legitimate US dog/puppy sites can we aggregate listings from, and how?*

PuppyFinder aggregates individual dog listings through `IListingProvider` implementations
(`backend/Services/`). `ListingAggregator` merges all enabled providers, caches for 10 minutes,
and reports per-source status via `GET /api/sources`. Adding a source = implementing the
interface + registering it in `Program.cs`.

---

## Tier 1 — Public/free APIs (aggregation-ready)

| Site | API | Auth | Status |
|---|---|---|---|
| [Petfinder](https://www.petfinder.com) | [v2 REST API](https://www.petfinder.com/developers/) | Free key + secret (OAuth client-credentials) | ✅ Provider implemented — `PetfinderProvider.cs` |
| [RescueGroups.org](https://rescuegroups.org) | [v5 REST API](https://rescuegroups.org/services/adoptable-pet-data-api/) | Free API key (request form) | 🔜 Best next provider |

### Petfinder
- Largest US adoption search engine — aggregates thousands of shelters and rescues.
- Official, documented v2 API: `GET /v2/animals?type=dog&status=adoptable` after an OAuth token call.
- Key signup: https://www.petfinder.com/developers/ (~2 minutes, free).
- Credentials go in `backend/appsettings.Development.json` under `"Petfinder"`.

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

## Roadmap

1. **Phase 1 (now):** Add the free Petfinder key → live adoption listings flow.
2. **Phase 2:** Request a RescueGroups key → implement `RescueGroupsProvider` (`IListingProvider`).
   Dedupe overlap: some rescues post to both networks — match on name + city/state.
3. **Phase 3 (optional):** Curated breeder link-out section; Adopt-a-Pet paid partnership if productized.
