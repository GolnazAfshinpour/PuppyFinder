<script setup>
import { onMounted, ref, watch } from 'vue'

const US_STATES = [
  'AL', 'AK', 'AZ', 'AR', 'CA', 'CO', 'CT', 'DE', 'FL', 'GA',
  'HI', 'ID', 'IL', 'IN', 'IA', 'KS', 'KY', 'LA', 'ME', 'MD',
  'MA', 'MI', 'MN', 'MS', 'MO', 'MT', 'NE', 'NV', 'NH', 'NJ',
  'NM', 'NY', 'NC', 'ND', 'OH', 'OK', 'OR', 'PA', 'RI', 'SC',
  'SD', 'TN', 'TX', 'UT', 'VT', 'VA', 'WA', 'WV', 'WI', 'WY',
]

const CATEGORY_LABELS = {
  BreederMarketplace: 'Breeder Marketplace',
  AdoptionPlatform: 'Adoption Platform',
  Rescue: 'Rescue',
  Shelter: 'Shelter',
}

const breeds = ref([])
const sites = ref([])
const selectedBreed = ref('')
const selectedState = ref('')
const loading = ref(true)
const error = ref('')

async function loadSites() {
  error.value = ''
  try {
    const params = new URLSearchParams()
    if (selectedBreed.value) params.set('breed', selectedBreed.value)
    if (selectedState.value) params.set('state', selectedState.value)
    const query = params.size ? `?${params}` : ''
    const res = await fetch(`/api/sites${query}`)
    if (!res.ok) throw new Error(`API returned ${res.status}`)
    sites.value = await res.json()
  } catch (e) {
    error.value = `Could not load sites — is the backend running? (${e.message})`
  } finally {
    loading.value = false
  }
}

async function loadBreeds() {
  try {
    const res = await fetch('/api/breeds')
    if (res.ok) breeds.value = await res.json()
  } catch {
    // breed filter is optional; site links still work without it
  }
}

watch([selectedBreed, selectedState], loadSites)

onMounted(() => {
  loadSites()
  loadBreeds()
})
</script>

<template>
  <main class="page">
    <header class="header">
      <h1>🐶 PuppyFinder</h1>
      <p>Pick a breed — jump straight to the listings on every major legit site.</p>
    </header>

    <div class="controls">
      <select v-model="selectedBreed">
        <option value="">All breeds</option>
        <option v-for="b in breeds" :key="b.slug" :value="b.slug">{{ b.displayName }}</option>
      </select>
      <select v-model="selectedState">
        <option value="">Anywhere in the US</option>
        <option v-for="s in US_STATES" :key="s" :value="s">{{ s }}</option>
      </select>
    </div>

    <p v-if="loading" class="status">Loading sites…</p>
    <p v-else-if="error" class="status error">{{ error }}</p>

    <ul v-else class="site-grid">
      <li v-for="site in sites" :key="site.id" class="card">
        <div class="card-body">
          <div class="card-title">
            <h2>{{ site.name }}</h2>
            <span class="tag">{{ CATEGORY_LABELS[site.category] ?? site.category }}</span>
          </div>
          <p class="description">{{ site.description }}</p>
          <a class="cta" :href="site.linkUrl" target="_blank" rel="noopener noreferrer">
            {{ site.linkLabel }} ↗
          </a>
        </div>
      </li>
    </ul>

    <p class="footnote">
      PuppyFinder links you directly to each site's own listings — always verify a breeder
      or rescue yourself before sending money.
    </p>
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

.site-grid {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 1.5rem;
}

.card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  box-shadow: var(--shadow);
  transition: box-shadow 0.25s, transform 0.25s;
  display: flex;
}

.card:hover {
  box-shadow: var(--shadow-hover);
  transform: translateY(-3px);
}

.card-body {
  padding: 1.4rem 1.5rem 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 0.7rem;
  flex: 1;
}

.card-title {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
}

.card-title h2 {
  font-size: 1.15rem;
  margin: 0;
}

.tag {
  font-size: 0.72rem;
  font-weight: 600;
  padding: 0.2rem 0.65rem;
  border-radius: 999px;
  background: var(--accent-soft);
  color: var(--accent);
  white-space: nowrap;
}

.description {
  margin: 0;
  font-size: 0.9rem;
  color: var(--text);
  flex: 1;
}

.cta {
  display: block;
  text-align: center;
  padding: 0.6rem 1rem;
  border-radius: 999px;
  background: var(--accent);
  color: #fff;
  font-size: 0.9rem;
  font-weight: 600;
  text-decoration: none;
  transition: filter 0.2s;
}

.cta:hover {
  filter: brightness(1.1);
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

  .controls {
    flex-direction: column;
  }
}
</style>
