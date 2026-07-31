# PuppyFinder Design System — "Golden Hour"

> One brand, two modes. Warm ground, one confident accent, photography does the
> emotional work, numbers do the trust work. If a change fights one of those
> four sentences, it's off-brand.

Adopted July 2026, based on two research tracks: a teardown of how the leading
pet brands design for trust + warmth (Petfinder, Good Dog, PuppySpot,
Adopt-a-Pet, Rover, Chewy, The Farmer's Dog) and a 2026 design-trend brief.
The shared finding: successful pet UIs use a **cream canvas + a single
saturated accent + real animal photography + numeric trust claims** — never
rainbows, script fonts, or paw-print wallpaper.

## 1. Color tokens

Defined once as the daisyUI themes `goldenhour` / `goldenhour-dark` in
`frontend/src/style.css`. **Always use semantic classes** (`bg-base-200`,
`text-primary`, `badge-secondary`) — never raw hex in components.

| Token | Light | Dark | Role |
|---|---|---|---|
| `base-200` | `#FAF6EF` cream | `#221A14` espresso | page canvas |
| `base-100` | `#FFFDF9` | `#2B211A` | card surfaces |
| `base-300` | `#EFE5D6` | `#3A2D22` | borders, image placeholders |
| `base-content` | `#2D2117` espresso ink | `#F2E8DC` | text (never pure black/white) |
| `primary` | `#C75B39` terracotta | `#D97A55` | THE accent: CTAs, links, active states |
| `secondary` | `#E8A33D` honey | `#E3AA52` | highlights, "buy" badges, alert card |
| `accent` | `#7C8B6F` sage | `#93A385` | "adopt" badges, quiet success |

Rule of one: terracotta is the only color that asks for attention on a screen.
Honey and sage support; they never compete.

## 2. Typography

| Level | Font | Class recipe | Usage |
|---|---|---|---|
| Hero (one per page) | Fraunces 600 | `font-display text-3xl sm:text-5xl leading-[1.1] tracking-tight` | the single headline |
| Wordmark | Fraunces 600 | `font-display text-xl` | nav only |
| Section heading | Nunito Sans 700 | `text-2xl font-bold` | "Adoptable dogs right now" |
| Card title | Fraunces 600 | `font-display text-xl font-semibold` | dog names, site names |
| Body | Nunito Sans 400 | `text-sm` / `text-base` | everything else |
| Micro label | Nunito Sans 700 | `text-xs font-bold uppercase tracking-wide opacity-60` | filter group labels |

Fraunces = brand voice, used **only** for the hero, wordmark, and card titles.
If Fraunces appears more than three levels deep on a screen, it stops being
special. Never larger than `text-5xl` (3rem) — bigger reads as shouting.

## 3. Shape, depth, motion

- Radii come from theme tokens: cards/modals `--radius-box` (1rem), inputs and
  buttons `--radius-field` (0.75rem), badges `--radius-selector` (1rem).
- Elevation: photo/content cards use `.card-lift` (warm-tinted shadow, 3px
  hover lift, gentle photo zoom). Nothing else casts strong shadows.
- The sticky nav is glass: `bg-base-200/80 backdrop-blur-md`.
- Motion is purposeful and short (≤ 0.4s ease); no bouncing, no parallax.

## 4. Iconography & imagery

- Icons are **Heroicons outline** (inline SVG, `stroke-width 1.8`), tinted
  `text-primary/80` or `text-secondary`. Emoji are allowed only as *content
  warmth* (🐶 in a tab label, 🐾 on the quiz button) — never as field icons,
  bullets, or in headings.
- Photos: 4:3 on listing cards (`aspect-[4/3]`, `object-cover`), `base-300`
  placeholder behind them, 🐶 fallback when a shelter has no photo. Dogs are
  never illustrated as humans and photos are never tinted.

## 5. Component conventions

- **Dog card:** photo with favorite heart top-right (base-100/85 blur circle,
  primary heart, `aria-pressed`) → Fraunces name (stretched link: the name's hit area is
  the whole card, other interactive elements sit above via `relative z-10`,
  card shows `focus-within` ring) + fit badge → ONE muted metadata line
  ("Male · Neutered · 2 Years · Medium") → breed → location → contact →
  one primary CTA ("Meet {name} ↗"). Max 1-2 badges per card, ever
  (Baymard/NN/g: badge piles kill 3-second scanning).
- **Dog detail (`?dog=<id>`):** photo `object-contain` on a fixed-height warm
  panel — shelter photos come in every aspect ratio and a fixed crop reliably
  decapitated the dog. Then name + meta line → age/breed badges → full bio →
  boxed shelter contact with the animal ID to quote → ONE outbound CTA. Cards link
  here rather than to the source site: ejecting at peak intent lost people to
  petharbor.com. Card links stay real `<a href>`s (middle-click works) with the
  click intercepted. Escape and backdrop close; body scroll locks; a dog that left
  the feed gets a "found a home" message, never an error.
- **Site card:** Fraunces name + kind badge → description → icon rows
  (vetting/price/delivery/best-for) → caution alert (if any) → link-depth
  badges → one primary CTA.
- **Badges:** `badge-soft` for data (sex, size), `badge-outline` for meta
  (trust chips), `badge-primary badge-soft` for interactive chips (removable
  filters, fit %). Text never wraps inside a badge (`whitespace-nowrap`).
- **Trust is numeric:** "14 sites", "56 live dogs", "$50 adoption fee" — no
  bare adjectives ("trusted", "best") without a number or mechanism nearby.

## 6. Voice

Second-person, warm verbs, contractions, clinical words only inside warm
sentences: "know exactly who to trust before any money changes hands."
Honesty beats marketing — cautions and coverage limits are stated plainly.

## 7. Search UX rules

- **Buying is the primary path** (owner decision, July 2026), so `buy` is the default
  goal and never appears in the URL. Buy mode runs in decision order: what the breed
  costs → is this quote sane → where to look → who to trust. Adoption stays a real
  secondary path, offered right after the price panels — the moment a four-figure
  range makes it most persuasive — never as a guilt trip.
- **A safety feature that can't be sourced is switched off, not caveated.** The price
  scam check returns `Unavailable` unless the breed's range came from independent cited
  sources. A caveat is the right tool for a *soft* claim ("size not listed"); it is the
  wrong tool for "this seller is defrauding you", where being wrong in either direction
  does real harm — a legitimate breeder accused, or a real scam waved through. The gate
  lives in `PriceCheck.Evaluate`, not just the UI, so no caller can bypass it, and it's
  keyed on data rather than a feature flag so each breed switches on by itself.
- **When a feature is off, replace it with something that works.** Price screening off
  doesn't mean a blank panel: the buy path shows the five checks that need no price at
  all (three quotes, live video, health testing on paper, never wire/gift-card/crypto,
  walk away from post-commitment fees).
- **A safety check must not read as an all-clear.** The price check says out loud that
  a believable price proves nothing, because scammers price realistically. Warnings
  use `alert-error`; everything else stays calm, since a page that alarms at every
  number trains people to ignore it.
- **The page is dogs, not websites** (adoption path). A search returns listings; the 14-site
  directory sits below them as the fallback for everywhere our feeds don't reach.
  Every primary CTA on the default screen used to be an exit link — that inverted
  the product into a directory of search engines.
- Filter groups ordered by decision-relevance (Baymard/NN/g): Goal → Breed →
  Age → State → City → Size → breed-list narrowing. Goal leads because it isn't a
  refiner — it decides whether the page shows adoptable dogs or breeder
  marketplaces. Refiners never above decision-drivers.
- Filter priority follows observed adopter behaviour (Adopt-a-Pet, 6.5M searches,
  Mar 2024): distance/location > breed (39% of searches) > age > size (16%) >
  sex > good-with > colour/coat (last; never volunteered by users).
- **A filter must filter the results.** Never ship one that quietly does something
  else — the breed-list narrowers are labelled "Narrow the breed list", not
  "Must-haves", because that is all they do.
- Options carry availability context where cheap ("MD · 42 live dogs").
- Active filters render as removable chips above results + "Clear all"
  (clear-all stays a quiet text link, physically apart from primary CTAs).
- Desktop filters update live; the sidebar is sticky with internal scroll
  (`max-h` + `overflow-y-auto`) so an expanded panel never clips.
- Mobile: filters collapse behind a toggle; results stay above the fold.
- Loading = skeleton cards matching the real layout, never spinners.
- **Missing data is "unknown", not "no".** Industry-wide only a fraction of shelter
  listings have complete profiles, so a filter that drops every blank field deletes
  most of the inventory and the user concludes there are no dogs. Unknowns stay in,
  labelled on the card, ranked below confirmed matches, with a "show only confirmed
  matches" opt-out. (Live example: `size=Small` returns 6 dogs, 5 with no size on
  file — the old hard-drop returned 1.)
- Zero results auto-broaden: relax one constraint at a time (city → size → age →
  breed → state → all), state exactly what was relaxed, offer one-tap clear
  and the saved-search alert. The user should never have to rework a failed
  search themselves. Broadened results never claim to be matches — the heading
  reads "Showing N dogs", not "N adoptable dogs".
- **One handoff, not fourteen.** Where our feeds fall short, recommend a single
  site, ranked by no-caution first, then by how much of the user's search its URL
  actually carries. "Open all 14 sites" handed the work straight back to the user.
- Multi-session memory is anonymous: favorites + recently-viewed live in
  localStorage as listing snapshots (they must survive dogs leaving the feed).
- The smart search box parses free text into these same filters (dictionary
  matching only — never a model that can hallucinate inventory); contradictions
  resolve toward the more specific intent with an explanatory hint. "Near me"
  geolocates to state+city with a manual fallback on denial.
- Known gaps (documented, not yet built): **ZIP + radius search and a true
  distance sort** — the single biggest miss, since both leaders treat distance as
  an always-on default; needs per-listing coordinates, which arrive with
  RescueGroups. Also: adoption fee on the card (81% of adopters rank it the most
  important item on a profile, and we show it nowhere), real "good with
  kids/dogs/cats" listing fields (+56% adoption likelihood), breed typeahead
  instead of a 179-option `<select>`, per-option result counts, and a compare view
  for saved dogs.
