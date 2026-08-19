# PuppyFinder

Buy a puppy without getting scammed. PuppyFinder's primary path is **buying**: which
marketplaces really vet their breeders, which have a complaint record, and the checks
that catch a scam before money moves. Adoption is a real secondary path, backed by live
shelter feeds.

Price-based scam screening switches on **per breed, as each range gets sourced**. Today that
is 50 of 174 breeds — 49 derived from live marketplace asking prices, one from published
sources. A further 9 breeds carry an unsourced legacy range, shown but never screened against.
The remaining 116 return `Unavailable` rather than screen against a number we can't attribute.

- **Price ranges that label their own reliability** — every range carries a `confidence` derived from its sources, so the UI never claims more than the data supports. Ranges live in SQLite with provenance (source URL, verbatim quote, retrieval date); the original hardcoded numbers are imported as `unverified` because no source was ever recorded for them. See [docs/SOURCES.md](docs/SOURCES.md)
- **Price scam check — on for sourced breeds only** (`GET /api/price-check`). Returns `Unavailable` for any breed whose range isn't `verified`. Owner decision: don't run fraud detection on numbers we can't attribute. It enables per breed automatically as each range gets sourced — there is no flag to flip. Live for 50 breeds
- **Ranges from real asking prices** — the middle half of the live listings for a breed, not a journalist's estimate. Crossbreeds are excluded (up to 15 in 50 results), and a range is refused when its middle half falls far below what publishers report — a classifieds site's cheap tail is what the check exists to flag, so it must not become the benchmark. Every range records which kind of evidence produced it (`basis`), and the UI says "the middle half of 49 puppies listed for sale" rather than crediting an article that didn't produce the number. See [docs/SOURCES.md](docs/SOURCES.md) for the terms caveat
- **Honest marketplace guide** — 7 breeder sites rated on vetting, price, delivery and documented cautions; no breeder marketplace publishes a data feed, and the UI says so rather than faking listings
- **Dogs first (adoption path)** — searching returns actual listings (photo, age, size, shelter phone number), filtered by breed / age / state / city / size and sortable by age. Currently ~345 dogs across 34 states, from two county open-data feeds plus RescueGroups
- **Distance search** — a ZIP and a radius, or one tap of "Use my location". Every card shows how far away the dog is, and `sort=nearest` orders by it. Distance is the filter adopters use most, so it leads the location group; a dog whose rescue recorded no location stays in the results rather than vanishing over a blank field
- **Age filter** — Puppy (under 1 yr) / Young / Adult / Senior, parsed out of the free-text ages the feeds publish (`AgeParser`)
- **In-app dog detail view** — full bio, shelter phone number and the animal ID to quote, with one outbound "start the adoption" link. Addressable as `?dog=<id>`, so a single dog is shareable; a dog that has since been adopted says so rather than erroring
- **Honest coverage** — the UI states where our feeds do and don't reach instead of showing an empty grid, and recommends **one** national site that carries your filters rather than opening fourteen tabs
- **Adoption fee and good-with-kids/dogs/cats on the listing** — the two fields adopters act on most, mapped from RescueGroups (`adoptionFeeString`, `isKidsOk`, `isDogsOk`, `isCatsOk`). Both are sparse in the feed — fee 24%, dogs 41%, kids 25%, cats 21% — so they are three-state, never two: yes, no, or *the rescue didn't say*. A blank never renders as "no", negatives are stated plainly, and a dog with no published fee gets "ask what it is and what it covers" instead of a silent gap. Fees are normalised for display ("175.00" → "$175") while non-numeric answers ("Varies", "$300-$450") pass through verbatim
- **Good-with filter** (`GET /api/listings?goodWith=kids,dogs,cats`) — narrows to dogs the rescue recorded as good with kids, other dogs or cats. Unknowns are kept and labelled, the way size and age already work; a dog the rescue marked **not** suitable is dropped unconditionally and `includeUnlisted=false` cannot bring it back. Live: `goodWith=cats` takes 350 dogs to 287 (63 explicit noes removed, 253 unrecorded kept), and strict mode to 34. Shown only when adopting; the breed-list narrower was renamed "Kid-friendly breeds" so the two controls can't produce identical chips
- **Missing data is "unknown", not "no"** — shelters leave size and age blank constantly, so those dogs stay in the results, labelled, ranked below confirmed matches, with a "show only confirmed matches" escape hatch
- **Breed typeahead** — the breed control matches anywhere in the name, so "retriever" finds Golden and Labrador and "shepherd" finds Australian and German. A native `<select>` only jumps to names beginning with what you type, which is why 174 options in a dropdown was the wrong control. Keyboard operable throughout, clears in one click, and says so when nothing matches
- **Breed finder quiz** — six lifestyle questions score against a breed-traits table and recommend your top 3 breeds (`POST /api/quiz`)
- **Site guide** — for breeder searches (where no legitimate feed exists) each site card shows what actually differs: vetting level, typical prices, delivery, and documented cautions
- **Saved-search alerts** — email when new matching dogs appear (`POST /api/alerts`)
- **Fee check — the scam check that needs no price** (`GET /api/fee-check?fee=&paid=&asker=`). Three inputs, and the fee's name is the least decisive: *what are they asking for*, *have you already sent money*, and *who is asking*. Dictionary matching against the fees documented in published scam reports — crate rental, shipping insurance, permits, customs, a quarantine/"release" fee, an emergency vet bill — with real costs (deposits, transport, health certificates, adoption fees) in the same catalog so a legitimate breeder is never called a scammer. Needs no price range, so unlike the price check it answers for all 174 breeds, and it is the only check in the app aimed at someone who has **already paid**: after a deposit, an unexpected fee is the pattern whatever it is called, so an unrecognised one still warns. Every verdict ends in concrete next actions — offer to collect the dog yourself, search a sentence of their email, check the shipper against IPATA's directory. Lives on the buying path and inside `/safe#escalating-fees`
- **The fee check knows the scam has two actors** — BBB's script is that after the deposit a second party appears posing as a shipping company, and every fee from there comes from them. A transporter who *contacted you* is the finding whatever the fee is called; a transporter you *found and booked* is a real company sending a real invoice, and is not swept up with it
- **Seller licence check — the one check that ends in a public database** (`GET /api/seller-check?delivery=&licence=`). Under the Animal Welfare Act a breeder needs a USDA Class A licence when they keep **more than four breeding females** *and* sell **sight-unseen** (the 2013 Retail Pet Store Rule narrowed the retail exemption to sales where the buyer can see the animal first). The line that does the work: **a puppy shipped to you is not a face-to-face sale**, so a shipper cannot be leaning on that exemption — either they keep four or fewer breeding females, or they are required to be licensed. Both answers are easy to give, which is why refusing to answer is the only branch that warns. A given number is deep-linked to APHIS's public search tool to be verified against name, address and inspection reports; a licence is stated as a floor, never an endorsement; and a buyer seeing the puppy in person is told the check simply doesn't apply, because most good hobby breeders are legitimately exempt. Lives on the buying path and inside `/safe#vet-a-breeder`
- **Safety guide at a real URL** — the eight scam sections are one page at `/safe`, each with its own anchor (`/safe#payments`), so the advice can be linked, shared, cited and indexed. The modal they used to live in is gone: a dialog has no URL, and the search state it was protecting is in the query string anyway, so Back restores it. Every safety button in the app is a link that scrolls to the section answering its question, and a footer links all eight. `npm run build` prerenders the page to static HTML with its title and description, so a crawler (or a reader with JavaScript off) gets the text. Set `SITE_URL` before building to also emit `sitemap.xml` and canonical tags; without it both are skipped rather than guessed. Content lives in `frontend/src/content/safety.js`

Layout: filters live in a left sidebar (sticky on desktop, collapsed on mobile);
dog results fill the right, with the site directory below them as the fallback tier.

## Where the listings come from

- **Government open data (always on):** Montgomery County MD Animal Services and King County WA pet adoption feeds — public Socrata JSON endpoints, refreshed continuously.
- **RescueGroups.org:** request a free key at https://rescuegroups.org/services/adoptable-pet-data-api/, then `cd backend && dotnet user-secrets set "RescueGroups:ApiKey" "..."`. This is what takes coverage from 2 states to 34 — without it the app has only the two county feeds. Name the project and give a real URL on the form: they publish organisation name, site URL and key status to their member rescues, so a request with no identifiable service tends to sit unanswered.
  - **Before hosting this publicly**, two obligations in their [API terms](https://rescuegroups.org/api-terms-of-service/) fall due: every pet detail page must carry their Pet Adoption Tracker image, and the key status must change from `Private` to `Public`. Neither applies while it runs on localhost. Details in `docs/SOURCES.md`.
- ~~Petfinder~~ — their public API was discontinued Dec 2, 2025; Petfinder remains available via the deep-link footer chips.

How the price search is designed: [docs/PRICE-SEARCH.md](docs/PRICE-SEARCH.md)
Full source research and roadmap: [docs/SOURCES.md](docs/SOURCES.md)

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

## Secrets

All credentials live in [`dotnet user-secrets`](https://learn.microsoft.com/aspnet/core/security/app-secrets)
(`~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`), outside the repo. The launchd
service sets `ASPNETCORE_ENVIRONMENT=Development` so they load.

```sh
cd backend
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."   # price research job
dotnet user-secrets set "Prices:AdminSecret" "..."        # admin endpoints (fail closed without it)
dotnet user-secrets set "RescueGroups:ApiKey" "..."       # nationwide rescue listings
dotnet user-secrets set "Smtp:Password" "..."             # alert emails
```

Do **not** put keys in `appsettings*.json` or in `deploy/launchd/*.plist` — the plist is
tracked, and `appsettings.Development.json` used to be, which made its own
"don't commit real keys" comment a trap. It's untracked and gitignored now.

Avoid `dotnet user-secrets list`: it prints every value in full, which is easy to leak
into a log or a shared terminal. Use `dotnet user-secrets list --json | jq 'keys'` to check
*which* secrets exist without revealing them.

## API

| Endpoint | Description |
|---|---|
| `GET /api/listings?breed=&state=&city=&size=&age=&goodWith=&sort=&includeUnlisted=&lat=&lon=&radius=` | Aggregated real dog listings, filtered and sorted. Each result carries derived `ageMonths` / `ageGroup` and an `unconfirmed` flag (matched only because a field was blank). Supplying `lat`/`lon` adds `distanceMiles` to every result and enables `sort=nearest`; adding `radius` (miles) also filters. |
| `GET /api/listings/{id}` | One dog, so a shared `?dog=` link opens regardless of the visitor's filters. 404 = adopted or pulled from the feed. |
| `GET /api/coverage` | Where live dogs exist right now: `[{ state, count, cities }]` |
| `GET /api/sources` | Per-source status: enabled, count, last error, and `lastFetchedAt` (null = never asked, which is not the same as a source that returned nothing) |
| `GET /api/breeds` | Breed list with price ranges plus `confidence` (`unverified`/`single_source`/`contested`/`verified`), `sourceCount` and `priceUpdatedAt`. Null price = no range at all. |
| `GET /api/price-sources?breed=` | The cited sources behind a breed's range — publisher, URL, verbatim quote, scope, retrieval date |
| `GET /api/price-check?breed=&price=` | Verdict on a quoted price. Returns `Unavailable` unless the breed's range is `verified`. Otherwise `Free` / `FarBelow` / `Below` / `Typical` / `Above`. |
| `GET /api/seller-check?delivery=&licence=` | Whether a seller is legally required to hold a USDA licence, and what their answer means. `delivery` is `sight-unseen` / `in-person`; `licence` is `given` / `exempt` / `refused`. Returns `Required` / `Unverifiable` / `Verify` / `Exempt` / `Unknown` plus next actions. Only the sight-unseen + refused pairing warns. |
| `GET /api/fee-check?fee=&paid=&asker=` | Verdict on what a seller is asking money for, with no price range involved. `StopPaying` / `Invented` / `Handoff` / `Papers` / `Real` / `Unrecognised`, plus a list of concrete next actions. `paid=true` shifts the instruction from "don't send it" to "stop". `asker` is `seller` / `transporter-contacted-me` / `transporter-i-booked`; the middle one is the scam's second act and outranks the fee's name. |
| `GET /api/sites?breed=&state=&city=` | Deep links into each source site, plus which filters each link carries |
| `POST /api/alerts` | Save an email alert for a search (`breed`, `state`, `city`, `size`, `age`) |

### Admin (price research)

Disabled unless `Prices:AdminSecret` is set; all require an `X-Admin-Secret` header.

| Endpoint | Description |
|---|---|
| `POST /api/admin/price-research?breed=` | Research one breed (or all, if omitted). Writes observations only — never sets confidence. Needs an Anthropic key. |
| `POST /api/admin/price-observations` | Record observations gathered by hand, for when no key exists. Same payload shape as the model emits, same validator, same rules — only the provenance differs (`model = "manual"`). Body: `[{ "breed": "...", "observations": [...] }]` |
| `POST /api/admin/listing-prices?breed=` | Collect live asking prices for one breed (or every curated breed) and publish the middle half when it clears the floor guard. Requires `Prices:ListingsEnabled=true`; off by default because the source's terms restrict automated collection |
| `POST /api/admin/price-reaggregate` | Re-derive every breed's confidence from stored observations. Free and idempotent: this is how a threshold change is applied. |
| `GET /api/admin/price-report` | Confidence distribution plus a what-if column per candidate threshold. Read-only — pick the bar from evidence. |
| `GET /api/admin/price-holds` | Price changes waiting on approval, each with the range it would replace and how far it moved. |
| `POST /api/admin/price-holds/{breed}?decision=approve\|dismiss&reason=` | `approve` publishes the held range; `dismiss` keeps what is live and stops the same proposal being raised again. |
| `POST /api/admin/price-observation/{id}?decision=accept\|reject&reason=` | Reject a bad source figure, or restore one. The row is kept either way, and that breed re-aggregates. Acts on one piece of *evidence* — to approve a price *change*, use `price-holds`. |

### When a price change waits for you

A range that moves sharply away from one already marked `verified` is **not published**. It is
recorded as a hold, the previous range stays live, and the run logs a warning naming every breed
still waiting. Nothing looks broken while a hold sits open — that is the point, the app keeps
screening quotes against figures it already trusted — but that breed's range is frozen until you
decide, so the outstanding count is repeated on every run rather than only when it is raised.

Moving off an unsourced seed range is never gated: that is the system working, not a warning.

### Turning the research job on

1. Store both secrets with `dotnet user-secrets` — never in a config file. Paste the real
   key: `sk-ant-...` below is a placeholder, and pasting it literally gets you 179 rows of
   `invalid x-api-key`.
   ```sh
   cd backend
   dotnet user-secrets set "Anthropic:ApiKey"    "sk-ant-api03-REAL-KEY-HERE"
   dotnet user-secrets set "Prices:AdminSecret"  "$(openssl rand -hex 24)"
   launchctl kickstart -k gui/$(id -u)/com.puppyfinder.api
   ```
2. `POST /api/admin/price-research?breed=french-bulldog` — read `price_observation` and
   check every row has an allowlisted URL, a verbatim quote and a correct scope.
3. Tune the prompt in `PriceResearchPrompt.SystemRules` against the calibration breeds.
4. `GET /api/admin/price-report` — pick the `verified` bar from the distribution, set
   `Prices:MinSources` / `RequireTierA` / `MaxSpreadRatio` / `MaxVerifiedBandRatio`, then
   re-aggregate. Band width is usually the binding constraint, not source count — see
   [docs/SOURCES.md](docs/SOURCES.md) for the measured distribution.
5. **Only then** set `Prices:AutoRefresh=true` to start the scheduled job
   (`Prices:RefreshDays`, default 30).

Two things the schedule deliberately will not do, because each would spend money you
didn't ask to spend:

- **A key alone starts nothing.** Scheduled runs also need `Prices:AutoRefresh=true`, and
  the default is off. Steps 2–4 are manual, one breed at a time. With no key the job is
  *dormant*; with a key but no opt-in it is *idle*. Either way it logs and changes nothing.
- **No run at startup, ever** — even opted in, the first pass is one full interval away.
  Services restart for reasons that have nothing to do with prices going stale, and a full
  sweep is ~179 paid API calls.

## Architecture

Sources implement `IListingProvider` (`backend/Services/`); `ListingAggregator` merges enabled providers and caches for 10 minutes. `SocrataProvider` is config-driven — adding another city/county open-data feed is one more `SocrataDataset` entry in `Program.cs`. `SiteCatalog.cs` holds the deep-link URL patterns.

`ListingQuery` is the single definition of what a filter means, shared by `/api/listings`
and the alert checker so a saved alert can never match differently from the search UI.
`AgeParser` turns feed ages into months and groups. `PriceCheck` holds the scam-screening
thresholds and copy. None of them know about HTTP, so all are unit-tested directly.
