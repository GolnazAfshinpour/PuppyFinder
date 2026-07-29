<script setup>
import { computed, ref } from 'vue'

const props = defineProps({
  listing: { type: Object, required: true },
})

const imageFailed = ref(false)

// "Female (spayed)" → a compact "Female" badge plus a "Spayed" chip,
// so badge text never wraps mid-word inside a fixed-height badge.
const sexBase = computed(() => props.listing.sex?.replace(/\s*\(.*\)\s*$/, '') ?? '')
const sexQualifier = computed(() => {
  const match = props.listing.sex?.match(/\((.+)\)/)
  return match ? match[1][0].toUpperCase() + match[1].slice(1) : ''
})
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
      <h3 class="card-title text-lg">{{ listing.name }}</h3>
      <div v-if="sexBase || listing.age || listing.size" class="flex flex-wrap gap-1">
        <span v-if="sexBase" class="badge badge-soft badge-secondary whitespace-nowrap">{{ sexBase }}</span>
        <span v-if="sexQualifier" class="badge badge-ghost whitespace-nowrap">{{ sexQualifier }}</span>
        <span v-if="listing.age" class="badge badge-ghost whitespace-nowrap">{{ listing.age }}</span>
        <span v-if="listing.size" class="badge badge-ghost whitespace-nowrap">{{ listing.size }}</span>
      </div>
      <p class="text-sm font-medium">{{ listing.breed }}</p>
      <p class="text-sm opacity-70">📍 {{ listing.city }}, {{ listing.state }}</p>
      <p v-if="listing.contactInfo" class="text-sm font-medium">
        {{ listing.contactInfo }}
        <span v-if="listing.animalRef" class="opacity-70">— ask about {{ listing.animalRef }}</span>
      </p>
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
