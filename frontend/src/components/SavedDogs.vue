<script setup>
import { computed, ref } from 'vue'
import { useModal } from '../useModal.js'

const props = defineProps({
  favorites: { type: Array, default: () => [] },
  recent: { type: Array, default: () => [] },
})

const emit = defineEmits(['close', 'open-dog', 'unsave'])

const box = ref(null)
const closeButton = ref(null)
useModal(() => emit('close'), closeButton, box)

// Recently-viewed minus anything already saved: seeing the same dog in both lists wastes the
// space and makes the saved list look padded.
const alsoViewed = computed(() => {
  const saved = new Set(props.favorites.map((f) => f.id))
  return props.recent.filter((r) => !saved.has(r.id))
})

// Stored snapshots, not live listings — a shelter drops a dog from its feed the moment it is
// adopted, and a dog you saved should still have its name and photo afterwards. Opening one
// goes by id so the detail view fetches fresh and can say "found a home" rather than showing
// a stale card as though the dog were still available.
function open(dog) {
  emit('open-dog', dog.id)
  emit('close')
}
</script>

<template>
  <div class="modal modal-open" @click.self="$emit('close')">
    <div ref="box" class="modal-box max-w-2xl" role="dialog" aria-modal="true" aria-labelledby="saved-dogs-title">
      <div class="flex items-start justify-between gap-4">
        <div>
          <h2 id="saved-dogs-title" class="font-display text-2xl font-semibold">Your dogs</h2>
          <p class="mt-1 max-w-prose text-sm opacity-70">
            Saved on this device — no account, nothing sent anywhere. Shelters remove a dog
            from their feed once it is adopted, so a saved card can outlive the listing.
          </p>
        </div>
        <button ref="closeButton" type="button" class="btn btn-sm btn-circle btn-ghost" aria-label="Close" @click="$emit('close')">
          ✕
        </button>
      </div>

      <section v-if="favorites.length" class="mt-4">
        <h3 class="mb-2 text-xs font-bold tracking-wide uppercase opacity-60">
          Saved ({{ favorites.length }})
        </h3>
        <ul class="divide-base-300 divide-y" data-testid="saved-dogs">
          <li v-for="f in favorites" :key="f.id" class="flex items-center gap-3 py-2">
            <img
              v-if="f.imageUrl"
              :src="f.imageUrl"
              :alt="f.name"
              referrerpolicy="no-referrer"
              class="bg-base-300 h-12 w-12 shrink-0 rounded-lg object-cover"
            />
            <span v-else class="bg-base-300 grid h-12 w-12 shrink-0 place-items-center rounded-lg" aria-hidden="true">
              🐶
            </span>
            <button type="button" class="min-w-0 flex-1 text-left" @click="open(f)">
              <span class="block truncate font-semibold">{{ f.name }}</span>
              <span class="block truncate text-xs opacity-60">
                {{ [f.breed, f.city && `${f.city}, ${f.state}`].filter(Boolean).join(' · ') }}
              </span>
            </button>
            <button
              type="button"
              class="btn btn-ghost btn-xs"
              :aria-label="`Remove ${f.name} from saved`"
              @click="emit('unsave', f)"
            >
              Remove
            </button>
          </li>
        </ul>
      </section>

      <section v-if="alsoViewed.length" class="mt-5">
        <h3 class="mb-2 text-xs font-bold tracking-wide uppercase opacity-60">
          Recently viewed
        </h3>
        <ul class="divide-base-300 divide-y" data-testid="recent-dogs">
          <li v-for="r in alsoViewed" :key="r.id" class="flex items-center gap-3 py-2">
            <img
              v-if="r.imageUrl"
              :src="r.imageUrl"
              :alt="r.name"
              referrerpolicy="no-referrer"
              class="bg-base-300 h-9 w-9 shrink-0 rounded-lg object-cover"
            />
            <span v-else class="bg-base-300 grid h-9 w-9 shrink-0 place-items-center rounded-lg" aria-hidden="true">
              🐶
            </span>
            <button type="button" class="min-w-0 flex-1 text-left" @click="open(r)">
              <span class="block truncate text-sm">{{ r.name }}</span>
              <span class="block truncate text-xs opacity-60">{{ r.breed }}</span>
            </button>
          </li>
        </ul>
      </section>

      <p v-if="!favorites.length && !alsoViewed.length" class="mt-4 max-w-prose text-sm opacity-70">
        Nothing saved yet. Tap the heart on any dog to keep it here, and dogs you open will
        show up under "recently viewed".
      </p>
    </div>
  </div>
</template>
