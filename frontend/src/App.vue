<script setup>
import { computed, onMounted, ref, watch } from 'vue'

const US_STATES = [
  'AL', 'AK', 'AZ', 'AR', 'CA', 'CO', 'CT', 'DE', 'FL', 'GA',
  'HI', 'ID', 'IL', 'IN', 'IA', 'KS', 'KY', 'LA', 'ME', 'MD',
  'MA', 'MI', 'MN', 'MS', 'MO', 'MT', 'NE', 'NV', 'NH', 'NJ',
  'NM', 'NY', 'NC', 'ND', 'OH', 'OK', 'OR', 'PA', 'RI', 'SC',
  'SD', 'TN', 'TX', 'UT', 'VT', 'VA', 'WA', 'WV', 'WI', 'WY',
]

const listings = ref([])
const sources = ref([])
const sites = ref([])
const breeds = ref([])
const selectedBreed = ref('')
const selectedState = ref('')
const loading = ref(true)
const error = ref('')
const brokenImages = ref(new Set())

async function loadListings() {
  error.value = ''
  try {
    const params = new URLSearchParams()
    if (selectedBreed.value) params.set('breed', selectedBreed.value)
    if (selectedState.value) params.set('state', selectedState.value)
    const query = params.size ? `?${params}` : ''
    const [listingsRes, sourcesRes] = await Promise.all([
      fetch(`/api/listings${query}`),
      fetch('/api/sources'),
    ])
    if (!listingsRes.ok) throw new Error(`API returned ${listingsRes.status}`)
    listings.value = await listingsRes.json()
    if (sourcesRes.ok) sources.value = await sourcesRes.json()
  } catch (e) {
    error.value = `Could not load listings — is the backend running? (${e.message})`
  } finally {
    loading.value = false
  }
}

async function loadExtras() {
  try {
    const [breedsRes, sitesRes] = await Promise.all([
      fetch('/api/breeds'),
      fetch('/api/sites'),
    ])
    if (breedsRes.ok) breeds.value = await breedsRes.json()
    if (sitesRes.ok) sites.value = await sitesRes.json()
  } catch {
    // dropdowns/footer are progressive enhancements over the listing grid
  }
}

// The breed dropdown holds display names because listings carry free-text breed names.
watch([selectedBreed, selectedState], loadListings)

const needsSetup = computed(
  () =>
    !loading.value &&
    !error.value &&
    sources.value.length > 0 &&
    sources.value.every((s) => !s.enabled),
)

const sourceErrors = computed(() =>
  sources.value.filter((s) => s.enabled && s.lastError),
)

function ageSex(listing) {
  return [listing.age, listing.sex].filter(Boolean).join(' • ')
}

function snippet(text) {
  if (!text) return 'No description provided — see the full listing.'
  return text.length > 140 ? `${text.slice(0, 140)}…` : text
}

function markImageBroken(id) {
  brokenImages.value = new Set(brokenImages.value).add(id)
}

onMounted(() => {
  loadListings()
  loadExtras()
})
</script>

<template>
  <main class="page">
    <header class="header">
      <h1>🐶 PuppyFinder</h1>
      <p>Every adoptable dog, one place — real listings aggregated live from source sites.</p>
    </header>

    <div v-if="!needsSetup" class="controls">
      <select v-model="selectedBreed">
        <option value="">All breeds</option>
        <option v-for="b in breeds" :key="b.slug" :value="b.displayName">{{ b.displayName }}</option>
      </select>
      <select v-model="selectedState">
        <option value="">Anywhere in the US</option>
        <option v-for="s in US_STATES" :key="s" :value="s">{{ s }}</option>
      </select>
    </div>

    <p v-if="loading" class="status">Fetching listings from source sites…</p>
    <p v-else-if="error" class="status error">{{ error }}</p>

    <div v-else-if="needsSetup" class="setup-card">
      <h2>🔑 One step to go live</h2>
      <p>
        PuppyFinder shows real adoptable-dog listings pulled from official APIs.
        Add at least one free API key:
      </p>
      <ol>
        <li>
          <strong>Petfinder</strong> (instant):
          <a href="https://www.petfinder.com/developers/" target="_blank" rel="noopener noreferrer">petfinder.com/developers</a>
          — grab the key <em>and</em> secret
        </li>
        <li>
          <strong>RescueGroups</strong> (email form):
          <a href="https://rescuegroups.org/services/adoptable-pet-data-api/" target="_blank" rel="noopener noreferrer">rescuegroups.org — Adoptable Pet Data API</a>
        </li>
      </ol>
      <p>
        Paste them into <code>backend/appsettings.Development.json</code> and restart the API —
        one key is enough to start.
      </p>
    </div>

    <template v-else>
      <p v-for="s in sourceErrors" :key="s.name" class="status error">
        {{ s.name }}: {{ s.lastError }}
      </p>
      <p v-if="listings.length === 0" class="status">No dogs match your filters.</p>

      <ul v-else class="listing-grid">
        <li v-for="dog in listings" :key="dog.id" class="card">
          <a :href="dog.listingUrl" target="_blank" rel="noopener noreferrer" class="card-media">
            <img
              v-if="dog.imageUrl && !brokenImages.has(dog.id)"
              :src="dog.imageUrl"
              :alt="`${dog.name}, ${dog.breed}`"
              loading="lazy"
              @error="markImageBroken(dog.id)"
            />
            <div v-else class="media-fallback">🐾</div>
            <span class="breed-badge">{{ dog.breed }}</span>
          </a>
          <div class="card-body">
            <div class="card-title">
              <a :href="dog.listingUrl" target="_blank" rel="noopener noreferrer">{{ dog.name }}</a>
              <span v-if="ageSex(dog)" class="age-sex">{{ ageSex(dog) }}</span>
            </div>
            <p class="description">{{ snippet(dog.description) }}</p>
            <div class="card-footer">
              <span class="location">📍 {{ dog.city }}, {{ dog.state }}</span>
              <a :href="dog.sourceUrl" target="_blank" rel="noopener noreferrer" class="source-chip">
                {{ dog.source }} ↗
              </a>
            </div>
          </div>
        </li>
      </ul>
    </template>

    <footer v-if="sites.length" class="sites-footer">
      <span class="sites-label">Browse the source sites directly:</span>
      <a
        v-for="site in sites"
        :key="site.id"
        :href="site.linkUrl"
        target="_blank"
        rel="noopener noreferrer"
        class="site-chip"
      >
        {{ site.name }} ↗
      </a>
    </footer>
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
  margin-bottom: 2.5rem;
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

.controls {
  display: flex;
  gap: 0.75rem;
  max-width: 560px;
  margin: 0 auto 2.5rem;
}

.controls select {
  flex: 1;
  padding: 0.7rem 1.1rem;
  border: 1px solid var(--border);
  border-radius: 999px;
  font-size: 0.95rem;
  font-family: inherit;
  background: var(--surface);
  color: var(--text-strong);
  box-shadow: var(--shadow);
  outline: none;
  transition: border-color 0.2s;
}

.controls select:focus {
  border-color: var(--accent);
}

.status {
  text-align: center;
  color: var(--text-muted);
}

.status.error {
  color: var(--accent);
}

.setup-card {
  max-width: 560px;
  margin: 0 auto;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  box-shadow: var(--shadow);
  padding: 2rem 2.25rem;
}

.setup-card h2 {
  margin-top: 0;
}

.setup-card ol {
  padding-left: 1.25rem;
}

.setup-card li {
  margin-bottom: 0.5rem;
}

.listing-grid {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 1.5rem;
}

.card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  overflow: hidden;
  box-shadow: var(--shadow);
  transition: box-shadow 0.25s, transform 0.25s;
  display: flex;
  flex-direction: column;
}

.card:hover {
  box-shadow: var(--shadow-hover);
  transform: translateY(-3px);
}

.card-media {
  position: relative;
  display: block;
  aspect-ratio: 3 / 2;
  background: var(--accent-soft);
}

.card-media img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

.media-fallback {
  width: 100%;
  height: 100%;
  display: grid;
  place-items: center;
  font-size: 3rem;
  background: linear-gradient(135deg, var(--accent-soft), #fdf6e3);
}

.breed-badge {
  position: absolute;
  left: 0.75rem;
  bottom: 0.75rem;
  padding: 0.3rem 0.8rem;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.92);
  color: var(--text-strong);
  font-size: 0.8rem;
  font-weight: 600;
  backdrop-filter: blur(4px);
  box-shadow: 0 1px 4px rgba(28, 24, 38, 0.15);
}

.card-body {
  padding: 1.1rem 1.25rem 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  flex: 1;
}

.card-title {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 0.5rem;
}

.card-title a {
  font-size: 1.15rem;
  font-weight: 650;
  color: var(--text-strong);
  text-decoration: none;
}

.card-title a:hover {
  color: var(--accent);
}

.age-sex {
  font-size: 0.8rem;
  color: var(--text-muted);
  white-space: nowrap;
}

.description {
  margin: 0;
  font-size: 0.9rem;
  color: var(--text);
  flex: 1;
}

.card-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.location {
  font-size: 0.85rem;
  color: var(--text-muted);
  white-space: nowrap;
}

.source-chip {
  font-size: 0.72rem;
  font-weight: 600;
  padding: 0.2rem 0.65rem;
  border-radius: 999px;
  background: var(--accent-soft);
  color: var(--accent);
  text-decoration: none;
}

.sites-footer {
  margin-top: 3rem;
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  justify-content: center;
  align-items: center;
}

.sites-label {
  font-size: 0.85rem;
  color: var(--text-muted);
  margin-right: 0.25rem;
}

.site-chip {
  font-size: 0.8rem;
  font-weight: 600;
  padding: 0.3rem 0.8rem;
  border-radius: 999px;
  border: 1px solid var(--border);
  background: var(--surface);
  color: var(--text-strong);
  text-decoration: none;
  transition: border-color 0.2s;
}

.site-chip:hover {
  border-color: var(--accent);
  color: var(--accent);
}

@media (max-width: 640px) {
  .header h1 {
    font-size: 2rem;
  }

  .controls {
    flex-direction: column;
  }
}
</style>
