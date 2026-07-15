<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { breedMatches } from './breedFilters.js'
import { fetchBreedImage } from './dogImages.js'
import { buildSearchQuery, parseSearchUrl } from './searchUrl.js'
import SearchHub from './components/SearchHub.vue'
import SiteCard from './components/SiteCard.vue'
import ListingCard from './components/ListingCard.vue'
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
const traits = ref(fromUrl.traits)
const goal = ref(fromUrl.goal)
const tab = ref(fromUrl.tab) // 'sites' = link-out cards, 'adopt' = live shelter listings
const quizOpen = ref(false)
const guideOpen = ref(false)
const loadingSites = ref(true)
const error = ref('')

// Live listings are kept fully separate from the site-search cards: they only
// load and render inside the "Adoptable now" tab.
const listings = ref([])
const sources = ref([])
const loadingListings = ref(false)
const listingsError = ref('')
const listingsStale = ref(true)

const selectedBreedName = computed(
  () => breeds.value.find((b) => b.slug === selectedBreed.value)?.displayName ?? '',
)

const visibleSites = computed(() => {
  if (goal.value === 'adopt') return sites.value.filter((s) => s.kind === 'Adopt')
  if (goal.value === 'buy') return sites.value.filter((s) => s.kind !== 'Adopt')
  return sites.value
})

// Filters the user has set — each card badges which of these its link carries.
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
  loadingSites.value = true
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
    error.value = `Could not load sites — is the backend running? (${e.message})`
  } finally {
    loadingSites.value = false
  }
}

async function loadListings() {
  loadingListings.value = true
  listingsError.value = ''
  try {
    const params = new URLSearchParams()
    if (selectedBreed.value) params.set('breed', selectedBreed.value)
    if (selectedState.value) params.set('state', selectedState.value)
    if (selectedCity.value.trim() && selectedState.value) params.set('city', selectedCity.value.trim())
    if (selectedSize.value) params.set('size', selectedSize.value)
    const [listRes, srcRes] = await Promise.all([
      fetch(`/api/listings${params.size ? `?${params}` : ''}`),
      sources.value.length ? Promise.resolve(null) : fetch('/api/sources'),
    ])
    if (!listRes.ok) throw new Error(`API returned ${listRes.status}`)
    listings.value = await listRes.json()
    if (srcRes?.ok) sources.value = await srcRes.json()
    listingsStale.value = false
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
    // hub still works without the dropdown contents
  }
}

function openAll() {
  for (const site of visibleSites.value) {
    window.open(site.linkUrl, '_blank', 'noopener')
  }
}

function pickQuizBreed(slug) {
  selectedBreed.value = slug
  quizOpen.value = false
}

watch([selectedBreed, selectedState], loadSites)

// Refresh listings when their filters change — immediately if the adopt tab is
// open, otherwise lazily on the next tab switch.
watch([selectedBreed, selectedState, selectedSize], () => {
  listingsStale.value = true
  if (tab.value === 'adopt') loadListings()
})
watch(tab, () => {
  if (tab.value === 'adopt' && listingsStale.value) loadListings()
})

// Keep the address bar in sync (replace, not push — no history spam).
watch([selectedBreed, selectedState, selectedCity, selectedSize, traits, goal, tab], () => {
  const query = buildSearchQuery({
    breed: selectedBreed.value,
    state: selectedState.value,
    city: selectedCity.value,
    size: selectedSize.value,
    traits: traits.value,
    goal: goal.value,
    tab: tab.value,
  })
  history.replaceState(null, '', query ? `?${query}` : window.location.pathname)
})

// City is free text — debounce so we don't refetch per keystroke.
let cityTimer = null
watch(selectedCity, () => {
  clearTimeout(cityTimer)
  cityTimer = setTimeout(() => {
    loadSites()
    listingsStale.value = true
    if (tab.value === 'adopt') loadListings()
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
  if (tab.value === 'adopt') loadListings()
})
</script>

<template>
  <main class="mx-auto max-w-6xl px-4 pt-3 pb-16 sm:px-6">
    <header class="mb-6">
      <div class="flex items-center justify-between gap-3">
        <div class="flex items-center gap-3">
          <PuppyLogo class="h-16 w-16 shrink-0 drop-shadow-md sm:h-20 sm:w-20" />
          <div>
            <h1 class="font-display text-3xl leading-none font-semibold tracking-wide sm:text-4xl">
              PuppyFinder
            </h1>
            <p class="font-display text-lg text-base-content/70 sm:text-xl">
              Find your magical companion
            </p>
          </div>
        </div>
        <div class="flex items-center gap-2">
          <button type="button" class="btn btn-ghost btn-sm" @click="guideOpen = true">
            🛡️ <span class="hidden sm:inline">Buy safely</span>
          </button>
          <ThemePicker />
        </div>
      </div>
      <ul class="steps mt-3 hidden w-full text-xs sm:grid">
        <li class="step step-primary">Pick a breed — or take the quiz</li>
        <li class="step step-primary">Choose adopt or buy</li>
        <li class="step step-primary">Jump to the right page on every site</li>
      </ul>
    </header>

    <div class="lg:grid lg:grid-cols-[320px_minmax(0,1fr)] lg:items-start lg:gap-8">
      <aside class="mb-8 lg:sticky lg:top-4 lg:mb-0">
        <SearchHub
          v-model:breed="selectedBreed"
          v-model:state="selectedState"
          v-model:city="selectedCity"
          v-model:size="selectedSize"
          v-model:traits="traits"
          v-model:goal="goal"
          :breeds="breeds"
          :us-states="US_STATES"
          :site-count="visibleSites.length"
          @open-all="openAll"
          @open-quiz="quizOpen = true"
        />
      </aside>

      <section>
        <div role="tablist" class="tabs tabs-box mb-5 w-fit">
          <button
            role="tab"
            class="tab gap-1"
            :class="{ 'tab-active': tab === 'sites' }"
            @click="tab = 'sites'"
          >
            🔗 Search the sites
          </button>
          <button
            role="tab"
            class="tab gap-1"
            :class="{ 'tab-active': tab === 'adopt' }"
            @click="tab = 'adopt'"
          >
            🐶 Adoptable now
          </button>
        </div>

        <template v-if="tab === 'sites'">
          <h2 class="mb-5 flex items-center gap-3 text-2xl font-bold">
            <img
              v-if="breedPhoto"
              :src="breedPhoto"
              :alt="selectedBreedName"
              class="ring-primary/40 h-12 w-12 shrink-0 rounded-full object-cover shadow ring-2"
            />
            <span>
              Your matching sites
              <span v-if="selectedBreedName" class="text-primary">for {{ selectedBreedName }}s</span>
            </span>
          </h2>
          <p v-if="loadingSites" class="text-center text-base-content/60">
            <span class="loading loading-dots loading-md" />
          </p>
          <div v-else-if="error" class="alert alert-error">{{ error }}</div>
          <ul v-else class="grid list-none gap-6 p-0 sm:grid-cols-2">
            <SiteCard
              v-for="site in visibleSites"
              :key="site.id"
              :site="site"
              :wanted="wantedFilters"
              @open-guide="guideOpen = true"
            />
          </ul>

          <p class="mt-10 text-center text-sm text-base-content/60">
            PuppyFinder links you directly to each site's own listings — always verify a breeder
            or rescue yourself before sending money.
          </p>
        </template>

        <template v-else>
          <h2 class="mb-1 text-2xl font-bold">
            Adoptable dogs right now
            <span v-if="selectedBreedName" class="text-primary">— {{ selectedBreedName }}s</span>
          </h2>
          <p class="mb-1 text-sm text-base-content/60">
            Live from public shelter feeds{{ activeSources.length ? ` (${activeSources.join(', ')})` : '' }}
            — refreshed every few minutes. Coverage grows as more open-data feeds are added.
          </p>
          <p v-if="traits.length" class="mb-5 text-xs text-base-content/50">
            ℹ️ Must-have traits narrow the breed picker only — shelter feeds don't include
            temperament data, so they aren't applied here.
          </p>
          <div v-else class="mb-5" />
          <p v-if="loadingListings" class="text-center text-base-content/60">
            <span class="loading loading-dots loading-md" />
          </p>
          <div v-else-if="listingsError" class="alert alert-error">{{ listingsError }}</div>
          <ul v-else-if="listings.length" class="grid list-none gap-6 p-0 sm:grid-cols-2 xl:grid-cols-3">
            <ListingCard v-for="l in listings" :key="l.id" :listing="l" />
          </ul>
          <div v-else class="card bg-base-100 shadow-md">
            <div class="card-body items-center text-center">
              <span class="text-4xl">🐾</span>
              <p class="font-semibold">No live listings match your filters yet.</p>
              <p class="text-sm opacity-70">
                Our shelter feeds currently cover Maryland (Montgomery County) and Washington
                (King County). Try clearing the breed or state — or use the site search tab,
                which covers the whole country.
              </p>
              <button type="button" class="btn btn-outline btn-sm" @click="tab = 'sites'">
                ← Back to site search
              </button>
            </div>
          </div>
        </template>
      </section>
    </div>

    <BreedQuiz v-if="quizOpen" @close="quizOpen = false" @select="pickQuizBreed" />
    <SafetyGuide v-if="guideOpen" @close="guideOpen = false" />
  </main>
</template>
