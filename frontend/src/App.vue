<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { TRAITS, breedMatches } from './breedFilters.js'
import { clearProfile, loadProfile, rankListings } from './adopterProfile.js'
import { loadFavorites, loadRecent, recordViewed, toggleFavorite } from './favorites.js'
import { parseQuery } from './smartSearch.js'
import { fetchBreedImage } from './dogImages.js'
import { buildSearchQuery, parseSearchUrl } from './searchUrl.js'
import SearchHub from './components/SearchHub.vue'
import ResultsFallback from './components/ResultsFallback.vue'
import ListingCard from './components/ListingCard.vue'
import DogDetail from './components/DogDetail.vue'
import AlertSignup from './components/AlertSignup.vue'
import BreedQuiz from './components/BreedQuiz.vue'
import SafetyGuide from './components/SafetyGuide.vue'
import ThemePicker from './components/ThemePicker.vue'
import PuppyLogo from './components/PuppyLogo.vue'

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
const traits = ref(fromUrl.traits)
const goal = ref(fromUrl.goal)
const sort = ref(fromUrl.sort)
const openDogId = ref(fromUrl.dog) // '' = no detail view open
const quizOpen = ref(false)
const guideOpen = ref(false)
const filtersOpen = ref(false) // mobile-only filter drawer state
const error = ref('')

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

const selectedBreedName = computed(
  () => breeds.value.find((b) => b.slug === selectedBreed.value)?.displayName ?? '',
)

const liveCount = computed(() => coverage.value.reduce((total, c) => total + c.count, 0))

// Buying means the site directory leads: no breeder marketplace publishes a
// feed, so we have no listings of our own to show — say so instead of an
// empty grid.
const buying = computed(() => goal.value === 'buy')

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
    selectedAge.value && !listing.ageGroup && 'age',
  ].filter(Boolean)
  if (!missing.length) return ''
  return `This shelter didn't list a ${missing.join(' or ')} — shown so you don't miss them.`
}

// Active filters as removable chips above the results (table-stakes search UX).
const TRAIT_LABELS = { kids: 'Good with kids', apartment: 'Apartment-friendly', lowshed: 'Low-shedding' }
const activeChips = computed(() => {
  const chips = []
  if (selectedAge.value) chips.push({ key: 'age', label: selectedAge.value, clear: () => (selectedAge.value = '') })
  if (selectedSize.value) chips.push({ key: 'size', label: `Size: ${selectedSize.value}`, clear: () => (selectedSize.value = '') })
  for (const t of traits.value) {
    chips.push({ key: `trait-${t}`, label: TRAIT_LABELS[t] ?? t, clear: () => (traits.value = traits.value.filter((x) => x !== t)) })
  }
  if (selectedBreed.value) chips.push({ key: 'breed', label: selectedBreedName.value || selectedBreed.value, clear: () => (selectedBreed.value = '') })
  if (selectedState.value) chips.push({ key: 'state', label: selectedState.value, clear: () => (selectedState.value = '') })
  if (selectedCity.value.trim() && selectedState.value) chips.push({ key: 'city', label: selectedCity.value.trim(), clear: () => (selectedCity.value = '') })
  return chips
})
function clearAllFilters() {
  selectedBreed.value = ''
  selectedState.value = ''
  selectedCity.value = ''
  selectedSize.value = ''
  selectedAge.value = ''
  traits.value = []
}

// The search card's "Clear filters" is a full reset — including the adopt/buy goal.
function resetSearch() {
  clearAllFilters()
  goal.value = 'both'
}

// Shelters leave size and age blank constantly, so those filters keep unknowns by
// default (see ListingQuery) — this lets someone who wants a hard match say so.
// Deliberately not in the shareable URL: it's a refinement of another filter, and
// it only appears when there's actually something to refine.
const strictMatch = ref(false)
const unconfirmedCount = computed(() => rankedListings.value.filter((l) => l.unconfirmed).length)

const SORTS = [
  { value: '', label: 'Best match' },
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

// "Near me": browser geolocation reverse-geocoded to state + city (keyless
// bigdatacloud endpoint), with a manual fallback message on denial.
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

async function loadSites() {
  error.value = ''
  try {
    const params = new URLSearchParams()
    if (selectedBreed.value) params.set('breed', selectedBreed.value)
    if (selectedState.value) params.set('state', selectedState.value)
    if (selectedCity.value.trim() && selectedState.value) params.set('city', selectedCity.value.trim())
    const res = await fetch(`/api/sites${params.size ? `?${params}` : ''}`)
    if (!res.ok) throw new Error(`API returned ${res.status}`)
    sites.value = await res.json()
  } catch (e) {
    error.value = `Could not load the site directory — is the backend running? (${e.message})`
  }
}

function listingParams(overrides = {}) {
  const values = {
    breed: selectedBreed.value,
    state: selectedState.value,
    city: selectedCity.value.trim() && selectedState.value ? selectedCity.value.trim() : '',
    size: selectedSize.value,
    age: selectedAge.value,
    sort: sort.value,
    includeUnlisted: strictMatch.value ? 'false' : '',
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
    { overrides: { age: '' }, note: 'of any age', applies: () => selectedAge.value },
    { overrides: { breed: '' }, note: 'of any breed', applies: () => selectedBreed.value },
    { overrides: { state: '', city: '' }, note: 'across all states with live feeds', applies: () => selectedState.value },
    { overrides: { breed: '', state: '', city: '', size: '', age: '' }, note: 'available right now (all filters relaxed)', applies: () => true },
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

async function loadListings() {
  loadingListings.value = true
  listingsError.value = ''
  try {
    const [listRes, srcRes, covRes] = await Promise.all([
      fetchListings(listingParams()),
      sources.value.length ? Promise.resolve(null) : fetch('/api/sources'),
      coverage.value.length ? Promise.resolve(null) : fetch('/api/coverage'),
    ])
    listings.value = listRes
    broadened.value = listRes.length ? null : await broadenSearch()
    if (srcRes?.ok) sources.value = await srcRes.json()
    if (covRes?.ok) coverage.value = await covRes.json()
  } catch (e) {
    listingsError.value = `Could not load listings (${e.message})`
  } finally {
    loadingListings.value = false
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
watch([selectedBreed, selectedState, selectedSize, selectedAge, sort, strictMatch], loadListings)

// Keep the address bar in sync (replace, not push — no history spam).
watch([selectedBreed, selectedState, selectedCity, selectedSize, selectedAge, traits, goal, sort, openDogId], () => {
  const query = buildSearchQuery({
    breed: selectedBreed.value,
    state: selectedState.value,
    city: selectedCity.value,
    size: selectedSize.value,
    age: selectedAge.value,
    traits: traits.value,
    goal: goal.value,
    sort: sort.value,
    dog: openDogId.value,
  })
  history.replaceState(null, '', query ? `?${query}` : window.location.pathname)
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
  <!-- Glass sticky nav: identity + global actions, nothing else. -->
  <nav class="bg-base-200/80 sticky top-0 z-40 backdrop-blur-md">
    <div class="mx-auto flex max-w-6xl items-center justify-between gap-3 px-4 py-2 sm:px-6">
      <div class="flex items-center gap-2">
        <PuppyLogo class="h-9 w-9 shrink-0" />
        <span class="font-display text-xl font-semibold tracking-tight">PuppyFinder</span>
      </div>
      <div class="flex items-center gap-1">
        <button type="button" class="btn btn-ghost btn-sm" @click="guideOpen = true">
          🛡️ <span class="hidden sm:inline">Buy safely</span>
        </button>
        <ThemePicker />
      </div>
    </div>
  </nav>

  <main class="mx-auto max-w-6xl px-4 pt-6 pb-16 sm:px-6">
    <!-- Editorial hero: one headline doing the brand work, numeric trust under it. -->
    <header class="mb-8 text-center">
      <h1 class="font-display mx-auto max-w-2xl text-3xl leading-[1.1] font-semibold tracking-tight sm:text-5xl">
        Find your dog.
        <span class="text-primary">Not another website.</span>
      </h1>
      <p class="text-base-content/70 mx-auto mt-3 max-w-xl text-base">
        Real adoptable dogs from live shelter feeds, plus an honest guide to the 14 sites
        worth using for everywhere we don't reach yet.
      </p>
      <div class="mt-4 flex flex-wrap justify-center gap-2">
        <button
          type="button"
          class="badge badge-lg cursor-pointer"
          :class="selectedAge === 'Puppy' ? 'badge-primary' : 'badge-outline'"
          @click="selectedAge = selectedAge === 'Puppy' ? '' : 'Puppy'"
        >
          🐶 Puppies only
        </button>
        <button type="button" class="badge badge-lg badge-outline cursor-pointer" @click="locateMe">
          {{ locating ? 'Locating…' : '📍 Near me' }}
        </button>
        <span v-if="liveCount" class="badge badge-lg badge-outline">{{ liveCount }} live dogs</span>
        <span class="badge badge-lg badge-outline">Scam-safety guide built in</span>
      </div>
    </header>

    <!-- Mobile: filters live behind a toggle so dogs stay above the fold. -->
    <div class="mb-4 lg:hidden">
      <button type="button" class="btn btn-outline btn-block" @click="filtersOpen = !filtersOpen">
        {{ filtersOpen ? '✕ Hide filters' : `⚙︎ Filters${activeChips.length ? ` (${activeChips.length})` : ''}` }}
      </button>
    </div>

    <div class="lg:grid lg:grid-cols-[320px_minmax(0,1fr)] lg:items-start lg:gap-8">
      <aside
        class="mb-8 lg:sticky lg:top-16 lg:mb-0 lg:max-h-[calc(100vh-5rem)] lg:overflow-y-auto"
        :class="filtersOpen ? 'block' : 'hidden lg:block'"
      >
        <SearchHub
          v-model:breed="selectedBreed"
          v-model:state="selectedState"
          v-model:city="selectedCity"
          v-model:size="selectedSize"
          v-model:age="selectedAge"
          v-model:traits="traits"
          v-model:goal="goal"
          :breeds="breeds"
          :us-states="US_STATES"
          :coverage="coverage"
          :locating="locating"
          @open-quiz="quizOpen = true"
          @clear="resetSearch"
          @near-me="locateMe"
        />
      </aside>

      <section>
        <form class="mb-2" @submit.prevent="runSmartSearch">
          <label class="input input-bordered flex w-full max-w-2xl items-center gap-2">
            <svg class="h-4 w-4 opacity-50" viewBox="0 0 24 24" fill="none" stroke="currentColor"
              stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="11" cy="11" r="7" />
              <path d="m21 21-4.3-4.3" />
            </svg>
            <input
              v-model="smartQuery"
              type="text"
              class="grow"
              placeholder="Try “golden retriever puppy near seattle” or “small senior dog in MD”"
            />
            <button type="submit" class="btn btn-primary btn-sm -mr-2">Search</button>
          </label>
        </form>
        <p v-if="smartHint" class="text-base-content/60 mb-3 text-xs">ℹ️ {{ smartHint }}</p>
        <div v-else class="mb-3" />

        <div v-if="activeChips.length" class="mb-4 flex flex-wrap items-center gap-1.5">
          <button
            v-for="chip in activeChips"
            :key="chip.key"
            type="button"
            class="badge badge-primary badge-soft gap-1 py-3"
            :title="`Remove ${chip.label}`"
            @click="chip.clear()"
          >
            {{ chip.label }} ✕
          </button>
          <button type="button" class="link text-xs opacity-60" @click="clearAllFilters">Clear all</button>
        </div>

        <!-- Buying: we have no breeder listings of our own, so the vetted-marketplace
             guide is the answer rather than a consolation prize below an empty grid. -->
        <template v-if="buying">
          <ResultsFallback
            :sites="sites"
            :wanted="wantedFilters"
            goal="buy"
            :breed-name="selectedBreedName"
            :state="selectedState"
            :coverage="coverage"
            :result-count="0"
            flush
            @open-guide="guideOpen = true"
          />
          <div v-if="listings.length" class="alert alert-soft alert-info mt-6 flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
            <span>
              🐶 <strong>{{ listings.length }}</strong> adoptable
              {{ listings.length === 1 ? 'dog' : 'dogs' }} also match this search — usually
              vaccinated, neutered, and a fraction of the price.
            </span>
            <button type="button" class="link" @click="goal = 'adopt'">See them</button>
          </div>
        </template>

        <template v-else>
          <div class="mb-1 flex flex-wrap items-end justify-between gap-3">
            <h2 class="flex items-center gap-3 text-2xl font-bold">
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
            <label v-if="rankedListings.length > 1" class="flex items-center gap-2 text-xs">
              <span class="font-bold tracking-wide uppercase opacity-60">Sort</span>
              <select v-model="sort" class="select select-bordered select-sm">
                <option v-for="s in SORTS" :key="s.value" :value="s.value">{{ s.label }}</option>
              </select>
            </label>
          </div>
          <p class="mb-5 text-sm text-base-content/60">
            Live from public shelter feeds{{ activeSources.length ? ` (${activeSources.join(', ')})` : '' }}
            — refreshed every few minutes.
          </p>

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
              {{ unconfirmedCount }} of these
              {{ rankedListings.length }} didn't have a
              {{ selectedSize && selectedAge ? 'size or age' : selectedSize ? 'size' : 'age' }}
              listed by the shelter. They're included so you don't miss them.
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
          <div v-else-if="listingsError" class="alert alert-error">{{ listingsError }}</div>
          <ul
            v-else-if="rankedListings.length"
            data-testid="dog-results"
            class="grid list-none gap-6 p-0 sm:grid-cols-2 xl:grid-cols-3"
          >
            <ListingCard
              v-for="l in rankedListings"
              :key="l.id"
              :listing="l"
              :favorite="favoriteIds.has(l.id)"
              :unconfirmed-note="unconfirmedNote(l)"
              @toggle-favorite="onToggleFavorite(l)"
              @open="openDog(l)"
            />
          </ul>
          <div v-else class="card bg-base-100 shadow-md">
            <div class="card-body items-center text-center">
              <span class="text-4xl">🐾</span>
              <p class="font-semibold">No live listings match your filters.</p>
              <p class="text-sm opacity-70">
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
              <div v-if="recent.length" class="mt-4 w-full text-left">
                <p class="mb-2 text-sm font-semibold">Dogs you looked at recently:</p>
                <ul class="space-y-2">
                  <li v-for="r in recent.slice(0, 4)" :key="r.id" class="flex items-center gap-3 text-sm">
                    <img v-if="r.imageUrl" :src="r.imageUrl" :alt="r.name" referrerpolicy="no-referrer"
                      class="h-9 w-9 rounded-lg object-cover" />
                    <span v-else class="bg-base-300 grid h-9 w-9 place-items-center rounded-lg">🐶</span>
                    <span class="min-w-0 flex-1 truncate">{{ r.name }} — {{ r.breed }}</span>
                    <a :href="r.listingUrl" target="_blank" rel="noopener noreferrer" class="link whitespace-nowrap">
                      Open ↗
                    </a>
                  </li>
                </ul>
              </div>
            </div>
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

          <details v-if="favorites.length" class="collapse-arrow border-base-300 bg-base-100 collapse mt-4 border">
            <summary class="collapse-title font-semibold">❤️ Your saved dogs ({{ favorites.length }})</summary>
            <div class="collapse-content">
              <ul class="space-y-2">
                <li v-for="f in favorites" :key="f.id" class="flex items-center gap-3 text-sm">
                  <img v-if="f.imageUrl" :src="f.imageUrl" :alt="f.name" referrerpolicy="no-referrer"
                    class="h-11 w-11 rounded-lg object-cover" />
                  <span v-else class="bg-base-300 grid h-11 w-11 place-items-center rounded-lg">🐶</span>
                  <span class="min-w-0 flex-1 truncate">
                    <strong>{{ f.name }}</strong> — {{ f.breed }} · {{ f.city }}, {{ f.state }}
                  </span>
                  <a :href="f.listingUrl" target="_blank" rel="noopener noreferrer" class="link whitespace-nowrap">
                    Open ↗
                  </a>
                  <button type="button" class="btn btn-ghost btn-xs" title="Remove from saved" @click="onToggleFavorite(f)">
                    ✕
                  </button>
                </li>
              </ul>
            </div>
          </details>

          <div v-if="error" class="alert alert-error mt-6">{{ error }}</div>
          <ResultsFallback
            v-else
            :sites="sites"
            :wanted="wantedFilters"
            :goal="goal"
            :breed-name="selectedBreedName"
            :state="selectedState"
            :coverage="coverage"
            :result-count="rankedListings.length"
            @open-guide="guideOpen = true"
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
      @search-similar="closeDog(); clearAllFilters()"
    />
    <BreedQuiz
      v-if="quizOpen"
      @close="quizOpen = false"
      @select="pickQuizBreed"
      @profile-saved="applyProfileToFilters"
    />
    <SafetyGuide v-if="guideOpen" @close="guideOpen = false" />
  </main>
</template>
