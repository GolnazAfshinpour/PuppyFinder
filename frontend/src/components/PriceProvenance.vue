<script setup>
import { computed, ref, watch } from 'vue'

const props = defineProps({
  breed: { type: Object, default: null }, // entry from /api/breeds
})

// Provenance is fetched lazily — it's a second request, and only worth making once
// someone actually has a breed selected.
const sources = ref([])
const listings = ref(null)
const loading = ref(false)
const failed = ref(false)

const confidence = computed(() => props.breed?.confidence ?? 'unverified')
const sourced = computed(() => confidence.value !== 'unverified')
// A range from live asking prices and one from published articles need different words.
// "49 sources" means 49 puppies for sale in the first case and 49 articles in the second.
const fromListings = computed(() => props.breed?.basis === 'listings')

watch(
  () => props.breed?.slug,
  async (slug) => {
    sources.value = []
    listings.value = null
    failed.value = false
    if (!slug || !sourced.value) return
    loading.value = true
    try {
      const res = await fetch(`/api/price-sources?breed=${encodeURIComponent(slug)}`)
      if (!res.ok) throw new Error(String(res.status))
      const body = await res.json()
      // Drop the legacy placeholder — it has no URL and nothing to click — and show each
      // publisher once. A page that states several figures produces several observations, so
      // the raw list read "Dogster, Dogster, Insuranceopedia, Insuranceopedia, MetLife, MetLife"
      // — which looks like a rendering bug and also overstates how many voices there are. The
      // aggregation already counts one vote per publisher; this makes the display agree.
      const byPublisher = new Map()
      for (const s of body.sources ?? []) {
        if (!s.sourceUrl) continue
        if (!byPublisher.has(s.publisher)) byPublisher.set(s.publisher, s)
      }
      sources.value = [...byPublisher.values()]
      listings.value = body.listings ?? null
    } catch {
      failed.value = true
    } finally {
      loading.value = false
    }
  },
  { immediate: true },
)

const lastChecked = computed(() => {
  const raw = props.breed?.priceUpdatedAt
  if (!raw) return ''
  return new Date(raw).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })
})

// Each state gets its own sentence rather than a generic disclaimer, so the label
// always matches what the data can actually support.
const LABELS = {
  verified: { icon: '✓', tone: 'text-success' },
  contested: { icon: '⚖︎', tone: 'text-warning' },
  single_source: { icon: 'ℹ', tone: 'text-base-content/70' },
  unverified: { icon: '⚠︎', tone: 'text-warning' },
}
const label = computed(() => LABELS[confidence.value] ?? LABELS.unverified)

const summary = computed(() => {
  const n = props.breed?.sourceCount ?? 0
  // Listings first: for these breeds the range IS the market, so say so plainly rather
  // than calling live puppies "sources".
  if (confidence.value === 'verified' && fromListings.value) {
    const median = listings.value?.median
    return `Middle half of ${n} live listings`
      + `${median ? ` · typical asking price $${median.toLocaleString()}` : ''}`
  }
  switch (confidence.value) {
    case 'verified':
      return `Range from ${n} independent ${n === 1 ? 'source' : 'sources'}`
    case 'contested':
      return `Sources disagree materially about this breed — the spread below is real, not a rounding artefact.`
    case 'single_source':
      return `Range rests on a single source${lastChecked.value ? `, checked ${lastChecked.value}` : ''} — a rough marker, not a going rate.`
    default:
      return `This is our own estimate and isn't sourced yet. Treat it as rough orientation and get three local quotes.`
  }
})
</script>

<template>
  <!--
    One line by default, detail on demand. Expanded, this block was three paragraphs plus a
    row of publisher links and had become the densest part of the card — burying the range it
    exists to support. Provenance still has to be *reachable*, so nothing is removed: the
    count and the median stay visible, and the rest is one click away.
  -->
  <div v-if="breed?.priceLow" class="text-xs">
    <details class="group">
      <summary class="flex cursor-pointer items-baseline gap-1.5 list-none">
        <span :class="label.tone" aria-hidden="true">{{ label.icon }}</span>
        <span :class="label.tone">{{ summary }}</span>
        <span class="link ml-1 whitespace-nowrap opacity-70 group-open:hidden">how we know →</span>
      </summary>

      <div class="border-base-300 mt-1.5 border-l-2 pl-2.5">
        <p v-if="loading" class="opacity-50">Loading sources…</p>

        <template v-else>
          <!-- The full span alongside the band is the point: it's where the scam-priced end
               and the rare-colour end both become visible. -->
          <p v-if="listings" class="max-w-prose opacity-70">
            {{ listings.count }} live listings on {{ listings.host }}, spanning
            ${{ listings.cheapest.toLocaleString() }}–${{ listings.dearest.toLocaleString() }}.
            The band trims the extremes at both ends.
            <span v-if="lastChecked">Checked {{ lastChecked }}.</span>
          </p>

          <ul v-if="sources.length" class="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 opacity-70">
            <!-- Labelled when a listing range is showing, so published articles are never
                 mistaken for the source of a number they didn't produce. -->
            <li v-if="listings">Published estimates for comparison:</li>
            <li v-for="s in sources" :key="s.sourceUrl">
              <a :href="s.sourceUrl" target="_blank" rel="noopener noreferrer" class="link" :title="s.quote">
                {{ s.publisher }}
              </a>
            </li>
          </ul>

          <p v-if="failed" class="opacity-50">Couldn't load the source list.</p>
        </template>
      </div>
    </details>
  </div>
</template>
