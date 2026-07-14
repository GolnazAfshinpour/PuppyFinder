<script setup>
defineProps({
  site: { type: Object, required: true },
  // Which filters the user set ('breed' | 'state' | 'city') — badged below so
  // visitors know how much of their search this site's link carries.
  wanted: { type: Array, default: () => [] },
})

defineEmits(['open-guide'])

const FILTER_LABELS = { breed: 'Breed', state: 'State', city: 'City' }
</script>

<template>
  <li class="card bg-base-100 shadow-md transition hover:-translate-y-0.5 hover:shadow-xl">
    <div class="card-body gap-3">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <h2 class="card-title text-lg">{{ site.name }}</h2>
        <span class="badge badge-soft" :class="site.kind === 'Adopt' ? 'badge-success' : 'badge-warning'">
          {{ site.kind }}
        </span>
      </div>
      <p class="text-sm opacity-80">{{ site.description }}</p>
      <div class="border-base-300 flex-1 space-y-2 border-t border-dashed pt-3 text-sm">
        <div class="flex items-baseline gap-2">
          <span title="Vetting">🛡️</span>
          <span>{{ site.vetting }}</span>
        </div>
        <div class="flex items-baseline gap-2">
          <span title="Price">💰</span>
          <span>{{ site.priceNote }}</span>
        </div>
        <div class="flex items-baseline gap-2">
          <span title="Getting your dog">🚚</span>
          <span>{{ site.delivery }}</span>
        </div>
        <div class="flex items-baseline gap-2">
          <span title="Best for">⭐</span>
          <span><strong>Best for:</strong> {{ site.bestFor }}</span>
        </div>
      </div>
      <div v-if="site.caution" role="alert" class="alert alert-warning alert-soft py-2 text-xs">
        <span>
          ⚠️ {{ site.caution }}
          <button type="button" class="link font-semibold" @click="$emit('open-guide')">
            Safety guide →
          </button>
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
