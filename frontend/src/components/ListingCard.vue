<script setup>
import { computed, ref } from 'vue'
import { goodWithLine } from '../goodWith.js'

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

// Present on about 28% of listings, and the single item adopters rank most important on a
// profile. Shown when the rescue published it and silent otherwise — the detail view is where
// "they haven't listed one, ask when you call" belongs.
const goodWith = computed(() => goodWithLine(props.listing))

// One muted metadata line instead of a pile of badges — evidence caps badges
// at 1-2 per card, so the fit % keeps badge treatment and the rest is text.
const metaLine = computed(() => {
  const sex = props.listing.sex?.replace(/\s*\(.*\)\s*$/, '')
  const qualifier = props.listing.sex?.match(/\((.+)\)/)?.[1]
  // Distance joins the same line rather than becoming a badge: DESIGN.md caps cards at one muted
  // metadata line, and it is only present when the visitor gave somewhere to measure from.
  const away = props.listing.distanceMiles === null || props.listing.distanceMiles === undefined
    ? null
    : `${Math.round(props.listing.distanceMiles)} mi away`
  return [sex, qualifier && qualifier[0].toUpperCase() + qualifier.slice(1), props.listing.age, props.listing.size, away]
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
        <div class="flex shrink-0 flex-wrap justify-end gap-1">
          <!-- The card's badge cap is two, and these are the two worth having: what it costs,
               and how well it fits. -->
          <span v-if="listing.adoptionFee" class="badge badge-secondary badge-soft font-bold whitespace-nowrap">
            {{ listing.adoptionFee }}
          </span>
          <span v-if="listing.fit != null" class="badge badge-primary badge-soft font-bold whitespace-nowrap">
            {{ listing.fit }}% fit
          </span>
        </div>
      </div>
      <p v-if="metaLine" class="text-sm opacity-70">{{ metaLine }}</p>
      <p v-if="unconfirmedNote" class="text-xs opacity-60 italic">{{ unconfirmedNote }}</p>
      <p class="text-sm font-medium">{{ listing.breed }}</p>
      <!-- Only rendered when the rescue actually recorded something. A "not recorded" line on
           three quarters of the grid would be noise rather than honesty. -->
      <p v-if="goodWith" class="text-xs opacity-70">{{ goodWith }}</p>
      <!--
        Heroicons, not emoji. DESIGN.md §4 allows emoji only as content warmth and never as
        field icons, and 📍/📞 were doing exactly that. Inline paths rather than a dependency
        for two glyphs.
      -->
      <p class="flex items-center gap-1.5 text-sm opacity-70">
        <svg class="text-primary/80 h-3.5 w-3.5 shrink-0" viewBox="0 0 24 24" fill="none"
          stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"
          aria-hidden="true">
          <path d="M15 10.5a3 3 0 11-6 0 3 3 0 016 0z" />
          <path d="M19.5 10.5c0 7.142-7.5 11.25-7.5 11.25S4.5 17.642 4.5 10.5a7.5 7.5 0 1115 0z" />
        </svg>
        {{ listing.city }}, {{ listing.state }}
      </p>
      <p v-if="listing.contactInfo" class="flex items-start gap-1.5 text-sm font-medium">
        <svg class="text-primary/80 mt-0.5 h-3.5 w-3.5 shrink-0" viewBox="0 0 24 24" fill="none"
          stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"
          aria-hidden="true">
          <path d="M2.25 6.75c0 8.284 6.716 15 15 15h2.25a2.25 2.25 0 002.25-2.25v-1.372c0-.516-.351-.966-.852-1.091l-4.423-1.106c-.44-.11-.902.055-1.173.417l-.97 1.293c-.282.376-.769.542-1.21.38a12.035 12.035 0 01-7.143-7.143c-.162-.441.004-.928.38-1.21l1.293-.97c.363-.271.527-.734.417-1.173L6.963 3.102a1.125 1.125 0 00-1.091-.852H4.5A2.25 2.25 0 002.25 4.5v2.25z" />
        </svg>
        <span>
          {{ listing.contactInfo }}
          <span v-if="listing.animalRef" class="opacity-70">— ask about {{ listing.animalRef }}</span>
        </span>
      </p>
      <!--
        Clamped to two lines *and* reserving that height, so a card with a bio and one without
        end the same length. Ragged card heights across the grid were making it hard to scan
        down a column.
      -->
      <p class="line-clamp-2 min-h-8 text-xs opacity-60">{{ listing.description || '' }}</p>
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
