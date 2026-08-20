<script setup>
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { TRAITS, breedMatches } from './breedFilters.js'
import { clearProfile, loadProfile, rankListings } from './adopterProfile.js'
import { loadFavorites, loadRecent, recordViewed, toggleFavorite } from './favorites.js'
import { parseQuery } from './smartSearch.js'
import { useDrawer } from './useModal.js'
import { fetchBreedImage } from './dogImages.js'
import { buildSearchQuery, parseSearchUrl } from './searchUrl.js'
import { ARTICLES, articlePath } from './content/articles.js'
import { SAFETY_SECTIONS, safetyPath } from './content/safety.js'
import SearchHub from './components/SearchHub.vue'
import ResultsFallback from './components/ResultsFallback.vue'
import ListingCard from './components/ListingCard.vue'
import DogDetail from './components/DogDetail.vue'
import BreedCost from './components/BreedCost.vue'
import FeeCheck from './components/FeeCheck.vue'
import SellerCheck from './components/SellerCheck.vue'
import AlertSignup from './components/AlertSignup.vue'
import BreedQuiz from './components/BreedQuiz.vue'
import SourcedPrices from './components/SourcedPrices.vue'
import SavedDogs from './components/SavedDogs.vue'
import ThemePicker from './components/ThemePicker.vue'
import PuppyLogo from './components/PuppyLogo.vue'
import Icon from './components/Icon.vue'

const US_STATES = [
  'AL', 'AK', 'AZ', 'AR', 'CA', 'CO', 'CT', 'DE', 'FL', 'GA',
  'HI', 'ID', 'IL', 'IN', 'IA', 'KS', 'KY', 'LA', 'ME', 'MD',
  'MA', 'MI', 'MN', 'MS', 'MO', 'MT', 'NE', 'NV', 'NH', 'NJ',
  'NM', 'NY', 'NC', 'ND', 'OH', 'OK', 'OR', 'PA', 'RI', 'SC',
  'SD', 'TN', 'TX', 'UT', 'VT', 'VA', 'WA', 'WV', 'WI', 'WY',
]

const breeds = ref([])
const sites = ref([])

// Searches are shareable: filters initialize from the page URL and sync back to it.
const fromUrl = parseSearchUrl(window.location.search, US_STATES)
const selectedBreed = ref(fromUrl.breed) // breed slug
const selectedState = ref(fromUrl.state)
const selectedCity = ref(fromUrl.city)
const selectedSize = ref(fromUrl.size)
const selectedAge = ref(fromUrl.age) // Puppy | Young | Adult | Senior
const selectedSex = ref(fromUrl.sex) // Male | Female — prefix-matched, so neutered/spayed count
const traits = ref(fromUrl.traits)
// Filters the dogs themselves, unlike `traits` above which prunes the breed list. Two controls
// that sound alike and do different jobs, so both are labelled for what they actually do.
const goodWith = ref(fromUrl.goodWith)
const goal = ref(fromUrl.goal)
const sort = ref(fromUrl.sort)
const openDogId = ref(fromUrl.dog) // '' = no detail view open
const quizOpen = ref(false)
const pricesOpen = ref(false)
const savedOpen = ref(false)
const filtersOpen = ref(false) // mobile-only filter drawer state
const error = ref('')

// The open drawer is a dialog and owes the reader the standard contract: Escape closes,
// the page behind stops scrolling, Tab stays inside, focus returns to the toggle button.
const filtersPanel = ref(null)
useDrawer(filtersOpen, () => (filtersOpen.value = false), filtersPanel)
// Past the breakpoint the panel is a static sidebar again — release the drawer state so
// a rotate-to-landscape doesn't leave the page scroll-locked behind an invisible dialog.
const lgViewport = matchMedia('(min-width: 1024px)')
lgViewport.addEventListener('change', () => {
  if (lgViewport.matches) filtersOpen.value = false
})

// Real dogs are the page now — they load on arrival rather than behind a tab.
// The site directory moved below them as the fallback for everywhere our feeds
// don't reach, which is most of the country.
const listings = ref([])
const sources = ref([])
const coverage = ref([]) // [{ state, count, cities }] — where live dogs exist right now
// Auto-broadened results shown when the exact search is empty: {listings, note}.
const broadened = ref(null)
const loadingListings = ref(false)
const listingsError = ref('')

// Multi-session memory: saved dogs + recently viewed (localStorage snapshots).
const favorites = ref(loadFavorites())
const recent = ref(loadRecent())
function onToggleFavorite(listing) {
  favorites.value = toggleFavorite(listing)
}
// Opening the detail view is the real "I'm interested" signal — the old card-click
// straight out to petharbor.com is gone.
function openDog(listing) {
  openDogId.value = listing.id
  recent.value = recordViewed(listing)
}
function closeDog() {
  openDogId.value = ''
}
const favoriteIds = computed(() => new Set(favorites.value.map((f) => f.id)))

// The open dog, if we already have the full record in hand. Only the API-backed
// lists count: favorites and recently-viewed are trimmed localStorage snapshots, so
// resolving from those would render a detail view with half its fields missing.
// Anything else (a shared ?dog= link, a saved dog) falls through to DogDetail
// fetching by id — which also correctly reports dogs that have since been adopted.
const openDogListing = computed(() =>
  [...listings.value, ...(broadened.value?.listings ?? [])]
    .find((l) => l.id === openDogId.value) ?? null,
)

// Saved quiz profile (localStorage): when present, listings are re-ranked by fit.
const profile = ref(loadProfile())
const displayListings = computed(() =>
  listings.value.length ? listings.value : (broadened.value?.listings ?? []),
)
const rankedListings = computed(() =>
  // An explicit sort is the user's instruction and outranks the saved profile.
  profile.value && !sort.value
    ? rankListings(displayListings.value, profile.value.scores)
    : displayListings.value,
)
function dropProfile() {
  clearProfile()
  profile.value = null
}

const selectedBreedInfo = computed(
  () => breeds.value.find((b) => b.slug === selectedBreed.value) ?? null,
)
const selectedBreedName = computed(() => selectedBreedInfo.value?.displayName ?? '')
// Two different numbers, deliberately. `pricedBreedCount` is what we can check at
// all; `verifiedBreedCount` is what we can actually stand behind — the hero shows
// the second, so the badge cannot overstate the data by construction.
const pricedBreedCount = computed(() => breeds.value.filter((b) => b.priceLow != null).length)
const verifiedBreedCount = computed(
  () => breeds.value.filter((b) => b.confidence === 'verified').length,
)

// Scam screening is offered per breed, and only where the range came from cited
// sources. Owner decision (July 2026): no price-based fraud detection on numbers
// we can't attribute. Data-driven rather than a flag, so each breed switches on by
// itself once the research job verifies it.
const canScreenSelectedBreed = computed(() => selectedBreedInfo.value?.confidence === 'verified')

const liveCount = computed(() => coverage.value.reduce((total, c) => total + c.count, 0))

// Buying means the site directory leads: no breeder marketplace publishes a
// feed, so we have no listings of our own to show — say so instead of an
// empty grid.
const buying = computed(() => goal.value === 'buy')
// "Show me both" has to mean both. It used to render the adopt template alone, so an
// explicit user choice quietly did something else — the price panels and the live dogs
// now render together, buy-first per the decision order in DESIGN.md.
const showBuy = computed(() => goal.value !== 'adopt')
const showAdopt = computed(() => goal.value !== 'buy')

const resultsHeading = computed(() => {
  const count = rankedListings.value.length
  const puppies = selectedAge.value === 'Puppy'
  const plural = puppies ? 'puppies' : 'dogs'
  const subject = count === 1 ? (puppies ? 'puppy' : 'dog') : plural
  if (!count) return `No adoptable ${plural}`
  // Broadened results aren't matches — claiming "60 adoptable dogs" under a TX
  // filter that returned nothing would be a lie the banner below then contradicts.
  if (!listings.value.length && broadened.value) return `Showing ${count} ${subject}`
  const parts = [`${count} adoptable ${subject}`]
  if (selectedAge.value && !puppies) parts.push(`· ${selectedAge.value}`)
  return parts.join(' ')
})

// Why a given dog is in the list despite a filter it has no data for.
function unconfirmedNote(listing) {
  if (!listing.unconfirmed) return ''
  const missing = [
    selectedSize.value && !listing.size && 'size',
    selectedSex.value && !listing.sex && 'sex',
    selectedAge.value && !listing.ageGroup && 'age',
  ].filter(Boolean)
  // Phrased separately: "didn't list a good with cats" doesn't parse, and this is the caveat
  // most worth getting right — the reader asked precisely because they have a cat.
  const unrecorded = goodWith.value
    .filter((w) => listing[`goodWith${w[0].toUpperCase()}${w.slice(1)}`] === null
      || listing[`goodWith${w[0].toUpperCase()}${w.slice(1)}`] === undefined)
  if (unrecorded.length) {
    const fields = unrecorded.join(unrecorded.length === 2 ? ' or ' : ', ')
    const rest = missing.length ? ` They also didn't list a ${missing.join(' or ')}.` : ''
    return `This rescue didn't record how they are with ${fields} — shown so you don't miss them, but ask.${rest}`
  }
  if (!missing.length) return ''
  return `This shelter didn't list a ${missing.join(' or ')} — shown so you don't miss them.`
}

// Chip labels: "Female" alone reads as a name filter; said as what it filters.
const SEX_LABELS = { Male: 'Male dogs', Female: 'Female dogs' }

// Active filters as removable chips above the results (table-stakes search UX).
// Chip labels for the breed-list narrowers. "Breeds" is load-bearing on the first one — see
// TRAITS in breedFilters.js.
const TRAIT_LABELS = { kids: 'Kid-friendly breeds', apartment: 'Apartment-friendly', lowshed: 'Low-shedding' }
const GOOD_WITH_LABELS = { kids: 'Good with kids', dogs: 'Good with dogs', cats: 'Good with cats' }
const activeChips = computed(() => {
  const chips = []
  if (selectedAge.value) chips.push({ key: 'age', label: selectedAge.value, clear: () => (selectedAge.value = '') })
  if (selectedSize.value) chips.push({ key: 'size', label: `Size: ${selectedSize.value}`, clear: () => (selectedSize.value = '') })
  if (selectedSex.value) chips.push({ key: 'sex', label: SEX_LABELS[selectedSex.value] ?? selectedSex.value, clear: () => (selectedSex.value = '') })
  for (const t of traits.value) {
    chips.push({ key: `trait-${t}`, label: TRAIT_LABELS[t] ?? t, clear: () => (traits.value = traits.value.filter((x) => x !== t)) })
  }
  for (const w of goodWith.value) {
    chips.push({ key: `goodwith-${w}`, label: GOOD_WITH_LABELS[w] ?? w, clear: () => (goodWith.value = goodWith.value.filter((x) => x !== w)) })
  }
  if (selectedBreed.value) chips.push({ key: 'breed', label: selectedBreedName.value || selectedBreed.value, clear: () => (selectedBreed.value = '') })
  // Distance gets a chip like everything else. It used to be invisible here and untouched by
  // "Clear all", so a 25-mile radius kept narrowing results with nothing on screen to explain why.
  if (zipResolved.value || zip.value.trim()) {
    const origin = zip.value.trim() ? `ZIP ${zip.value.trim()}` : 'my location'
    const label = radius.value && zipResolved.value ? `Within ${radius.value} mi of ${origin}` : `Near ${origin}`
    chips.push({ key: 'origin', label, clear: clearOrigin })
  }
  if (selectedState.value) chips.push({ key: 'state', label: selectedState.value, clear: () => (selectedState.value = '') })
  if (selectedCity.value.trim() && selectedState.value) chips.push({ key: 'city', label: selectedCity.value.trim(), clear: () => (selectedCity.value = '') })
  return chips
})
// The origin can come from a ZIP or from geolocation, and both must clear together with the
// radius — a radius without an origin looks applied and does nothing.
function clearOrigin() {
  zip.value = ''
  radius.value = ''
  originLat.value = null
  originLon.value = null
  if (sort.value === 'nearest') sort.value = ''
}
function clearAllFilters() {
  selectedBreed.value = ''
  selectedState.value = ''
  selectedCity.value = ''
  selectedSize.value = ''
  selectedAge.value = ''
  selectedSex.value = ''
  traits.value = []
  goodWith.value = []
  clearOrigin()
}

// The search card's "Clear filters" is a full reset — back to the buying default.
function resetSearch() {
  clearAllFilters()
  goal.value = 'buy'
}

// Shelters leave size and age blank constantly, so those filters keep unknowns by
// default (see ListingQuery) — this lets someone who wants a hard match say so.
// Deliberately not in the shareable URL: it's a refinement of another filter, and
// it only appears when there's actually something to refine.
const strictMatch = ref(false)
const unconfirmedCount = computed(() => rankedListings.value.filter((l) => l.unconfirmed).length)

// What the rescue left blank, named from the filters actually in play. It used to be hardcoded
// to size and age with age as the fallback, so filtering on "good with cats" alone produced
// "didn't have a age listed" — wrong field and wrong grammar, on the banner whose whole job is
// explaining why these dogs are in the list.
const unconfirmedReason = computed(() => {
  const parts = []
  if (selectedSize.value) parts.push('a size')
  if (selectedSex.value) parts.push('the sex')
  if (selectedAge.value) parts.push('an age')
  for (const w of goodWith.value) parts.push(`whether they're good with ${w}`)
  if (parts.length <= 1) return parts[0] ?? 'that'
  return `${parts.slice(0, -1).join(', ')} or ${parts[parts.length - 1]}`
})

// Spoken to screen readers when a search settles (see the role="status" node in the
// template). Updated only when loading finishes, so rapid filter changes don't queue a
// backlog of announcements for results that were already replaced.
const resultsAnnouncement = ref('')
watch(loadingListings, (loading) => {
  if (loading) return
  resultsAnnouncement.value = listingsError.value
    ? 'Loading the dogs failed.'
    : `${resultsHeading.value}${selectedBreedName.value ? ` — ${selectedBreedName.value}s` : ''}`
})

// Reveal in pages rather than dumping every dog into one scroll: 53 cards measured 10,539px,
// about ten screens, with no way to tell how far in you were. The full count stays in the
// heading, so this shortens the page without hiding how many dogs there are — the honest-
// coverage rule applies to a "show more" button as much as to an empty state.
const PAGE = 24
const shownCount = ref(PAGE)
const visibleListings = computed(() => rankedListings.value.slice(0, shownCount.value))
const moreCount = computed(() => Math.max(0, rankedListings.value.length - shownCount.value))

// Any change to the search starts the reveal over — otherwise a narrowed search inherits a
// large count from the previous one and silently shows everything.
watch([rankedListings, () => sort.value], () => {
  shownCount.value = PAGE
})

const ALL_SORTS = [
  { value: '', label: 'Best match' },
  { value: 'nearest', label: 'Nearest first', needsOrigin: true },
  { value: 'youngest', label: 'Youngest first' },
  { value: 'oldest', label: 'Oldest first' },
]

// One smart search box: dictionary NL parsing into the same filters.
const smartQuery = ref('')
const smartHint = ref('')
function runSmartSearch() {
  const parsed = parseQuery(smartQuery.value, { breeds: breeds.value, usStates: US_STATES })
  const hints = []
  // A searched breed is the most specific intent, so it always wins over
  // size/trait constraints — parsed OR pre-existing — that contradict it.
  // Otherwise the breed-narrowing watcher would silently delete the breed
  // and the site links would open unfiltered.
  if (parsed.breed) {
    const breedInfo = breeds.value.find((b) => b.slug === parsed.breed)
    if (breedInfo?.size) {
      const wantedSize = parsed.size || selectedSize.value
      if (wantedSize && breedInfo.size !== wantedSize) {
        hints.push(`ignored “${wantedSize.toLowerCase()}” — ${breedInfo.displayName}s are ${breedInfo.size.toLowerCase()}`)
        parsed.size = ''
        selectedSize.value = ''
      }
      const wantedTraits = [...new Set([...traits.value, ...parsed.traits])]
      const conflicting = wantedTraits.filter((key) => {
        const trait = TRAITS.find((t) => t.key === key)
        return trait && !trait.matches(breedInfo)
      })
      if (conflicting.length) {
        hints.push(`ignored ${conflicting.map((k) => `“${TRAIT_LABELS[k].toLowerCase()}”`).join(', ')} — not a ${breedInfo.displayName} strength`)
        parsed.traits = parsed.traits.filter((t) => !conflicting.includes(t))
        traits.value = traits.value.filter((t) => !conflicting.includes(t))
      }
    }
  }
  if (parsed.breed) selectedBreed.value = parsed.breed
  if (parsed.state) selectedState.value = parsed.state
  if (parsed.size) selectedSize.value = parsed.size
  if (parsed.age) selectedAge.value = parsed.age
  if (parsed.traits.length) traits.value = [...new Set([...traits.value, ...parsed.traits])]
  if (parsed.goal) goal.value = parsed.goal
  if (parsed.inferredState) hints.push(`assuming ${parsed.city} is in ${parsed.inferredState}`)
  if (parsed.city) {
    if (parsed.state || selectedState.value) selectedCity.value = parsed.city
    else hints.push(`pick a state to apply “${parsed.city}”`)
  }
  if (parsed.nearMe) locateMe()
  if (parsed.unmatched.length) hints.push(`didn't understand: ${parsed.unmatched.map((u) => `“${u}”`).join(', ')}`)
  const appliedAny = parsed.breed || parsed.state || parsed.size || parsed.age || parsed.traits.length ||
    parsed.goal || parsed.city || parsed.nearMe
  if (!appliedAny && smartQuery.value.trim()) hints.unshift('try mentioning a breed, age, size, trait, or state')
  smartHint.value = hints.join(' · ')
  if (appliedAny) smartQuery.value = ''
}

// Where the visitor is searching from. Held separately from the ZIP text because the text is what
// they typed and these are what it resolved to — a half-typed ZIP must not move the origin.
const zip = ref(fromUrl.zip ?? '')
const radius = ref(fromUrl.radius ?? '')
const originLat = ref(null)
const originLon = ref(null)
const zipError = ref('')
const zipResolved = computed(() => originLat.value !== null && originLon.value !== null)

// Offered only when there is somewhere to measure from: a "nearest" option that silently does
// nothing is the failure the backend comment warned about, one layer up.
const SORTS = computed(() => ALL_SORTS.filter((s) => !s.needsOrigin || zipResolved.value))

// ZIP to coordinates via zippopotam.us — keyless, US-wide. Debounced because this fires per
// keystroke, and only attempted on a complete five-digit ZIP.
let zipTimer
watch(zip, (value) => {
  clearTimeout(zipTimer)
  const clean = value.trim()
  zipError.value = ''
  if (clean.length === 0) {
    originLat.value = null
    originLon.value = null
    if (sort.value === 'nearest') sort.value = ''
    return
  }

  if (!/^\d{5}$/.test(clean)) {
    originLat.value = null
    originLon.value = null
    return
  }

  zipTimer = setTimeout(async () => {
    try {
      const res = await fetch(`https://api.zippopotam.us/us/${clean}`)
      if (!res.ok) throw new Error('not found')
      const place = (await res.json()).places?.[0]
      const lat = Number(place?.latitude)
      const lon = Number(place?.longitude)
      if (!Number.isFinite(lat) || !Number.isFinite(lon)) throw new Error('no coordinates')
      originLat.value = lat
      originLon.value = lon
      // Nearest is the reason someone typed a ZIP, so offer it by default rather than making them
      // find it in the sort menu.
      if (!sort.value) sort.value = 'nearest'
    } catch {
      originLat.value = null
      originLon.value = null
      zipError.value = `We couldn't find ZIP ${clean} — check it, or pick a state instead.`
    }
  }, 450)
})

// "Near me": browser geolocation. Keeps the coordinates for distance and still reverse-geocodes
// (keyless bigdatacloud endpoint) to fill in the state, with a manual fallback on denial.
const locating = ref(false)
function locateMe() {
  smartHint.value = ''
  if (!navigator.geolocation) {
    smartHint.value = 'Your browser has no location support — pick a state instead.'
    return
  }
  locating.value = true
  navigator.geolocation.getCurrentPosition(
    async (pos) => {
      // Keep these first. The reverse-geocode below may fail or land outside the US, and the
      // coordinates are useful regardless — this is exactly what the old version threw away.
      originLat.value = pos.coords.latitude
      originLon.value = pos.coords.longitude
      zipError.value = ''
      zip.value = ''
      if (!sort.value) sort.value = 'nearest'
      try {
        const res = await fetch(
          `https://api.bigdatacloud.net/data/reverse-geocode-client?latitude=${pos.coords.latitude}&longitude=${pos.coords.longitude}&localityLanguage=en`,
        )
        const place = await res.json()
        const state = place.principalSubdivisionCode?.split('-')[1]
        if (state && US_STATES.includes(state)) {
          selectedState.value = state
          if (place.city) selectedCity.value = place.city
        } else {
          smartHint.value = "Couldn't map your location to a US state — pick one instead."
        }
      } catch {
        smartHint.value = 'Location lookup failed — pick a state instead.'
      } finally {
        locating.value = false
      }
    },
    () => {
      locating.value = false
      smartHint.value = 'Location permission denied — pick a state instead.'
    },
    { timeout: 8000 },
  )
}

// Quiz ↔ filters: saving a profile pre-fills the matching filters, so the
// ranking is legible as removable chips instead of invisible magic.
function applyProfileToFilters(savedProfile) {
  profile.value = savedProfile
  const answers = savedProfile?.answers ?? {}
  if (answers.size && answers.size !== 'any') {
    selectedSize.value = answers.size[0].toUpperCase() + answers.size.slice(1)
  }
  const quizTraits = [
    answers.kids === 'yes' && 'kids',
    answers.home === 'apartment' && 'apartment',
    answers.grooming === 'low' && 'lowshed',
  ].filter(Boolean)
  if (quizTraits.length) traits.value = [...new Set([...traits.value, ...quizTraits])]
}

// Filters the user has set — the fallback card badges which of these its link carries.
const wantedFilters = computed(() => {
  const wanted = []
  if (selectedBreed.value) wanted.push('breed')
  if (selectedState.value) wanted.push('state')
  if (selectedCity.value.trim() && selectedState.value) wanted.push('city')
  return wanted
})

const breedPhoto = ref(null)
watch(selectedBreed, async (slug) => {
  breedPhoto.value = null
  const imagePath = breeds.value.find((b) => b.slug === slug)?.imagePath
  const url = await fetchBreedImage(imagePath)
  if (selectedBreed.value === slug) breedPhoto.value = url // ignore stale fetches
})

// Monotonic request ids, so a slow response can never overwrite a newer one. Filters fire a
// fetch per change and nothing guaranteed the replies landed in order — rapid changes could
// paint a stale search's results over the current one.
let sitesRequest = 0
async function loadSites() {
  const requestId = ++sitesRequest
  error.value = ''
  try {
    const params = new URLSearchParams()
    if (selectedBreed.value) params.set('breed', selectedBreed.value)
    if (selectedState.value) params.set('state', selectedState.value)
    if (selectedCity.value.trim() && selectedState.value) params.set('city', selectedCity.value.trim())
    const res = await fetch(`/api/sites${params.size ? `?${params}` : ''}`)
    if (!res.ok) throw new Error(`API returned ${res.status}`)
    const data = await res.json()
    if (requestId !== sitesRequest) return // superseded by a newer search
    sites.value = data
  } catch {
    if (requestId !== sitesRequest) return
    error.value = 'We couldn’t load the site guide. Check your connection and try again in a moment.'
  }
}

function listingParams(overrides = {}) {
  const values = {
    breed: selectedBreed.value,
    state: selectedState.value,
    city: selectedCity.value.trim() && selectedState.value ? selectedCity.value.trim() : '',
    size: selectedSize.value,
    age: selectedAge.value,
    sex: selectedSex.value,
    sort: sort.value,
    includeUnlisted: strictMatch.value ? 'false' : '',
    goodWith: goodWith.value.join(','),
    // Only ever sent together: a radius with no origin cannot mean anything, and the backend
    // ignores it anyway.
    lat: zipResolved.value ? String(originLat.value) : '',
    lon: zipResolved.value ? String(originLon.value) : '',
    radius: zipResolved.value ? radius.value : '',
    ...overrides,
  }
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(values)) {
    if (value) params.set(key, value)
  }
  return params
}

async function fetchListings(params) {
  const res = await fetch(`/api/listings${params.size ? `?${params}` : ''}`)
  if (!res.ok) throw new Error(`API returned ${res.status}`)
  return res.json()
}

// Zero results is our job to fix, not the user's (only ~20% rework a failed
// search themselves): relax one constraint at a time until dogs appear, and
// say exactly what was relaxed. State goes last — with feeds in two states,
// widening geography means dogs a thousand miles away.
async function broadenSearch() {
  const relaxations = [
    { overrides: { city: '' }, note: `outside ${selectedCity.value.trim()} (all of ${selectedState.value})`, applies: () => selectedCity.value.trim() && selectedState.value },
    { overrides: { size: '' }, note: 'of any size', applies: () => selectedSize.value },
    { overrides: { sex: '' }, note: 'of either sex', applies: () => selectedSex.value },
    { overrides: { age: '' }, note: 'of any age', applies: () => selectedAge.value },
    {
      overrides: { goodWith: '' },
      note: `without the "${goodWith.value.map((w) => GOOD_WITH_LABELS[w].toLowerCase()).join('", "')}" filter`,
      applies: () => goodWith.value.length,
    },
    { overrides: { breed: '' }, note: 'of any breed', applies: () => selectedBreed.value },
    { overrides: { state: '', city: '' }, note: 'across all states with live feeds', applies: () => selectedState.value },
    { overrides: { breed: '', state: '', city: '', size: '', age: '', sex: '', goodWith: '' }, note: 'available right now (all filters relaxed)', applies: () => true },
  ]
  for (const relaxation of relaxations) {
    if (!relaxation.applies()) continue
    try {
      const result = await fetchListings(listingParams(relaxation.overrides))
      if (result.length) {
        return { listings: result, note: relaxation.note }
      }
    } catch {
      return null
    }
  }
  return null
}

let listingsRequest = 0
async function loadListings() {
  const requestId = ++listingsRequest
  loadingListings.value = true
  listingsError.value = ''
  try {
    const [listRes, srcRes, covRes] = await Promise.all([
      fetchListings(listingParams()),
      sources.value.length ? Promise.resolve(null) : fetch('/api/sources'),
      coverage.value.length ? Promise.resolve(null) : fetch('/api/coverage'),
    ])
    if (requestId !== listingsRequest) return // superseded by a newer search
    listings.value = listRes
    // Broadening is up to seven more sequential requests, so check again after it: the user
    // may have changed the search while an already-superseded broaden was still walking.
    const widened = listRes.length ? null : await broadenSearch()
    if (requestId !== listingsRequest) return
    broadened.value = widened
    if (srcRes?.ok) sources.value = await srcRes.json()
    if (covRes?.ok) coverage.value = await covRes.json()
  } catch {
    if (requestId !== listingsRequest) return
    listingsError.value = 'We couldn’t load the dogs. Check your connection and try again in a moment.'
  } finally {
    if (requestId === listingsRequest) loadingListings.value = false
  }
}

const activeSources = computed(() =>
  sources.value.filter((s) => s.enabled).map((s) => s.name),
)

async function loadBreeds() {
  try {
    const res = await fetch('/api/breeds')
    if (res.ok) breeds.value = await res.json()
    // A shared URL can carry a breed we don't know — drop it instead of erroring.
    if (selectedBreed.value && !breeds.value.some((b) => b.slug === selectedBreed.value)) {
      selectedBreed.value = ''
    }
  } catch {
    // the page still works without the dropdown contents
  }
}

function pickQuizBreed(slug) {
  selectedBreed.value = slug
  quizOpen.value = false
}

watch([selectedBreed, selectedState], loadSites)
watch([selectedBreed, selectedState, selectedSize, selectedAge, selectedSex, goodWith, sort, strictMatch, originLat, radius], loadListings)

// Keep the address bar in sync, and make Back/Forward actually work.
//
// This used to call replaceState on every change, so no history entry was ever created:
// two filter changes left history.length at 2, and pressing Back left the site instead of
// undoing the change. The old comment said "replace, not push — no history spam", and the
// price of avoiding spam was having no history at all. There was also no popstate listener,
// so even with entries the UI would not have reacted to Back — the address bar would have
// changed while the page silently disagreed with it.
const currentQuery = () => buildSearchQuery({
  breed: selectedBreed.value,
  state: selectedState.value,
  city: selectedCity.value,
  size: selectedSize.value,
  age: selectedAge.value,
  sex: selectedSex.value,
  traits: traits.value,
  goodWith: goodWith.value,
  goal: goal.value,
  sort: sort.value,
  zip: zip.value.trim(),
  radius: radius.value,
  dog: openDogId.value,
})

// Set while applying a URL back onto the state, so the watcher below doesn't push a new
// entry for a change that *came from* history — the classic popstate feedback loop.
let applyingHistory = false

watch([selectedBreed, selectedState, selectedCity, selectedSize, selectedAge, selectedSex, traits, goodWith, goal, sort, zip, radius, openDogId], () => {
  if (applyingHistory) return

  const query = currentQuery()
  const url = query ? `?${query}` : window.location.pathname
  // Nothing actually changed (a watcher can fire on an identical value): pushing here would
  // add an entry that makes Back appear to do nothing.
  if (url === window.location.search || (!query && !window.location.search)) return

  history.pushState(null, '', url)
})

// The other half: apply the URL back onto the state when the user navigates history.
window.addEventListener('popstate', () => {
  const from = parseSearchUrl(window.location.search, US_STATES)
  applyingHistory = true
  selectedBreed.value = from.breed
  selectedState.value = from.state
  selectedCity.value = from.city
  selectedSize.value = from.size
  selectedAge.value = from.age
  selectedSex.value = from.sex
  traits.value = from.traits
  goodWith.value = from.goodWith
  goal.value = from.goal
  sort.value = from.sort
  // Restored so Back and Forward return to the same circle, not a silently wider search.
  zip.value = from.zip
  radius.value = from.radius
  openDogId.value = from.dog
  // Release after Vue has flushed, or the watcher fires with the flag already cleared and
  // pushes the state we just arrived at back onto the stack.
  nextTick(() => {
    applyingHistory = false
  })
})

// City is free text — debounce so we don't refetch per keystroke.
let cityTimer = null
watch(selectedCity, () => {
  clearTimeout(cityTimer)
  cityTimer = setTimeout(() => {
    loadSites()
    loadListings()
  }, 450)
})

// Clear a breed that newly chosen size/trait filters exclude.
watch([selectedSize, traits], () => {
  if (!selectedBreed.value) return
  const current = breeds.value.find((b) => b.slug === selectedBreed.value)
  if (current && !breedMatches(current, { size: selectedSize.value, traits: traits.value })) {
    selectedBreed.value = ''
  }
})

onMounted(() => {
  loadBreeds()
  loadSites()
  loadListings()
})
</script>

<template>
  <!-- Visually hidden until focused: the first Tab stop skips the nav and hero for keyboard
       and screen-reader users, who otherwise walk every chip to reach the dogs. -->
  <a
    href="#main"
    class="btn btn-primary btn-sm sr-only focus:not-sr-only focus:fixed focus:top-2 focus:left-2 focus:z-50"
  >
    Skip to content
  </a>

  <!-- Glass sticky nav: identity + global actions, nothing else. -->
  <nav class="bg-base-200/80 sticky top-0 z-40 backdrop-blur-md">
    <div class="mx-auto flex max-w-6xl items-center justify-between gap-3 px-4 py-2 sm:px-6">
      <!-- A link, like the guide's logo already is: there was no home link anywhere in this nav. -->
      <a href="/" class="flex items-center gap-2">
        <PuppyLogo class="h-9 w-9 shrink-0" />
        <span class="font-display text-xl font-semibold tracking-tight">PuppyFinder</span>
      </a>
      <div class="flex items-center gap-1">
        <!--
          Saving was one click and everywhere; retrieving was a 5,000px scroll and then an
          accordion. The count belongs where it is always visible.
        -->
        <button
          v-if="favorites.length || recent.length"
          type="button"
          class="btn btn-ghost btn-sm"
          :aria-label="`Your dogs — ${favorites.length} saved`"
          @click="savedOpen = true"
        >
          <Icon name="heart" class="text-primary/80 h-4 w-4" />
          <span v-if="favorites.length" class="badge badge-primary badge-sm">{{ favorites.length }}</span>
          <span class="hidden sm:inline">Your dogs</span>
        </button>
        <!-- A link, not a dialog. The guide is eight pages now, and the state this used to
             protect by staying in place is entirely in the query string — Back restores the
             same search. See DESIGN.md. -->
        <a href="/safe" class="btn btn-ghost btn-sm">
          <Icon name="shield-check" class="text-primary/80 h-4 w-4" />
          <span class="hidden sm:inline">Buy safely</span>
        </a>
        <ThemePicker />
      </div>
    </div>
  </nav>

  <main id="main" class="mx-auto max-w-6xl px-4 pt-6 pb-16 sm:px-6">
    <!-- The one place a filter change is *announced*. Sighted users watch the grid repaint;
         a screen reader heard nothing at all — no busy state, no new count, nothing. -->
    <p role="status" aria-live="polite" class="sr-only">{{ resultsAnnouncement }}</p>

    <!--
      Editorial hero: one headline doing the brand work, numeric trust under it.

      Mode-aware, because it wasn't: "Buy a puppy. Don't get scammed." sat above a grid of
      rescue dogs, and the subhead talked about which marketplaces vet their breeders while
      you were browsing shelters. The headline contradicted the page under it.
    -->
    <header class="mb-8 text-center">
      <h1 class="font-display mx-auto max-w-3xl text-4xl leading-[1.1] font-semibold tracking-tight sm:text-6xl">
        <template v-if="buying">
          Buy a puppy.
          <span class="text-primary">Don't get scammed.</span>
        </template>
        <template v-else-if="goal === 'both'">
          Adopt or buy.
          <span class="text-primary">Don't get scammed either way.</span>
        </template>
        <template v-else>
          Adopt a dog.
          <span class="text-primary">They're already waiting.</span>
        </template>
      </h1>
      <p class="text-base-content/70 mx-auto mt-3 max-w-xl text-base">
        <template v-if="buying">
          Which marketplaces actually vet their breeders, which ones have a complaint
          record, and the checks that catch a scam before you send a cent.
        </template>
        <template v-else-if="goal === 'both'">
          Live shelter dogs next to honestly rated breeder marketplaces — and the checks
          that catch a scam before you send a cent.
        </template>
        <template v-else>
          Real dogs from public shelter feeds — photo, age, size and the shelter's own phone
          number. No listing fees, no middlemen, and most are already vaccinated and neutered.
        </template>
      </p>
      <!--
        The clickable chips carry an arrow and an underline; the static one carries neither.
        Before this they were all `badge badge-outline` and visually identical, so three
        buttons sat in a row of four chips with nothing to say they did anything — and
        cursor-pointer only helps after you have already guessed.
      -->
      <div class="mt-4 flex flex-wrap justify-center gap-2">
        <button
          v-if="showBuy && verifiedBreedCount"
          type="button"
          class="badge badge-lg badge-outline hover:badge-primary cursor-pointer underline decoration-dotted underline-offset-2"
          @click="pricesOpen = true"
        >
          {{ verifiedBreedCount }} sourced price ranges →
        </button>
        <span v-if="buying" class="badge badge-lg badge-outline opacity-70">
          7 breeder marketplaces, honestly rated
        </span>
        <!-- Adopting: the honest headline number is coverage, and it is already computed. -->
        <span v-else-if="coverage.length" class="badge badge-lg badge-outline opacity-70">
          {{ liveCount }} dogs live across {{ coverage.length }}
          {{ coverage.length === 1 ? 'state' : 'states' }}
        </span>
        <a
          href="/safe"
          class="badge badge-lg badge-outline hover:badge-primary cursor-pointer underline decoration-dotted underline-offset-2"
        >
          <Icon name="shield-check" class="h-3.5 w-3.5" /> Scam-safety checklist →
        </a>
        <!-- Underlined like the others because it is clickable; no arrow, because it toggles
             the view rather than opening something. Hidden in both mode, where each half of
             its label would describe something already on the page. -->
        <button
          v-if="liveCount && goal !== 'both'"
          type="button"
          class="badge badge-lg cursor-pointer underline decoration-dotted underline-offset-2"
          :class="goal === 'adopt' ? 'badge-primary' : 'badge-outline hover:badge-primary'"
          @click="goal = goal === 'adopt' ? 'buy' : 'adopt'"
        >
          {{ goal === 'adopt' ? '🛍️ Or buy from a breeder' : `🤝 Or adopt (${liveCount} live)` }}
        </button>
      </div>
    </header>

    <!-- Mobile: filters live behind a toggle so dogs stay above the fold. -->
    <div class="mb-4 lg:hidden">
      <button
        type="button"
        class="btn btn-outline btn-block"
        :aria-expanded="filtersOpen"
        aria-controls="search-filters"
        @click="filtersOpen = !filtersOpen"
      >
        <Icon :name="filtersOpen ? 'close' : 'funnel'" class="h-4 w-4" />
        {{ filtersOpen ? 'Hide filters' : `Filters${activeChips.length ? ` (${activeChips.length})` : ''}` }}
      </button>
    </div>

    <div class="lg:grid lg:grid-cols-[320px_minmax(0,1fr)] lg:items-start lg:gap-8">
      <!-- Mobile: a full-screen sheet rather than a class-toggled block pushing the page
           down — it opens above the nav, scrolls on its own, and behaves as a dialog. -->
      <aside
        id="search-filters"
        ref="filtersPanel"
        aria-label="Search filters"
        :role="filtersOpen ? 'dialog' : undefined"
        :aria-modal="filtersOpen ? 'true' : undefined"
        class="lg:sticky lg:top-16 lg:max-h-[calc(100vh-5rem)] lg:overflow-y-auto"
        :class="filtersOpen
          ? 'drawer-sheet fixed inset-0 z-50 overflow-y-auto bg-base-200 p-4 lg:static lg:z-auto lg:bg-transparent lg:p-0'
          : 'hidden lg:block'"
      >
        <SearchHub
          v-model:breed="selectedBreed"
          v-model:state="selectedState"
          v-model:city="selectedCity"
          v-model:size="selectedSize"
          v-model:age="selectedAge"
          v-model:sex="selectedSex"
          v-model:traits="traits"
          v-model:good-with="goodWith"
          v-model:goal="goal"
          :breeds="breeds"
          :us-states="US_STATES"
          :coverage="coverage"
          :locating="locating"
          v-model:zip="zip"
          v-model:radius="radius"
          :zip-resolved="zipResolved"
          :zip-error="zipError"
          :result-count="showAdopt && !loadingListings ? rankedListings.length : null"
          @open-quiz="quizOpen = true"
          @clear="resetSearch"
          @near-me="locateMe"
          @close="filtersOpen = false"
        />
      </aside>

      <section>
        <form class="mb-2" @submit.prevent="runSmartSearch">
          <label class="input input-bordered flex w-full max-w-2xl items-center gap-2">
            <Icon name="search" class="h-4 w-4 opacity-50" />
            <input
              v-model="smartQuery"
              type="text"
              class="grow"
              :placeholder="buying
                ? 'Try “french bulldog” or “small apartment dog”'
                : 'Try “golden retriever puppy near seattle” or “small senior dog in MD”'"
            />
            <button type="submit" class="btn btn-primary btn-sm -mr-2">Search</button>
          </label>
        </form>
        <p v-if="smartHint" class="text-base-content/60 mb-3 flex items-center gap-1 text-xs">
          <Icon name="info" class="h-3.5 w-3.5 shrink-0" /> {{ smartHint }}
        </p>
        <div v-else class="mb-3" />

        <div v-if="activeChips.length" class="mb-4 flex flex-wrap items-center gap-1.5">
          <!-- The action lives in the accessible name, not only in a title tooltip: "Beagle ✕"
               announced bare reads as a state, not as the remove button it is. -->
          <button
            v-for="chip in activeChips"
            :key="chip.key"
            type="button"
            class="badge badge-primary badge-soft gap-1 py-3"
            :title="`Remove ${chip.label}`"
            :aria-label="`Remove filter: ${chip.label}`"
            @click="chip.clear()"
          >
            {{ chip.label }} <span aria-hidden="true">✕</span>
          </button>
          <button type="button" class="link text-xs opacity-60" @click="clearAllFilters">Clear all</button>
        </div>

        <!-- Buying: we have no breeder listings of our own, so the vetted-marketplace
             guide is the answer rather than a consolation prize below an empty grid. -->
        <!-- The buying path, in the order the decision actually happens: what it
             should cost → is this quote sane → where to look → who to trust. No
             marketplace publishes a feed, so there are no listings of our own to
             show; the useful thing we can give a buyer is the price floor and the
             vetting differences between sites. -->
        <template v-if="showBuy">
          <div class="flex flex-col gap-5" :class="showAdopt ? 'mb-10' : ''">
            <!-- One card: the range, the meter and the quote checker together. They were two
                 stacked cards, which duplicated the breed name and the "far below market" line
                 and put the answer a scroll away from the question. -->
            <BreedCost
              :breed="selectedBreedInfo"
              :breeds="breeds"
              :photo="breedPhoto"
              @pick-breed="selectedBreed = $event"
              @open-quiz="quizOpen = true"
              @open-prices="pricesOpen = true"
            />
            <!--
              The buying path in the order the decision happens: what should it cost, is this
              seller real, and only then what to do when they ask for more money. Vetting comes
              before the fee check because it happens before there is a fee to question.
            -->
            <SellerCheck />
            <!--
              Straight after "is this quote sane", because it is the same question one step
              later: the quote was fine and now they want $350 more. It needs no price range,
              so unlike the panel above it is never blank — and it is the only check here that
              reaches someone who has already paid.
            -->
            <FeeCheck />
            <!-- Redundant in both mode, where the dogs it points at are already on the page. -->
            <div
              v-if="listings.length && goal === 'buy'"
              class="alert alert-soft alert-info flex flex-wrap items-center justify-between gap-2 py-2 text-sm"
            >
              <span class="max-w-prose">
                🐶 <strong>{{ listings.length }}</strong> adoptable
                {{ listings.length === 1 ? 'dog matches' : 'dogs match' }} this search — usually
                already vaccinated and neutered, for a fraction of a breeder's price.
              </span>
              <button type="button" class="link" @click="goal = 'adopt'">See them</button>
            </div>
          </div>

          <!-- In both mode the adopt section below renders the one directory, with both kinds
               of site in it — two directories a scroll apart would each look like all there is. -->
          <template v-if="goal === 'buy'">
            <div v-if="error" role="alert" class="alert alert-error mt-6">{{ error }}</div>
            <ResultsFallback
              v-else
              :sites="sites"
              :wanted="wantedFilters"
              goal="buy"
              :breed-name="selectedBreedName"
              :state="selectedState"
              :coverage="coverage"
              :result-count="0"
            />
          </template>

        </template>

        <template v-if="showAdopt">
          <div class="mb-1 flex flex-wrap items-end justify-between gap-3">
            <h2 class="font-display flex items-center gap-3 text-2xl font-semibold tracking-tight">
              <img
                v-if="breedPhoto"
                :src="breedPhoto"
                :alt="selectedBreedName"
                class="ring-primary/40 h-12 w-12 shrink-0 rounded-full object-cover shadow ring-2"
              />
              <span>
                {{ resultsHeading }}
                <span v-if="selectedBreedName" class="text-primary">— {{ selectedBreedName }}s</span>
              </span>
            </h2>
            <!-- Also kept while a sort is set: narrowing to one result used to remove the only
                 control that could undo "Youngest first". -->
            <label v-if="rankedListings.length > 1 || sort" class="flex items-center gap-2 text-xs">
              <span class="font-bold tracking-wide uppercase opacity-60">Sort</span>
              <select v-model="sort" class="select select-bordered select-sm">
                <option v-for="s in SORTS" :key="s.value" :value="s.value">{{ s.label }}</option>
              </select>
            </label>
          </div>
          <p class="mb-2 max-w-prose text-sm text-base-content/60">
            Live from public shelter feeds{{ activeSources.length ? ` (${activeSources.join(', ')})` : '' }}
            — refreshed every few minutes.
          </p>

          <!--
            Collapsed on purpose. The line above answers "who"; this answers "why", which matters
            because RescueGroups is the name a reader is least likely to know while supplying most
            of the dogs. Kept honest about what the source cannot do, since coverage gaps and blank
            fields are the two things a reader will actually notice.
          -->
          <details class="collapse-arrow bg-base-200 collapse mb-5">
            <summary class="collapse-title py-2 text-sm font-semibold">
              Where these dogs come from
            </summary>
            <div class="collapse-content space-y-3 text-sm">
              <p class="max-w-prose">
                Two of the feeds are government open data, published directly by
                Montgomery County (MD) and King County (WA) animal services.
              </p>
              <p class="max-w-prose">
                The rest come from <strong>RescueGroups.org</strong>, a 501(c)(3) non-profit that
                has given animal rescues free and low-cost adoption software since 2002 —
                listing management, online forms, and the websites many small rescues run on.
                The rescue caring for the dog writes the listing themselves, and RescueGroups
                passes it on to over 200 adoption sites. That is also why some links here open a
                <em>rescuename</em>.rescuegroups.org page: RescueGroups hosts it for them.
              </p>
              <p class="max-w-prose">
                <strong>Why we use them.</strong> These are the rescues' own words about their own
                dogs, not a marketplace — nobody pays to be listed and nobody profits from the
                adoption, which is the opposite of the breeder classifieds this site warns you
                about. It is also the only route still open: Petfinder closed its public API in
                December 2025, Adopt-a-Pet's full feed is paid partners only, and just two county
                open-data feeds publish adoptable dogs nationwide.
              </p>
              <p class="max-w-prose">
                <strong>What that means for you.</strong> Coverage follows the rescues that happen
                to use RescueGroups, so it is uneven by state rather than complete. And because
                each rescue fills in its own listing, some dogs arrive with no photo or no size
                recorded — we show those anyway, marked as unknown, instead of hiding a real dog
                over a blank field. Adoption fees, hours and requirements are always the rescue's,
                not ours.
              </p>
            </div>
          </details>

          <div
            v-if="!listings.length && broadened"
            class="alert alert-soft alert-warning mb-4 flex flex-wrap items-center justify-between gap-2 py-2 text-sm"
          >
            <span>
              No dogs match your exact search — showing
              <strong>{{ broadened.listings.length }} dogs</strong> {{ broadened.note }} instead.
            </span>
            <button type="button" class="link" @click="clearAllFilters">Clear my filters</button>
          </div>
          <div
            v-if="profile && !sort"
            class="alert alert-soft alert-info mb-4 flex flex-wrap items-center justify-between gap-2 py-2 text-sm"
          >
            <span>✨ Sorted by fit to your quiz profile — best matches first.</span>
            <span class="flex gap-2">
              <button type="button" class="link" @click="quizOpen = true">Retake quiz</button>
              <button type="button" class="link opacity-70" @click="dropProfile">Clear profile</button>
            </span>
          </div>

          <div
            v-if="unconfirmedCount || strictMatch"
            class="alert alert-soft mb-4 flex flex-wrap items-center justify-between gap-2 py-2 text-sm"
          >
            <span v-if="unconfirmedCount">
              {{ unconfirmedCount }} of these {{ rankedListings.length }} are unconfirmed — the
              rescue didn't record {{ unconfirmedReason }}. They're included so you don't miss
              them.
            </span>
            <span v-else>Showing only dogs the shelter explicitly listed — some may be hidden.</span>
            <button type="button" class="link whitespace-nowrap" @click="strictMatch = !strictMatch">
              {{ strictMatch ? 'Include unlisted again' : 'Show only confirmed matches' }}
            </button>
          </div>

          <ul v-if="loadingListings" class="grid list-none gap-6 p-0 sm:grid-cols-2 xl:grid-cols-3" aria-hidden="true">
            <li v-for="n in 6" :key="n" class="card bg-base-100 overflow-hidden">
              <div class="skeleton h-44 rounded-none" />
              <div class="space-y-2 p-4">
                <div class="skeleton h-5 w-2/5" />
                <div class="skeleton h-4 w-4/5" />
                <div class="skeleton h-4 w-3/5" />
              </div>
            </li>
          </ul>
          <div v-else-if="listingsError" role="alert" class="alert alert-error flex flex-wrap items-center justify-between gap-2">
            <span>{{ listingsError }}</span>
            <button type="button" class="btn btn-sm" @click="loadListings">Try again</button>
          </div>
          <ul
            v-else-if="rankedListings.length"
            data-testid="dog-results"
            class="grid list-none gap-6 p-0 sm:grid-cols-2 xl:grid-cols-3"
          >
            <ListingCard
              v-for="l in visibleListings"
              :key="l.id"
              :listing="l"
              :favorite="favoriteIds.has(l.id)"
              :unconfirmed-note="unconfirmedNote(l)"
              @toggle-favorite="onToggleFavorite(l)"
              @open="openDog(l)"
            />
          </ul>
          <!-- The v-else of "are there results?", not of "are there more pages?" — it used to
               hang off the show-more button's v-if, so every search returning a single page of
               dogs rendered the grid and then this card claiming nothing matched, under the
               skeleton and the error alert too. -->
          <div v-else class="card bg-base-100 shadow-md">
            <div class="card-body items-center text-center">
              <span class="text-4xl">🐾</span>
              <p class="font-semibold">No live listings match your filters.</p>
              <p class="mx-auto max-w-prose text-sm opacity-70">
                Our own feeds only reach a few counties so far — the sites below cover the
                whole country.
              </p>
              <div class="flex flex-wrap justify-center gap-2">
                <button
                  v-for="area in coverage.filter((c) => c.state !== selectedState)"
                  :key="area.state"
                  type="button"
                  class="btn btn-outline btn-sm"
                  @click="selectedState = area.state"
                >
                  🐶 {{ area.count }} dogs in {{ area.state }}
                </button>
              </div>
            </div>
          </div>

          <!-- Names the remaining number rather than saying "load more": the count is the
               useful part, and hiding it would understate coverage. -->
          <div v-if="!loadingListings && !listingsError && moreCount" class="mt-6 text-center">
            <button type="button" class="btn btn-outline" @click="shownCount += PAGE">
              Show {{ Math.min(moreCount, PAGE) }} more
              {{ moreCount === 1 ? 'dog' : 'dogs' }}
              <span class="opacity-60">({{ moreCount }} left)</span>
            </button>
          </div>

          <div class="mt-6">
            <AlertSignup
              :breed="selectedBreed"
              :breed-name="selectedBreedName"
              :state="selectedState"
              :city="selectedCity"
              :size="selectedSize"
              :age="selectedAge"
            />
          </div>


          <div v-if="error" role="alert" class="alert alert-error mt-6">{{ error }}</div>
          <ResultsFallback
            v-else
            :sites="sites"
            :wanted="wantedFilters"
            :goal="goal"
            :breed-name="selectedBreedName"
            :state="selectedState"
            :coverage="coverage"
            :result-count="rankedListings.length"
          />
        </template>
      </section>
    </div>

    <DogDetail
      v-if="openDogId"
      :listing="openDogListing"
      :listing-id="openDogId"
      :favorite="favoriteIds.has(openDogId)"
      @close="closeDog"
      @toggle-favorite="openDogListing && onToggleFavorite(openDogListing)"
      @search-similar="closeDog(); clearAllFilters(); goal = 'adopt'"
    />
    <BreedQuiz
      v-if="quizOpen"
      @close="quizOpen = false"
      @select="pickQuizBreed"
      @profile-saved="applyProfileToFilters"
    />
    <SavedDogs
      v-if="savedOpen"
      :favorites="favorites"
      :recent="recent"
      @close="savedOpen = false"
      @open-dog="openDogId = $event"
      @unsave="onToggleFavorite($event)"
    />
    <SourcedPrices
      v-if="pricesOpen"
      :breeds="breeds"
      @close="pricesOpen = false"
      @pick-breed="selectedBreed = $event"
    />
  </main>

  <!--
    The safety guide's only link into the app used to be inside the modal, which nothing that
    doesn't click ever opens — so eight pages of the best writing here had no route in. These
    are real <a href>s on the default screen: a crawler can reach them, and a reader who wants
    to send one to someone has a URL to copy.
  -->
  <footer class="border-base-300 border-t">
    <div class="mx-auto max-w-6xl px-4 py-8 sm:px-6">
      <h2 class="mb-3 text-xs font-bold tracking-wide uppercase opacity-60">
        <a href="/safe" class="link link-hover">Buy &amp; adopt safely</a>
      </h2>
      <ul class="grid list-none gap-x-6 gap-y-1 p-0 text-sm sm:grid-cols-2 lg:grid-cols-4">
        <li v-for="s in SAFETY_SECTIONS" :key="s.slug">
          <a :href="safetyPath(s.slug)" class="link link-hover opacity-70">
            <span aria-hidden="true">{{ s.emoji }}</span> {{ s.title }}
          </a>
        </li>
      </ul>
      <!-- The scam-guide entrance pages. A page nothing links to is an orphan however good it
           is — the same rule that put the safety anchors above into this footer. -->
      <h2 class="mt-6 mb-3 text-xs font-bold tracking-wide uppercase opacity-60">Scam guides</h2>
      <ul class="grid list-none gap-x-6 gap-y-1 p-0 text-sm sm:grid-cols-2 lg:grid-cols-4">
        <li v-for="a in ARTICLES" :key="a.slug">
          <a :href="articlePath(a.slug)" class="link link-hover opacity-70">{{ a.h1 }}</a>
        </li>
        <li>
          <a href="/embed" class="link link-hover opacity-70">
            For rescues: a free scam-check widget
          </a>
        </li>
      </ul>
      <!-- No disclaimer here: ResultsFallback already ends every screen with one, and two
           near-identical ones stacked a scroll apart is how a caveat stops being read. -->
    </div>
  </footer>
</template>
