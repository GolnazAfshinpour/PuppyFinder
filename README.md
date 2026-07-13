# PuppyFinder

Search once — land on the right page of every legit puppy site. PuppyFinder is a central hub over the popular, legitimate US puppy websites:

- **Universal search hub** — pick breed + state + adopt/buy, and every site card deep-links to that site's filtered results; "Open results on all sites" launches them all in tabs
- **Breed finder quiz** — six lifestyle questions score against a breed-traits table and recommend your top 3 breeds (`POST /api/quiz`)
- **Site guide** — each site card shows what actually differs: vetting level, typical prices, how the dog gets to you, and who the site is best for

Layout: search filters live in a left sidebar (sticky on desktop); matching sites render on the right. The live-listings backend (`/api/listings`, open-data + RescueGroups providers) still runs but is not currently displayed in the UI.

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
| `GET /api/listings?breed=&state=` | Aggregated real dog listings (filtered) |
| `GET /api/sources` | Per-source status: enabled, count, last error |
| `GET /api/breeds` | Curated breed list for the dropdown |
| `GET /api/sites?breed=&state=` | Deep links into each source site (footer chips) |

## Architecture

Sources implement `IListingProvider` (`backend/Services/`); `ListingAggregator` merges enabled providers and caches for 10 minutes. `SocrataProvider` is config-driven — adding another city/county open-data feed is one more `SocrataDataset` entry in `Program.cs`. `SiteCatalog.cs` holds the deep-link URL patterns for the footer.
