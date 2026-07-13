# PuppyFinder

Every adoptable dog, one place: PuppyFinder shows real puppy/dog listings inside its own UI, aggregated live from official source APIs, with each card linking back to the original listing.

## Setup: API keys (required for live listings)

At least one free key is needed:

1. **Petfinder** (instant): https://www.petfinder.com/developers/ — sign up, copy the **key + secret**
2. **RescueGroups** (email form): https://rescuegroups.org/services/adoptable-pet-data-api/

Paste into `backend/appsettings.Development.json`:

```json
"Petfinder":    { "ApiKey": "YOUR_KEY", "ApiSecret": "YOUR_SECRET" },
"RescueGroups": { "ApiKey": "YOUR_KEY" }
```

Restart the API and listings appear. ⚠ Don't commit real keys — for anything beyond local dev, use `dotnet user-secrets`.

## Stack

- **Backend:** .NET (ASP.NET Core minimal API) — `backend/`
- **Frontend:** Vue 3 + Vite — `frontend/`

## Running locally

```sh
cd backend && dotnet run          # API on http://localhost:5133
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

Sources implement `IListingProvider` (`backend/Services/`); `ListingAggregator` merges enabled providers and caches for 10 minutes. `SiteCatalog.cs` holds the curated site/breed catalog and verified deep-link URL patterns. Source research: [docs/SOURCES.md](docs/SOURCES.md).
