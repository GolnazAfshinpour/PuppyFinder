<script setup>
defineProps({
  site: { type: Object, required: true },
  // Which filters the user set ('breed' | 'state' | 'city') — badged below so
  // visitors know how much of their search this site's link carries.
  wanted: { type: Array, default: () => [] },
})

const FILTER_LABELS = { breed: 'Breed', state: 'State', city: 'City' }

// Heroicons (MIT) outline paths — a real icon language instead of emoji.
const ICONS = {
  shield: 'M9 12.75 11.25 15 15 9.75m-3-7.036A11.959 11.959 0 0 1 3.598 6 11.99 11.99 0 0 0 3 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.571-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285Z',
  price: 'M12 6v12m-3-2.818.879.659c1.171.879 3.07.879 4.242 0 1.172-.879 1.172-2.303 0-3.182C13.536 12.219 12.768 12 12 12c-.725 0-1.45-.22-2.003-.659-1.106-.879-1.106-2.303 0-3.182s2.9-.879 4.006 0l.415.33M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z',
  truck: 'M8.25 18.75a1.5 1.5 0 0 1-3 0m3 0a1.5 1.5 0 0 0-3 0m3 0h6m-9 0H3.375a1.125 1.125 0 0 1-1.125-1.125V14.25m17.25 4.5a1.5 1.5 0 0 1-3 0m3 0a1.5 1.5 0 0 0-3 0m3 0h1.125c.621 0 1.129-.504 1.09-1.124a17.902 17.902 0 0 0-3.213-9.193 2.056 2.056 0 0 0-1.58-.86H14.25M16.5 18.75h-2.25m0-11.177v-.958c0-.568-.422-1.048-.987-1.106a48.554 48.554 0 0 0-10.026 0 1.106 1.106 0 0 0-.987 1.106v7.635m12-6.677v6.677m0 4.5v-4.5m0 0h-12',
  star: 'M11.48 3.499a.562.562 0 0 1 1.04 0l2.125 5.111a.563.563 0 0 0 .475.345l5.518.442c.499.04.701.663.321.988l-4.204 3.602a.563.563 0 0 0-.182.557l1.285 5.385a.562.562 0 0 1-.84.61l-4.725-2.885a.562.562 0 0 0-.586 0L6.982 20.54a.562.562 0 0 1-.84-.61l1.285-5.386a.562.562 0 0 0-.182-.557l-4.204-3.602a.562.562 0 0 1 .321-.988l5.518-.442a.563.563 0 0 0 .475-.345L11.48 3.5Z',
}

const FIELD_ROWS = [
  { icon: 'shield', title: 'Vetting', key: 'vetting' },
  { icon: 'price', title: 'Price', key: 'priceNote' },
  { icon: 'truck', title: 'Getting your dog', key: 'delivery' },
]
</script>

<template>
  <li class="card card-lift bg-base-100">
    <div class="card-body gap-3">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <h2 class="font-display card-title text-xl font-semibold">{{ site.name }}</h2>
        <span class="badge badge-soft" :class="site.kind === 'Adopt' ? 'badge-accent' : 'badge-secondary'">
          {{ site.kind }}
        </span>
      </div>
      <p class="max-w-prose text-sm opacity-80">{{ site.description }}</p>
      <div class="border-base-300 flex-1 space-y-2 border-t border-dashed pt-3 text-sm">
        <div v-for="row in FIELD_ROWS" :key="row.key" class="flex gap-2">
          <svg class="text-primary/80 mt-0.5 h-4 w-4 shrink-0" viewBox="0 0 24 24" fill="none"
            stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
            <title>{{ row.title }}</title>
            <path :d="ICONS[row.icon]" />
          </svg>
          <span>{{ site[row.key] }}</span>
        </div>
        <div class="flex gap-2">
          <svg class="text-secondary mt-0.5 h-4 w-4 shrink-0" viewBox="0 0 24 24" fill="none"
            stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
            <title>Best for</title>
            <path :d="ICONS.star" />
          </svg>
          <span><strong>Best for:</strong> {{ site.bestFor }}</span>
        </div>
      </div>
      <div v-if="site.caution" role="alert" class="alert alert-warning alert-soft py-2 text-xs">
        <span>
          ⚠️ {{ site.caution }}
          <!-- The caution is that this site does not vet, so the useful page is the one
               that says how to vet the breeder yourself. -->
          <a href="/safe#vet-a-breeder" class="link font-semibold">
            Safety guide →
          </a>
        </span>
      </div>
      <div v-if="wanted.length" class="flex flex-wrap items-center gap-1">
        <span class="text-xs opacity-60">This link carries:</span>
        <span
          v-for="f in wanted"
          :key="f"
          class="badge badge-sm"
          :class="site.appliedFilters.includes(f) ? 'badge-success badge-soft' : 'badge-ghost opacity-50'"
          :title="site.appliedFilters.includes(f)
            ? `Opens a ${FILTER_LABELS[f].toLowerCase()}-filtered page`
            : `This site has no ${FILTER_LABELS[f].toLowerCase()}-filtered pages`"
        >
          {{ site.appliedFilters.includes(f) ? '✓' : '✕' }} {{ FILTER_LABELS[f] }}
        </span>
      </div>
      <a class="btn btn-primary btn-block" :href="site.linkUrl" target="_blank" rel="noopener noreferrer">
        {{ site.linkLabel }} ↗
      </a>
    </div>
  </li>
</template>
