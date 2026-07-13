# PuppyFinder

One place to start your puppy search: pick a breed (and optionally a state) and jump straight into the breed-filtered listings on every major legitimate US site — breeder marketplaces, adoption platforms, shelters, and rescues.

No API keys, no scraping — the backend serves a curated catalog of sites with verified deep-link URL patterns, and all navigation happens in your browser.

## Stack

- **Backend:** .NET (ASP.NET Core minimal API) — `backend/`
- **Frontend:** Vue 3 + Vite — `frontend/`

## Running locally

Start the API (http://localhost:5133):

```sh
cd backend
dotnet run
```

In a second terminal, start the frontend (http://localhost:5173, or the next free port):

```sh
cd frontend
npm install
npm run dev
```

The Vite dev server proxies `/api/*` to the backend, so no CORS configuration is needed in development.

## API

| Endpoint | Description |
|---|---|
| `GET /api/breeds` | Curated breed list for the dropdown |
| `GET /api/sites?breed={slug}&state={XX}` | Site cards with resolved deep links for the chosen breed/state |

The site catalog and per-site URL templates live in `backend/Data/SiteCatalog.cs`. To add a site or breed, extend the lists there (URL patterns are documented in [docs/SOURCES.md](docs/SOURCES.md)).

Source research (which sites are legit, what access they offer): [docs/SOURCES.md](docs/SOURCES.md)
