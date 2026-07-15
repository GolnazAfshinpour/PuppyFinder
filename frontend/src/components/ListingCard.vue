<script setup>
import { ref } from 'vue'

defineProps({
  listing: { type: Object, required: true },
})

const imageFailed = ref(false)
</script>

<template>
  <li class="card bg-base-100 shadow-md transition hover:-translate-y-0.5 hover:shadow-xl">
    <figure class="bg-base-200 h-44">
      <img
        v-if="listing.imageUrl && !imageFailed"
        :src="listing.imageUrl"
        :alt="listing.name"
        class="h-full w-full object-cover"
        loading="lazy"
        referrerpolicy="no-referrer"
        @error="imageFailed = true"
      />
      <span v-else class="text-5xl" aria-hidden="true">🐶</span>
    </figure>
    <div class="card-body gap-2 p-4">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <h3 class="card-title text-lg">{{ listing.name }}</h3>
        <div class="flex gap-1">
          <span v-if="listing.sex" class="badge badge-soft badge-secondary">{{ listing.sex }}</span>
          <span v-if="listing.age" class="badge badge-ghost">{{ listing.age }}</span>
        </div>
      </div>
      <p class="text-sm font-medium">{{ listing.breed }}</p>
      <p class="text-sm opacity-70">📍 {{ listing.city }}, {{ listing.state }}</p>
      <p v-if="listing.description" class="line-clamp-2 text-xs opacity-60">{{ listing.description }}</p>
      <p class="text-xs opacity-50">
        via <a :href="listing.sourceUrl" target="_blank" rel="noopener noreferrer" class="link">{{ listing.source }}</a>
      </p>
      <a
        class="btn btn-primary btn-block btn-sm mt-1"
        :href="listing.listingUrl"
        target="_blank"
        rel="noopener noreferrer"
      >
        Meet {{ listing.name }} ↗
      </a>
    </div>
  </li>
</template>
