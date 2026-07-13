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
  { value: 'both', label: 'Adopt or buy' },
  { value: 'adopt', label: 'Adopt' },
  { value: 'buy', label: 'Buy from a breeder' },
]
</script>

<template>
  <section class="hub">
    <div class="hub-row">
      <select :value="breed" @change="emit('update:breed', $event.target.value)">
        <option value="">Any breed</option>
        <option v-for="b in breeds" :key="b.slug" :value="b.slug">{{ b.displayName }}</option>
      </select>
      <select :value="state" @change="emit('update:state', $event.target.value)">
        <option value="">Anywhere in the US</option>
        <option v-for="s in usStates" :key="s" :value="s">{{ s }}</option>
      </select>
    </div>

    <div class="goal-row">
      <button
        v-for="g in GOALS"
        :key="g.value"
        type="button"
        class="goal-pill"
        :class="{ active: goal === g.value }"
        @click="emit('update:goal', g.value)"
      >
        {{ g.label }}
      </button>
    </div>

    <div class="actions-row">
      <button type="button" class="open-all" @click="emit('open-all')">
        🚀 Open results on all {{ siteCount }} sites
      </button>
      <button type="button" class="quiz-link" @click="emit('open-quiz')">
        Not sure which breed? Take the 1-minute quiz →
      </button>
    </div>
    <p class="popup-hint">Your browser may ask to allow pop-ups the first time — allow once.</p>
  </section>
</template>

<style scoped>
.hub {
  max-width: 640px;
  margin: 0 auto 2.5rem;
  display: flex;
  flex-direction: column;
  gap: 0.9rem;
}

.hub-row {
  display: flex;
  gap: 0.75rem;
}

.hub-row select {
  flex: 1;
  padding: 0.7rem 1.1rem;
  border: 1px solid var(--border);
  border-radius: 999px;
  font-size: 0.95rem;
  font-family: inherit;
  background: var(--surface);
  color: var(--text-strong);
  box-shadow: var(--shadow);
  outline: none;
  transition: border-color 0.2s;
}

.hub-row select:focus {
  border-color: var(--accent);
}

.goal-row {
  display: flex;
  gap: 0.5rem;
  justify-content: center;
}

.goal-pill {
  padding: 0.45rem 1.1rem;
  border-radius: 999px;
  border: 1px solid var(--border);
  background: var(--surface);
  color: var(--text);
  font-size: 0.85rem;
  font-weight: 600;
  font-family: inherit;
  cursor: pointer;
  transition: all 0.2s;
}

.goal-pill.active {
  background: var(--accent);
  border-color: var(--accent);
  color: #fff;
}

.actions-row {
  display: flex;
  gap: 0.75rem;
  justify-content: center;
  align-items: center;
  flex-wrap: wrap;
}

.open-all {
  padding: 0.7rem 1.4rem;
  border-radius: 999px;
  border: none;
  background: var(--accent);
  color: #fff;
  font-size: 0.95rem;
  font-weight: 650;
  font-family: inherit;
  cursor: pointer;
  box-shadow: var(--shadow);
  transition: filter 0.2s;
}

.open-all:hover {
  filter: brightness(1.1);
}

.quiz-link {
  border: none;
  background: none;
  color: var(--accent);
  font-size: 0.9rem;
  font-weight: 600;
  font-family: inherit;
  cursor: pointer;
  text-decoration: underline;
  text-underline-offset: 3px;
}

.popup-hint {
  margin: 0;
  text-align: center;
  font-size: 0.75rem;
  color: var(--text-muted);
}

@media (max-width: 640px) {
  .hub-row {
    flex-direction: column;
  }
}
</style>
