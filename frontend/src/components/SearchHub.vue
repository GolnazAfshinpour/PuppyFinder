<script setup>
const props = defineProps({
  breeds: { type: Array, required: true },
  usStates: { type: Array, required: true },
  breed: { type: String, default: '' },
  state: { type: String, default: '' },
  goal: { type: String, default: 'both' },
  siteCount: { type: Number, default: 0 },
})

const emit = defineEmits(['update:breed', 'update:state', 'update:goal', 'open-all', 'open-quiz'])

const GOALS = [
  { value: 'adopt', label: '🤝 Adopt' },
  { value: 'buy', label: '🛍️ Buy from a breeder' },
  { value: 'both', label: 'Show me both' },
]
</script>

<template>
  <section class="card bg-base-100 shadow-md">
    <div class="card-body gap-4">
      <h2 class="card-title text-base">🔍 Your search</h2>

      <label class="form-control">
        <span class="label-text mb-1 text-xs font-bold tracking-wide uppercase opacity-60">Breed</span>
        <select
          class="select select-bordered w-full"
          :value="breed"
          @change="emit('update:breed', $event.target.value)"
        >
          <option value="">Any breed</option>
          <option v-for="b in breeds" :key="b.slug" :value="b.slug">{{ b.displayName }}</option>
        </select>
      </label>

      <label class="form-control">
        <span class="label-text mb-1 text-xs font-bold tracking-wide uppercase opacity-60">Location</span>
        <select
          class="select select-bordered w-full"
          :value="state"
          @change="emit('update:state', $event.target.value)"
        >
          <option value="">Anywhere in the US</option>
          <option v-for="s in usStates" :key="s" :value="s">{{ s }}</option>
        </select>
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
