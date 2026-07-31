# PuppyFinder

Buy a puppy without getting scammed. PuppyFinder's primary path is **buying**: what a
breed actually costs, which marketplaces really vet their breeders, and a price check
that catches the most common fraud. Adoption is a real secondary path, backed by live
shelter feeds.

- **Price ranges that label their own reliability** — every range carries a `confidence` derived from its sources, so the UI never claims more than the data supports. Ranges live in SQLite with provenance (source URL, verbatim quote, retrieval date); the original hardcoded numbers are imported as `unverified` because no source was ever recorded for them. See [docs/SOURCES.md](docs/SOURCES.md)
- **Price scam check — currently switched off** (`GET /api/price-check`). The screening logic is built and tested, but it returns `Unavailable` for any breed whose range isn't `verified`, and today none are. Owner decision: don't run fraud detection on numbers we can't attribute. It re-enables per breed, automatically, as the research job sources each range — there is no flag to flip
- **Honest marketplace guide** — 7 breeder sites rated on vetting, price, delivery and documented cautions; no breeder marketplace publishes a data feed, and the UI says so rather than faking listings
- **Dogs first (adoption path)** — searching returns actual listings (photo, age, size, shelter phone number), filtered by breed / age / state / city / size and sortable by age
- **Age filter** — Puppy (under 1 yr) / Young / Adult / Senior, parsed out of the free-text ages the feeds publish (`AgeParser`)
- **In-app dog detail view** — full bio, shelter phone number and the animal ID to quote, with one outbound "start the adoption" link. Addressable as `?dog=<id>`, so a single dog is shareable; a dog that has since been adopted says so rather than erroring
- **Honest coverage** — the UI states where our feeds do and don't reach instead of showing an empty grid, and recommends **one** national site that carries your filters rather than opening fourteen tabs
- **Missing data is "unknown", not "no"** — shelters leave size and age blank constantly, so those dogs stay in the results, labelled, ranked below confirmed matches, with a "show only confirmed matches" escape hatch
- **Breed finder quiz** — six lifestyle questions score against a breed-traits table and recommend your top 3 breeds (`POST /api/quiz`)
- **Site guide** — for breeder searches (where no legitimate feed exists) each site card shows what actually differs: vetting level, typical prices, delivery, and documented cautions
- **Saved-search alerts** — email when new matching dogs appear (`POST /api/alerts`)

Layout: filters live in a left sidebar (sticky on desktop, collapsed on mobile);
dog results fill the right, with the site directory below them as the fallback tier.

## Where the listings come from

- **Government open data (always on):** Montgomery County MD Animal Services and King County WA pet adoption feeds — public Socrata JSON endpoints, refreshed continuously.
- **RescueGroups.org (optional):** request a free key at https://rescuegroups.org/services/adoptable-pet-data-api/ and paste it into `backend/appsettings.Development.json` for nationwide rescue coverage. ⚠ Don't commit real keys.
- ~~Petfinder~~ — their public API was discontinued Dec 2, 2025; Petfinder remains available via the deep-link footer chips.

Full research and roadmap: [docs/SOURCES.md](docs/SOURCES.md)

## Stack

- **Backend:** .NET (ASP.NET Core minimal API) — `backend/`
- **Frontend:** Vue 3 + Vite + Tailwind CSS 4 + DaisyUI 5 — `frontend/`

Theming: all 35 DaisyUI themes are enabled — use the 🎨 picker in the header to switch live (persisted in localStorage; default is `autumn`).

## Running locally

```sh
cd backend && dotnet run                    # API on http://localhost:5133
cd frontend && npm install && npm run dev   # UI on http://localhost:5173 (or next free port)
```

The Vite dev server proxies `/api/*` to the backend.

## API

| Endpoint | Description |
|---|---|
| `GET /api/listings?breed=&state=&city=&size=&age=&sort=&includeUnlisted=` | Aggregated real dog listings, filtered and sorted. Each result carries derived `ageMonths` / `ageGroup` and an `unconfirmed` flag (matched only because a field was blank). |
| `GET /api/listings/{id}` | One dog, so a shared `?dog=` link opens regardless of the visitor's filters. 404 = adopted or pulled from the feed. |
| `GET /api/coverage` | Where live dogs exist right now: `[{ state, count, cities }]` |
| `GET /api/sources` | Per-source status: enabled, count, last error |
| `GET /api/breeds` | Breed list with price ranges plus `confidence` (`unverified`/`single_source`/`contested`/`verified`), `sourceCount` and `priceUpdatedAt`. Null price = no range at all. |
| `GET /api/price-sources?breed=` | The cited sources behind a breed's range — publisher, URL, verbatim quote, scope, retrieval date |
| `GET /api/price-check?breed=&price=` | Verdict on a quoted price: `Unknown` / `Free` / `FarBelow` / `Below` / `Typical` / `Above` |
| `GET /api/sites?breed=&state=&city=` | Deep links into each source site, plus which filters each link carries |
| `POST /api/alerts` | Save an email alert for a search (`breed`, `state`, `city`, `size`, `age`) |

## Architecture

Sources implement `IListingProvider` (`backend/Services/`); `ListingAggregator` merges enabled providers and caches for 10 minutes. `SocrataProvider` is config-driven — adding another city/county open-data feed is one more `SocrataDataset` entry in `Program.cs`. `SiteCatalog.cs` holds the deep-link URL patterns.

`ListingQuery` is the single definition of what a filter means, shared by `/api/listings`
and the alert checker so a saved alert can never match differently from the search UI.
`AgeParser` turns feed ages into months and groups. `PriceCheck` holds the scam-screening
thresholds and copy. None of them know about HTTP, so all are unit-tested directly.
