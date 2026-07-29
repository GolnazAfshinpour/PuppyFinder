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

- **Dog card:** photo → Fraunces name (stretched link: the name's hit area is
  the whole card, other interactive elements sit above via `relative z-10`,
  card shows `focus-within` ring) + fit badge → ONE muted metadata line
  ("Male · Neutered · 2 Years · Medium") → breed → location → contact →
  one primary CTA ("Meet {name} ↗"). Max 1-2 badges per card, ever
  (Baymard/NN/g: badge piles kill 3-second scanning).
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

- Filter groups ordered by decision-relevance (Baymard/NN/g): Breed → State →
  City → Size → Must-haves → Goal. Refiners never above decision-drivers.
- Options carry availability context where cheap ("MD · live shelter dogs").
- Active filters render as removable chips above results + "Clear all"
  (clear-all stays a quiet text link, physically apart from primary CTAs).
- Desktop filters update live; the sidebar is sticky with internal scroll
  (`max-h` + `overflow-y-auto`) so an expanded panel never clips.
- Mobile: filters collapse behind a toggle; results stay above the fold.
- Loading = skeleton cards matching the real layout, never spinners.
- Empty states are never dead ends: say *why* it's empty, offer the next step.
- Known gaps (documented, not yet built): per-option result counts, breed
  combobox with type-ahead, compare-selected view for the 14-site directory.
