<script setup>
import { computed, ref, watch } from 'vue'

const props = defineProps({
  breed: { type: Object, default: null }, // entry from /api/breeds
})

// Provenance is fetched lazily — it's a second request, and only worth making once
// someone actually has a breed selected.
const sources = ref([])
const loading = ref(false)
const failed = ref(false)

const confidence = computed(() => props.breed?.confidence ?? 'unverified')
const sourced = computed(() => confidence.value !== 'unverified')

watch(
  () => props.breed?.slug,
  async (slug) => {
    sources.value = []
    failed.value = false
    if (!slug || !sourced.value) return
    loading.value = true
    try {
      const res = await fetch(`/api/price-sources?breed=${encodeURIComponent(slug)}`)
      if (!res.ok) throw new Error(String(res.status))
      const body = await res.json()
      // Drop the legacy placeholder — it has no URL and nothing to click.
      sources.value = (body.sources ?? []).filter((s) => s.sourceUrl)
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
  switch (confidence.value) {
    case 'verified':
      return `Range from ${n} independent ${n === 1 ? 'source' : 'sources'}${lastChecked.value ? `, last checked ${lastChecked.value}` : ''}.`
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
  <div v-if="breed?.priceLow" class="text-xs">
    <p :class="label.tone" class="flex items-start gap-1.5">
      <span aria-hidden="true">{{ label.icon }}</span>
      <span>{{ summary }}</span>
    </p>

    <p v-if="loading" class="mt-1 opacity-50">Loading sources…</p>

    <ul v-else-if="sources.length" class="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 opacity-70">
      <li v-for="s in sources" :key="s.sourceUrl">
        <a :href="s.sourceUrl" target="_blank" rel="noopener noreferrer" class="link" :title="s.quote">
          {{ s.publisher }}
        </a>
      </li>
    </ul>

    <p v-else-if="failed" class="mt-1 opacity-50">Couldn't load the source list.</p>
  </div>
</template>
