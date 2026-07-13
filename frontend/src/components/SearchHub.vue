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
  <section class="card bg-base-100 mx-auto mb-11 max-w-2xl shadow-md">
    <div class="card-body gap-4">
      <div class="flex flex-col gap-3 sm:flex-row">
        <label class="form-control flex-1">
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
        <label class="form-control flex-1">
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
      </div>

      <div>
        <span class="label-text mb-1 block text-xs font-bold tracking-wide uppercase opacity-60">I want to…</span>
        <div class="join w-full flex-col sm:flex-row">
          <button
            v-for="g in GOALS"
            :key="g.value"
            type="button"
            class="btn join-item flex-1"
            :class="goal === g.value ? 'btn-primary' : 'btn-outline'"
            @click="emit('update:goal', g.value)"
          >
            {{ g.label }}
          </button>
        </div>
      </div>

      <div class="mt-1 flex flex-col items-center gap-2 sm:flex-row sm:justify-center sm:gap-3">
        <button type="button" class="btn btn-primary w-full sm:w-auto" @click="emit('open-all')">
          🚀 Open results on all {{ siteCount }} sites
        </button>
        <button type="button" class="btn btn-outline w-full sm:w-auto" @click="emit('open-quiz')">
          🧭 Not sure? Take the 1-minute quiz
        </button>
      </div>
      <p class="text-center text-xs opacity-60">
        Your browser may ask to allow pop-ups the first time — allow once.
      </p>
    </div>
  </section>
</template>
