<script setup>
import { ref } from 'vue'

const emit = defineEmits(['close', 'select'])

const QUESTIONS = [
  {
    key: 'home',
    label: 'Where do you live?',
    options: [
      { value: 'apartment', label: 'Apartment / condo' },
      { value: 'house', label: 'House with a yard' },
    ],
  },
  {
    key: 'activity',
    label: 'How active is your household?',
    options: [
      { value: 'low', label: 'Mostly relaxed' },
      { value: 'medium', label: 'Daily walks' },
      { value: 'high', label: 'Running, hiking, always out' },
    ],
  },
  {
    key: 'kids',
    label: 'Kids at home?',
    options: [
      { value: 'yes', label: 'Yes' },
      { value: 'no', label: 'No' },
    ],
  },
  {
    key: 'grooming',
    label: 'Brushing and grooming appointments…',
    options: [
      { value: 'low', label: 'Keep it minimal' },
      { value: 'high', label: 'Happy to groom' },
    ],
  },
  {
    key: 'size',
    label: 'Preferred size?',
    options: [
      { value: 'small', label: 'Small' },
      { value: 'medium', label: 'Medium' },
      { value: 'large', label: 'Large' },
      { value: 'any', label: 'No preference' },
    ],
  },
  {
    key: 'budget',
    label: 'Puppy budget (if buying)?',
    options: [
      { value: 'under1500', label: 'Under $1,500' },
      { value: 'over1500', label: '$1,500 is fine' },
      { value: 'any', label: 'Budget is flexible' },
    ],
  },
]

const answers = ref({ home: '', activity: '', kids: '', grooming: '', size: '', budget: '' })
const results = ref(null)
const submitting = ref(false)
const error = ref('')

const complete = () => Object.values(answers.value).every(Boolean)

async function submit() {
  if (!complete()) {
    error.value = 'Answer all six questions first.'
    return
  }
  error.value = ''
  submitting.value = true
  try {
    const res = await fetch('/api/quiz', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(answers.value),
    })
    if (!res.ok) throw new Error(`API returned ${res.status}`)
    results.value = await res.json()
  } catch (e) {
    error.value = `Could not score the quiz (${e.message})`
  } finally {
    submitting.value = false
  }
}

function reset() {
  results.value = null
  error.value = ''
}
</script>

<template>
  <div class="overlay" @click.self="emit('close')">
    <div class="modal">
      <button type="button" class="close" @click="emit('close')">✕</button>

      <template v-if="!results">
        <h2>🐕 Find your breed</h2>
        <p class="subtitle">Six quick questions — we'll match you to the breeds that fit your life.</p>

        <fieldset v-for="q in QUESTIONS" :key="q.key">
          <legend>{{ q.label }}</legend>
          <div class="options">
            <label v-for="o in q.options" :key="o.value" :class="{ picked: answers[q.key] === o.value }">
              <input v-model="answers[q.key]" type="radio" :name="q.key" :value="o.value" />
              {{ o.label }}
            </label>
          </div>
        </fieldset>

        <p v-if="error" class="error">{{ error }}</p>
        <button type="button" class="submit" :disabled="submitting" @click="submit">
          {{ submitting ? 'Matching…' : 'Show my matches' }}
        </button>
      </template>

      <template v-else>
        <h2>Your top matches</h2>
        <div v-for="(m, i) in results" :key="m.slug" class="match">
          <div class="match-head">
            <span class="match-name">{{ i === 0 ? '🏆 ' : '' }}{{ m.displayName }}</span>
            <span class="match-pct">{{ m.matchPercent }}% match</span>
          </div>
          <div class="bar"><div class="fill" :style="{ width: m.matchPercent + '%' }" /></div>
          <p class="blurb">{{ m.blurb }}</p>
          <div class="match-foot">
            <span class="price">Typical price: {{ m.typicalPrice }}</span>
            <span class="reasons">{{ m.reasons.join(' · ') }}</span>
          </div>
          <button type="button" class="pick" @click="emit('select', m.slug)">
            Search {{ m.displayName }}s everywhere →
          </button>
        </div>
        <button type="button" class="again" @click="reset">← Change my answers</button>
      </template>
    </div>
  </div>
</template>

<style scoped>
.overlay {
  position: fixed;
  inset: 0;
  background: rgba(28, 24, 38, 0.45);
  display: grid;
  place-items: center;
  padding: 1rem;
  z-index: 50;
}

.modal {
  position: relative;
  width: min(560px, 100%);
  max-height: 88vh;
  overflow-y: auto;
  background: var(--surface);
  border-radius: var(--radius);
  box-shadow: var(--shadow-hover);
  padding: 2rem 2.25rem;
}

.close {
  position: absolute;
  top: 1rem;
  right: 1rem;
  border: none;
  background: none;
  font-size: 1rem;
  color: var(--text-muted);
  cursor: pointer;
}

h2 {
  margin: 0 0 0.25rem;
}

.subtitle {
  color: var(--text-muted);
  margin: 0 0 1.25rem;
  font-size: 0.9rem;
}

fieldset {
  border: none;
  margin: 0 0 1rem;
  padding: 0;
}

legend {
  font-weight: 650;
  font-size: 0.9rem;
  color: var(--text-strong);
  margin-bottom: 0.4rem;
}

.options {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
}

.options label {
  padding: 0.4rem 0.9rem;
  border-radius: 999px;
  border: 1px solid var(--border);
  font-size: 0.85rem;
  cursor: pointer;
  transition: all 0.15s;
}

.options label.picked {
  background: var(--accent);
  border-color: var(--accent);
  color: #fff;
}

.options input {
  display: none;
}

.error {
  color: var(--accent);
  font-size: 0.85rem;
}

.submit {
  width: 100%;
  padding: 0.7rem;
  border-radius: 999px;
  border: none;
  background: var(--accent);
  color: #fff;
  font-size: 0.95rem;
  font-weight: 650;
  font-family: inherit;
  cursor: pointer;
}

.submit:disabled {
  opacity: 0.6;
}

.match {
  border-top: 1px solid var(--border);
  padding: 1rem 0;
}

.match-head {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
}

.match-name {
  font-weight: 650;
  color: var(--text-strong);
}

.match-pct {
  font-size: 0.85rem;
  color: var(--accent);
  font-weight: 650;
}

.bar {
  height: 6px;
  border-radius: 999px;
  background: var(--accent-soft);
  margin: 0.4rem 0 0.6rem;
  overflow: hidden;
}

.fill {
  height: 100%;
  background: var(--accent);
  border-radius: 999px;
}

.blurb {
  margin: 0 0 0.4rem;
  font-size: 0.85rem;
}

.match-foot {
  display: flex;
  justify-content: space-between;
  gap: 0.75rem;
  font-size: 0.75rem;
  color: var(--text-muted);
  margin-bottom: 0.6rem;
  flex-wrap: wrap;
}

.pick {
  border: 1px solid var(--accent);
  background: none;
  color: var(--accent);
  border-radius: 999px;
  padding: 0.4rem 1rem;
  font-size: 0.85rem;
  font-weight: 600;
  font-family: inherit;
  cursor: pointer;
}

.pick:hover {
  background: var(--accent);
  color: #fff;
}

.again {
  margin-top: 0.75rem;
  border: none;
  background: none;
  color: var(--text-muted);
  font-size: 0.85rem;
  font-family: inherit;
  cursor: pointer;
  text-decoration: underline;
}
</style>
