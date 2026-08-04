<script setup>
import { computed, ref } from 'vue'
import { articleFor } from '../article.js'

const props = defineProps({
  breeds: { type: Array, default: () => [] }, // from /api/breeds
})

const emit = defineEmits(['close', 'pick-breed'])

// The gap this closes: the hero advertised "50 sourced price ranges" as plain text, and the
// only way to see any of them was to guess a breed in the dropdown, or read the card's six
// examples. Advertising a number with no way to inspect it is the same shape of problem as
// publishing a range with no way to see its sources.
const sourced = computed(() =>
  props.breeds.filter((b) => b.confidence === 'verified' && b.priceLow != null),
)

const query = ref('')
const sort = ref('name')

const SORTS = [
  ['name', 'A–Z'],
  ['cheapest', 'Cheapest first'],
  ['dearest', 'Most expensive first'],
  ['evidence', 'Most listings behind it'],
]

const shown = computed(() => {
  const needle = query.value.trim().toLowerCase()
  const list = needle
    ? sourced.value.filter((b) => b.displayName.toLowerCase().includes(needle))
    : [...sourced.value]

  switch (sort.value) {
    case 'cheapest':
      return list.sort((a, b) => a.priceLow - b.priceLow)
    case 'dearest':
      return list.sort((a, b) => b.priceHigh - a.priceHigh)
    case 'evidence':
      return list.sort((a, b) => (b.sourceCount ?? 0) - (a.sourceCount ?? 0))
    default:
      return list.sort((a, b) => a.displayName.localeCompare(b.displayName))
  }
})

// Say what each range rests on in the row itself. "n=143" means something very different for
// live listings than for published articles, so the wording follows the basis rather than
// showing a bare number that reads as more authoritative than it is.
function evidence(breed) {
  const n = breed.sourceCount ?? 0
  return breed.basis === 'listings'
    ? `${n} live listings`
    : `${n} published ${n === 1 ? 'source' : 'sources'}`
}

function pick(slug) {
  emit('pick-breed', slug)
  emit('close')
}
</script>

<template>
  <div class="modal modal-open" @click.self="$emit('close')">
    <div class="modal-box max-w-3xl">
      <div class="flex items-start justify-between gap-4">
        <div>
          <h2 class="font-display text-2xl font-semibold">
            {{ sourced.length }} breeds with a sourced price range
          </h2>
          <p class="mt-1 max-w-prose text-sm opacity-70">
            Each range is the middle half of real asking prices, or a median across published
            sources — never our own estimate. Pick a breed to check a quote against it.
          </p>
        </div>
        <button type="button" class="btn btn-sm btn-circle btn-ghost" aria-label="Close" @click="$emit('close')">
          ✕
        </button>
      </div>

      <div class="mt-4 flex flex-wrap items-center gap-2">
        <label class="input input-bordered input-sm flex items-center gap-2">
          <svg class="h-4 w-4 opacity-50" viewBox="0 0 24 24" fill="none" stroke="currentColor"
            stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <circle cx="11" cy="11" r="7" />
            <path d="m21 21-4.3-4.3" />
          </svg>
          <input v-model="query" class="grow" placeholder="Filter breeds" aria-label="Filter breeds" />
        </label>
        <select v-model="sort" class="select select-bordered select-sm" aria-label="Sort ranges">
          <option v-for="[value, label] in SORTS" :key="value" :value="value">{{ label }}</option>
        </select>
      </div>

      <ul class="mt-3 divide-base-300 max-h-[55vh] divide-y overflow-y-auto" data-testid="sourced-prices">
        <li v-for="b in shown" :key="b.slug">
          <button
            type="button"
            class="hover:bg-base-200 flex w-full items-baseline justify-between gap-3 px-1 py-2 text-left"
            @click="pick(b.slug)"
          >
            <span class="min-w-0">
              <span class="font-semibold">{{ b.displayName }}</span>
              <span class="block text-xs opacity-60">{{ evidence(b) }}</span>
            </span>
            <span class="text-primary shrink-0 font-semibold">{{ b.typicalPrice }}</span>
          </button>
        </li>
      </ul>

      <p v-if="!shown.length" class="mt-3 max-w-prose text-sm opacity-70">
        No sourced range matches "{{ query }}". Try a shorter search — most breeds don't have
        enough live listings to build a range from.
      </p>

      <p class="mt-4 max-w-prose text-xs opacity-60">
        Not listed means we couldn't source it, not that it's cheap or rare to find.
        {{ breeds.length - sourced.length }} of {{ breeds.length }} breeds have no range we can
        stand behind, so the app says nothing about their prices rather than guessing.
      </p>
    </div>
  </div>
</template>
