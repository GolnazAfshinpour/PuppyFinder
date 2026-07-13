<script setup>
defineProps({
  site: { type: Object, required: true },
})
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
      <a class="btn btn-primary btn-block" :href="site.linkUrl" target="_blank" rel="noopener noreferrer">
        {{ site.linkLabel }} ↗
      </a>
    </div>
  </li>
</template>
