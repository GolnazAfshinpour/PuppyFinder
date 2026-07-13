<script setup>
import { ref } from 'vue'

defineProps({
  listings: { type: Array, required: true },
  loading: { type: Boolean, default: false },
})

const brokenImages = ref(new Set())

function ageSex(listing) {
  return [listing.age, listing.sex].filter(Boolean).join(' • ')
}

function markImageBroken(id) {
  brokenImages.value = new Set(brokenImages.value).add(id)
}
</script>

<template>
  <section class="mt-14">
    <h2 class="mb-6 text-center text-2xl font-bold">🐾 Real adoptable dogs right now</h2>
    <p v-if="loading" class="text-center">
      <span class="loading loading-dots loading-md" />
    </p>
    <p v-else-if="listings.length === 0" class="text-center text-base-content/60">
      No live listings match this breed/state — try widening the search.
    </p>
    <ul v-else class="grid list-none gap-5 p-0 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
      <li
        v-for="dog in listings"
        :key="dog.id"
        class="card bg-base-100 shadow-md transition hover:-translate-y-0.5 hover:shadow-xl"
      >
        <figure class="relative aspect-[3/2]">
          <a
            :href="dog.listingUrl"
            target="_blank"
            rel="noopener noreferrer"
            class="block h-full w-full"
          >
            <img
              v-if="dog.imageUrl && !brokenImages.has(dog.id)"
              :src="dog.imageUrl"
              :alt="`${dog.name}, ${dog.breed}`"
              loading="lazy"
              class="h-full w-full object-cover"
              @error="markImageBroken(dog.id)"
            />
            <div v-else class="bg-base-200 grid h-full w-full place-items-center text-4xl">🐾</div>
            <span class="badge badge-neutral absolute bottom-2 left-2">{{ dog.breed }}</span>
          </a>
        </figure>
        <div class="card-body gap-2 p-4">
          <div class="flex items-baseline justify-between gap-2">
            <a
              :href="dog.listingUrl"
              target="_blank"
              rel="noopener noreferrer"
              class="link link-hover font-bold"
            >
              {{ dog.name }}
            </a>
            <span v-if="ageSex(dog)" class="text-xs whitespace-nowrap opacity-60">{{ ageSex(dog) }}</span>
          </div>
          <div class="flex flex-wrap items-center justify-between gap-1">
            <span class="text-xs opacity-60">📍 {{ dog.city }}, {{ dog.state }}</span>
            <a
              :href="dog.sourceUrl"
              target="_blank"
              rel="noopener noreferrer"
              class="badge badge-ghost badge-sm"
            >
              {{ dog.source }} ↗
            </a>
          </div>
        </div>
      </li>
    </ul>
  </section>
</template>
