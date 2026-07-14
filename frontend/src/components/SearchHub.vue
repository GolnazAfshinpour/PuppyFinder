<script setup>
import { computed } from 'vue'

const props = defineProps({
  breeds: { type: Array, required: true },
  usStates: { type: Array, required: true },
  breed: { type: String, default: '' },
  state: { type: String, default: '' },
  city: { type: String, default: '' },
  size: { type: String, default: '' },
  goal: { type: String, default: 'both' },
  siteCount: { type: Number, default: 0 },
})

const emit = defineEmits([
  'update:breed', 'update:state', 'update:city', 'update:size', 'update:goal',
  'open-all', 'open-quiz',
])

const GOALS = [
  { value: 'adopt', label: '🤝 Adopt' },
  { value: 'buy', label: '🛍️ Buy from a breeder' },
  { value: 'both', label: 'Show me both' },
]

const SIZES = ['Teacup', 'Small', 'Medium', 'Large']

// Size narrows the breed list; sizes are known for the curated breeds only.
const filteredBreeds = computed(() =>
  props.size ? props.breeds.filter((b) => b.size === props.size) : props.breeds,
)
</script>

<template>
  <section class="card bg-base-100 shadow-md">
    <div class="card-body gap-4">
      <h2 class="card-title text-base">🔍 Your search</h2>

      <div>
        <span class="label-text mb-1 block text-xs font-bold tracking-wide uppercase opacity-60">Size</span>
        <div class="join w-full">
          <button
            type="button"
            class="btn join-item btn-sm flex-1"
            :class="size === '' ? 'btn-primary' : 'btn-outline'"
            @click="emit('update:size', '')"
          >
            Any
          </button>
          <button
            v-for="s in SIZES"
            :key="s"
            type="button"
            class="btn join-item btn-sm flex-1 px-1"
            :class="size === s ? 'btn-primary' : 'btn-outline'"
            @click="emit('update:size', s)"
          >
            {{ s }}
          </button>
        </div>
        <p v-if="size" class="mt-1 text-xs opacity-60">
          Showing {{ filteredBreeds.length }} {{ size.toLowerCase() }} breeds (from our curated list).
        </p>
      </div>

      <label class="form-control">
        <span class="label-text mb-1 text-xs font-bold tracking-wide uppercase opacity-60">Breed</span>
        <select
          class="select select-bordered w-full"
          :value="breed"
          @change="emit('update:breed', $event.target.value)"
        >
          <option value="">Any breed</option>
          <option v-for="b in filteredBreeds" :key="b.slug" :value="b.slug">{{ b.displayName }}</option>
        </select>
      </label>

      <label class="form-control">
        <span class="label-text mb-1 text-xs font-bold tracking-wide uppercase opacity-60">State</span>
        <select
          class="select select-bordered w-full"
          :value="state"
          @change="emit('update:state', $event.target.value)"
        >
          <option value="">Anywhere in the US</option>
          <option v-for="s in usStates" :key="s" :value="s">{{ s }}</option>
        </select>
      </label>

      <label class="form-control">
        <span class="label-text mb-1 text-xs font-bold tracking-wide uppercase opacity-60">
          City <span class="normal-case opacity-70">(optional)</span>
        </span>
        <input
          type="text"
          class="input input-bordered w-full"
          :value="city"
          :disabled="!state"
          :placeholder="state ? 'e.g. Houston' : 'Pick a state first'"
          @input="emit('update:city', $event.target.value)"
        />
      </label>

      <div>
        <span class="label-text mb-1 block text-xs font-bold tracking-wide uppercase opacity-60">I want to…</span>
        <div class="join join-vertical w-full">
          <button
            v-for="g in GOALS"
            :key="g.value"
            type="button"
            class="btn join-item justify-start"
            :class="goal === g.value ? 'btn-primary' : 'btn-outline'"
            @click="emit('update:goal', g.value)"
          >
            {{ g.label }}
          </button>
        </div>
      </div>

      <div class="mt-1 flex flex-col gap-2">
        <button type="button" class="btn btn-primary w-full" @click="emit('open-all')">
          🚀 Open all {{ siteCount }} sites
        </button>
        <button type="button" class="btn btn-outline w-full" @click="emit('open-quiz')">
          🧭 Take the breed quiz
        </button>
      </div>
      <p class="text-center text-xs opacity-60">
        Your browser may ask to allow pop-ups — allow once.
      </p>
    </div>
  </section>
</template>
