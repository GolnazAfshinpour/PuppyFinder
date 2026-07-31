<script setup>
import { computed, ref } from 'vue'

const props = defineProps({
  listing: { type: Object, required: true },
  favorite: { type: Boolean, default: false },
  // Set when this dog matched a size/age filter only because the shelter left
  // that field blank. Stating it is more useful than either hiding the dog or
  // pretending it's a confirmed match.
  unconfirmedNote: { type: String, default: '' },
})

defineEmits(['toggle-favorite', 'open'])

// A real href so middle-click and "open in new tab" still work, but the click is
// intercepted into the in-app detail view. Deliberately just ?dog= with no filters:
// sharing a dog should open that dog, not the sender's search.
const detailHref = computed(() => `?dog=${encodeURIComponent(props.listing.id)}`)

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
    <figure class="bg-base-300 relative aspect-[4/3]">
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
      <button
        type="button"
        class="btn btn-circle btn-sm bg-base-100/85 absolute top-2 right-2 z-10 border-none backdrop-blur-sm transition-transform active:scale-125"
        :title="favorite ? `Remove ${listing.name} from saved dogs` : `Save ${listing.name}`"
        :aria-pressed="favorite"
        @click="$emit('toggle-favorite')"
      >
        <svg class="h-4.5 w-4.5" viewBox="0 0 24 24" :fill="favorite ? 'var(--color-primary)' : 'none'"
          stroke="var(--color-primary)" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
          <path d="M21 8.25c0-2.485-2.099-4.5-4.688-4.5-1.935 0-3.597 1.126-4.312 2.733-.715-1.607-2.377-2.733-4.313-2.733C5.1 3.75 3 5.765 3 8.25c0 7.22 9 12 9 12s9-4.78 9-12Z" />
        </svg>
      </button>
    </figure>
    <div class="card-body gap-2 p-4">
      <div class="flex items-start justify-between gap-2">
        <!-- Stretched link: the name is the accessible link, its hit area is the
             whole card; other interactive elements sit above it via z-10. -->
        <h3 class="font-display card-title text-xl font-semibold">
          <a
            :href="detailHref"
            class="after:absolute after:inset-0 after:content-[''] focus-visible:outline-none"
            @click.prevent="$emit('open')"
          >
            {{ listing.name }}
          </a>
        </h3>
        <span v-if="listing.fit != null" class="badge badge-primary badge-soft font-bold whitespace-nowrap">
          {{ listing.fit }}% fit
        </span>
      </div>
      <p v-if="metaLine" class="text-sm opacity-70">{{ metaLine }}</p>
      <p v-if="unconfirmedNote" class="text-xs opacity-60 italic">{{ unconfirmedNote }}</p>
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
        :href="detailHref"
        @click.prevent="$emit('open')"
      >
        Meet {{ listing.name }}
      </a>
    </div>
  </li>
</template>
