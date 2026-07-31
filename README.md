# PuppyFinder

Search for a dog, not for a website. PuppyFinder shows real adoptable dogs from live
shelter feeds, and falls back to an honest guide to the 14 legitimate US puppy sites
for everywhere those feeds don't reach yet.

- **Dogs first** — searching returns actual listings (photo, age, size, shelter phone number), filtered by breed / age / state / city / size and sortable by age
- **Age filter** — Puppy (under 1 yr) / Young / Adult / Senior, parsed out of the free-text ages the feeds publish (`AgeParser`)
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
| `GET /api/coverage` | Where live dogs exist right now: `[{ state, count, cities }]` |
| `GET /api/sources` | Per-source status: enabled, count, last error |
| `GET /api/breeds` | Curated breed list for the dropdown |
| `GET /api/sites?breed=&state=&city=` | Deep links into each source site, plus which filters each link carries |
| `POST /api/alerts` | Save an email alert for a search (`breed`, `state`, `city`, `size`, `age`) |

## Architecture

Sources implement `IListingProvider` (`backend/Services/`); `ListingAggregator` merges enabled providers and caches for 10 minutes. `SocrataProvider` is config-driven — adding another city/county open-data feed is one more `SocrataDataset` entry in `Program.cs`. `SiteCatalog.cs` holds the deep-link URL patterns.

`ListingQuery` is the single definition of what a filter means, shared by `/api/listings`
and the alert checker so a saved alert can never match differently from the search UI.
`AgeParser` turns feed ages into months and groups. Neither knows about HTTP, so both
are unit-tested directly.
