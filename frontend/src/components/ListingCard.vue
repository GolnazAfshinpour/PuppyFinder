<script setup>
import { computed, ref } from 'vue'

const props = defineProps({
  listing: { type: Object, required: true },
})

const imageFailed = ref(false)

// One muted metadata line instead of a pile of badges — evidence caps badges
// at 1-2 per card, so the fit % keeps badge treatment and the rest is text.
const metaLine = computed(() => {
  const sex = props.listing.sex?.replace(/\s*\(.*\)\s*$/, '')
  const qualifier = props.listing.sex?.match(/\((.+)\)/)?.[1]
  return [sex, qualifier && qualifier[0].toUpperCase() + qualifier.slice(1), props.listing.age, props.listing.size]
    .filter(Boolean)
    .join(' · ')
})
</script>

<template>
  <li class="card card-lift bg-base-100 focus-within:ring-primary/50 relative overflow-hidden focus-within:ring-2">
    <figure class="bg-base-300 aspect-[4/3]">
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
      <div class="flex items-start justify-between gap-2">
        <!-- Stretched link: the name is the accessible link, its hit area is the
             whole card; other interactive elements sit above it via z-10. -->
        <h3 class="font-display card-title text-xl font-semibold">
          <a
            :href="listing.listingUrl"
            target="_blank"
            rel="noopener noreferrer"
            class="after:absolute after:inset-0 after:content-[''] focus-visible:outline-none"
          >
            {{ listing.name }}
          </a>
        </h3>
        <span v-if="listing.fit != null" class="badge badge-primary badge-soft font-bold whitespace-nowrap">
          {{ listing.fit }}% fit
        </span>
      </div>
      <p v-if="metaLine" class="text-sm opacity-70">{{ metaLine }}</p>
      <p class="text-sm font-medium">{{ listing.breed }}</p>
      <p class="text-sm opacity-70">📍 {{ listing.city }}, {{ listing.state }}</p>
      <p v-if="listing.contactInfo" class="text-sm font-medium">
        {{ listing.contactInfo }}
        <span v-if="listing.animalRef" class="opacity-70">— ask about {{ listing.animalRef }}</span>
      </p>
      <p v-if="listing.description" class="line-clamp-2 text-xs opacity-60">{{ listing.description }}</p>
      <p class="text-xs opacity-50">
        via
        <a :href="listing.sourceUrl" target="_blank" rel="noopener noreferrer" class="link relative z-10">
          {{ listing.source }}
        </a>
      </p>
      <a
        class="btn btn-primary btn-block btn-sm relative z-10 mt-1"
        :href="listing.listingUrl"
        target="_blank"
        rel="noopener noreferrer"
      >
        Meet {{ listing.name }} ↗
      </a>
    </div>
  </li>
</template>
