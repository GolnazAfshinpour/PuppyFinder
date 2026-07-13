<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import SearchHub from './components/SearchHub.vue'
import SiteCard from './components/SiteCard.vue'
import BreedQuiz from './components/BreedQuiz.vue'
import ListingsSection from './components/ListingsSection.vue'

const US_STATES = [
  'AL', 'AK', 'AZ', 'AR', 'CA', 'CO', 'CT', 'DE', 'FL', 'GA',
  'HI', 'ID', 'IL', 'IN', 'IA', 'KS', 'KY', 'LA', 'ME', 'MD',
  'MA', 'MI', 'MN', 'MS', 'MO', 'MT', 'NE', 'NV', 'NH', 'NJ',
  'NM', 'NY', 'NC', 'ND', 'OH', 'OK', 'OR', 'PA', 'RI', 'SC',
  'SD', 'TN', 'TX', 'UT', 'VT', 'VA', 'WA', 'WV', 'WI', 'WY',
]

const breeds = ref([])
const sites = ref([])
const listings = ref([])
const selectedBreed = ref('') // breed slug
const selectedState = ref('')
const goal = ref('both')
const quizOpen = ref(false)
const loadingSites = ref(true)
const loadingListings = ref(true)
const error = ref('')

const selectedBreedName = computed(
  () => breeds.value.find((b) => b.slug === selectedBreed.value)?.displayName ?? '',
)

const visibleSites = computed(() => {
  if (goal.value === 'adopt') return sites.value.filter((s) => s.kind === 'Adopt')
  if (goal.value === 'buy') return sites.value.filter((s) => s.kind !== 'Adopt')
  return sites.value
})

async function loadSites() {
  loadingSites.value = true
  error.value = ''
  try {
    const params = new URLSearchParams()
    if (selectedBreed.value) params.set('breed', selectedBreed.value)
    if (selectedState.value) params.set('state', selectedState.value)
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
  try {
    const params = new URLSearchParams()
    if (selectedBreedName.value) params.set('breed', selectedBreedName.value)
    if (selectedState.value) params.set('state', selectedState.value)
    const res = await fetch(`/api/listings${params.size ? `?${params}` : ''}`)
    if (res.ok) listings.value = await res.json()
  } catch {
    listings.value = []
  } finally {
    loadingListings.value = false
  }
}

async function loadBreeds() {
  try {
    const res = await fetch('/api/breeds')
    if (res.ok) breeds.value = await res.json()
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

watch([selectedBreed, selectedState], () => {
  loadSites()
  loadListings()
})

onMounted(() => {
  loadBreeds()
  loadSites()
  loadListings()
})
</script>

<template>
  <main class="page">
    <header class="header">
      <h1>🐶 PuppyFinder</h1>
      <p>Search once — we point you to the right page on every legit puppy site.</p>
    </header>

    <SearchHub
      v-model:breed="selectedBreed"
      v-model:state="selectedState"
      v-model:goal="goal"
      :breeds="breeds"
      :us-states="US_STATES"
      :site-count="visibleSites.length"
      @open-all="openAll"
      @open-quiz="quizOpen = true"
    />

    <p v-if="loadingSites" class="status">Loading sites…</p>
    <p v-else-if="error" class="status error">{{ error }}</p>
    <ul v-else class="site-grid">
      <SiteCard v-for="site in visibleSites" :key="site.id" :site="site" />
    </ul>

    <ListingsSection :listings="listings" :loading="loadingListings" />

    <p class="footnote">
      PuppyFinder links you directly to each site's own listings — always verify a breeder
      or rescue yourself before sending money.
    </p>

    <BreedQuiz v-if="quizOpen" @close="quizOpen = false" @select="pickQuizBreed" />
  </main>
</template>

<style scoped>
.page {
  max-width: 1080px;
  margin: 0 auto;
  padding: 3rem 1.5rem 4rem;
}

.header {
  text-align: center;
  margin-bottom: 2rem;
}

.header h1 {
  font-size: 2.5rem;
  margin: 0 0 0.35rem;
}

.header p {
  color: var(--text-muted);
  margin: 0;
  font-size: 1.05rem;
}

.status {
  text-align: center;
  color: var(--text-muted);
}

.status.error {
  color: var(--accent);
}

.site-grid {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 1.5rem;
}

.footnote {
  text-align: center;
  color: var(--text-muted);
  font-size: 0.85rem;
  margin-top: 2.5rem;
}

@media (max-width: 640px) {
  .header h1 {
    font-size: 2rem;
  }
}
</style>
