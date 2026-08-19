<script setup>
import { computed, nextTick, ref, watch } from 'vue'
import { rankBreeds } from '../breedSearch.js'

// Replaces a 179-option <select>. A native select only jumps to names *beginning* with what you
// type, so "retriever" found nothing and "shepherd" missed Australian Shepherd — and the
// alternative was scrolling a list nearly two hundred long. Ranking lives in breedSearch.js.

const props = defineProps({
  // Already narrowed by size and the breed-list traits; this component never re-filters those.
  breeds: { type: Array, default: () => [] },
  modelValue: { type: String, default: '' },
})

const emit = defineEmits(['update:modelValue'])

const query = ref('')
const open = ref(false)
const active = ref(-1)
const input = ref(null)
const listId = 'breed-picker-list'

const selected = computed(() => props.breeds.find((b) => b.slug === props.modelValue) ?? null)
const matches = computed(() => rankBreeds(props.breeds, query.value))

// What the box shows: the selected breed while closed, whatever is being typed while open.
const display = computed(() => (open.value ? query.value : selected.value?.displayName ?? ''))

// A breed can stop matching the size/trait narrowers while it is selected — the parent clears
// it, and the box has to follow rather than keep showing a name that is no longer chosen.
watch(() => props.modelValue, () => {
  if (!open.value) query.value = ''
})

function show() {
  open.value = true
  query.value = ''
  active.value = -1
}

function close() {
  open.value = false
  query.value = ''
  active.value = -1
}

function choose(breed) {
  emit('update:modelValue', breed?.slug ?? '')
  close()
  input.value?.blur()
}

function onInput(event) {
  query.value = event.target.value
  open.value = true
  active.value = -1
}

async function move(delta) {
  if (!open.value) {
    show()
    await nextTick()
  }
  const count = matches.value.length + 1 // +1 for the "Any breed" row at the top
  if (!count) return
  active.value = (active.value + delta + count) % count
  await nextTick()
  document.getElementById(optionId(active.value))?.scrollIntoView({ block: 'nearest' })
}

function onEnter() {
  if (!open.value) return
  // Nothing highlighted: take the best match, which is what the list is already showing first.
  if (active.value < 0) choose(matches.value[0] ?? null)
  else choose(active.value === 0 ? null : matches.value[active.value - 1])
}

const optionId = (index) => `${listId}-opt-${index}`
</script>

<template>
  <div class="relative" data-testid="breed-picker" @focusout="(e) => !$el.contains(e.relatedTarget) && close()">
    <input
      ref="input"
      type="text"
      class="input input-bordered w-full"
      role="combobox"
      aria-autocomplete="list"
      :aria-expanded="open"
      :aria-controls="listId"
      :aria-activedescendant="open && active >= 0 ? optionId(active) : undefined"
      aria-label="Breed"
      data-testid="breed-input"
      :placeholder="selected ? selected.displayName : 'Any breed — type to search'"
      :value="display"
      @focus="show"
      @input="onInput"
      @keydown.down.prevent="move(1)"
      @keydown.up.prevent="move(-1)"
      @keydown.enter.prevent="onEnter"
      @keydown.esc.prevent="close"
    />

    <!-- Clearing is one click rather than scrolling back to the top of a list for "Any breed". -->
    <button
      v-if="modelValue && !open"
      type="button"
      class="btn btn-ghost btn-xs absolute top-1/2 right-2 -translate-y-1/2"
      aria-label="Clear breed"
      @click="choose(null)"
    >
      ✕
    </button>

    <ul
      v-if="open"
      :id="listId"
      role="listbox"
      aria-label="Breeds"
      class="bg-base-100 border-base-300 rounded-box absolute z-30 mt-1 max-h-64 w-full list-none overflow-y-auto border p-1 shadow-lg"
    >
      <li
        :id="optionId(0)"
        role="option"
        :aria-selected="!modelValue"
        class="cursor-pointer rounded px-3 py-2 text-sm"
        :class="active === 0 ? 'bg-primary text-primary-content' : 'hover:bg-base-200'"
        @mousedown.prevent="choose(null)"
        @mousemove="active = 0"
      >
        Any breed
      </li>
      <li
        v-for="(b, i) in matches"
        :id="optionId(i + 1)"
        :key="b.slug"
        role="option"
        :aria-selected="b.slug === modelValue"
        :data-slug="b.slug"
        class="cursor-pointer rounded px-3 py-2 text-sm"
        :class="active === i + 1 ? 'bg-primary text-primary-content' : 'hover:bg-base-200'"
        @mousedown.prevent="choose(b)"
        @mousemove="active = i + 1"
      >
        {{ b.displayName }}
      </li>
      <!-- Said rather than shown as an empty box: an empty list with no explanation reads as a
           broken control. -->
      <li v-if="!matches.length" class="px-3 py-2 text-sm opacity-60">
        No breeds match “{{ query }}”.
      </li>
    </ul>
  </div>
</template>
