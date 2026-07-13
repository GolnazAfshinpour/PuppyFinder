<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import SearchHub from './components/SearchHub.vue'
import SiteCard from './components/SiteCard.vue'
import BreedQuiz from './components/BreedQuiz.vue'
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
const selectedBreed = ref('') // breed slug
const selectedState = ref('')
const goal = ref('both')
const quizOpen = ref(false)
const loadingSites = ref(true)
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

watch([selectedBreed, selectedState], loadSites)

onMounted(() => {
  loadBreeds()
  loadSites()
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
        <ThemePicker />
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
          v-model:goal="goal"
          :breeds="breeds"
          :us-states="US_STATES"
          :site-count="visibleSites.length"
          @open-all="openAll"
          @open-quiz="quizOpen = true"
        />
      </aside>

      <section>
        <h2 class="mb-5 text-2xl font-bold">
          Your matching sites
          <span v-if="selectedBreedName" class="text-primary">for {{ selectedBreedName }}s</span>
        </h2>
        <p v-if="loadingSites" class="text-center text-base-content/60">
          <span class="loading loading-dots loading-md" />
        </p>
        <div v-else-if="error" class="alert alert-error">{{ error }}</div>
        <ul v-else class="grid list-none gap-6 p-0 sm:grid-cols-2">
          <SiteCard v-for="site in visibleSites" :key="site.id" :site="site" />
        </ul>

        <p class="mt-10 text-center text-sm text-base-content/60">
          PuppyFinder links you directly to each site's own listings — always verify a breeder
          or rescue yourself before sending money.
        </p>
      </section>
    </div>

    <BreedQuiz v-if="quizOpen" @close="quizOpen = false" @select="pickQuizBreed" />
  </main>
</template>
