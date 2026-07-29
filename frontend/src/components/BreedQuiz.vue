<script setup>
import { computed, ref } from 'vue'
import { fetchBreedImage } from '../dogImages.js'
import { saveProfile } from '../adopterProfile.js'
import PuppyLogo from './PuppyLogo.vue'

const emit = defineEmits(['close', 'select', 'profile-saved'])

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
const photos = ref({}) // slug → dog.ceo image url, filled in as fetches resolve
const submitting = ref(false)
const error = ref('')
const profileSaved = ref(false)

// Persist the answers as "my profile": fetch fit scores for every quiz breed,
// store locally, and let the app re-rank live listings by fit.
async function saveAsProfile() {
  error.value = ''
  try {
    const res = await fetch('/api/quiz/scores', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(answers.value),
    })
    if (!res.ok) throw new Error(`API returned ${res.status}`)
    const profile = saveProfile(answers.value, await res.json())
    profileSaved.value = true
    emit('profile-saved', profile)
  } catch (e) {
    error.value = `Could not save your profile (${e.message})`
  }
}

const answered = computed(() => Object.values(answers.value).filter(Boolean).length)
const complete = computed(() => answered.value === QUESTIONS.length)

async function submit() {
  if (!complete.value) return
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
    photos.value = {}
    for (const m of results.value) {
      fetchBreedImage(m.imagePath).then((url) => {
        if (url) photos.value = { ...photos.value, [m.slug]: url }
      })
    }
  } catch (e) {
    error.value = `Could not score the quiz (${e.message})`
  } finally {
    submitting.value = false
  }
}

function reset() {
  results.value = null
  error.value = ''
  profileSaved.value = false
}
</script>

<template>
  <div class="modal modal-open" @click.self="emit('close')">
    <div class="modal-box max-w-xl">
      <button
        type="button"
        class="btn btn-sm btn-circle btn-ghost absolute top-3 right-3"
        @click="emit('close')"
      >
        ✕
      </button>

      <template v-if="!results">
        <div class="mb-5 flex items-center gap-3">
          <PuppyLogo class="h-14 w-14 shrink-0 drop-shadow-sm" />
          <div>
            <h2 class="font-display text-3xl leading-none font-semibold tracking-wide">Find your breed</h2>
            <p class="text-sm opacity-60">
              Six quick questions — we'll match you to the breeds that fit your life.
            </p>
          </div>
        </div>

        <fieldset v-for="q in QUESTIONS" :key="q.key" class="mb-4">
          <legend class="mb-2 text-xs font-bold tracking-wide uppercase opacity-60">{{ q.label }}</legend>
          <div class="flex flex-wrap gap-2">
            <button
              v-for="o in q.options"
              :key="o.value"
              type="button"
              class="btn btn-sm"
              :class="answers[q.key] === o.value ? 'btn-primary' : 'btn-outline'"
              @click="answers[q.key] = o.value"
            >
              {{ o.label }}
            </button>
          </div>
        </fieldset>

        <p v-if="error" class="text-error text-sm">{{ error }}</p>
        <p class="mb-2 text-center text-xs opacity-60">{{ answered }} of {{ QUESTIONS.length }} answered</p>
        <button
          type="button"
          class="btn btn-primary btn-block"
          :disabled="submitting || !complete"
          @click="submit"
        >
          <span v-if="submitting" class="loading loading-spinner loading-sm" />
          {{ submitting ? 'Matching…' : complete ? 'Show my matches' : 'Answer all six to continue' }}
        </button>
      </template>

      <template v-else>
        <div class="mb-4 flex items-center gap-3">
          <PuppyLogo class="h-14 w-14 shrink-0 drop-shadow-sm" />
          <h2 class="font-display text-3xl leading-none font-semibold tracking-wide">Your top matches</h2>
        </div>
        <div class="flex flex-col gap-3">
          <div v-for="(m, i) in results" :key="m.slug" class="card bg-base-200">
            <div class="card-body gap-2 p-4">
              <div class="flex items-center gap-3">
                <img
                  v-if="photos[m.slug]"
                  :src="photos[m.slug]"
                  :alt="m.displayName"
                  class="h-16 w-16 shrink-0 rounded-xl object-cover shadow-sm"
                  loading="lazy"
                />
                <div class="min-w-0 flex-1">
                  <div class="flex items-baseline justify-between gap-2">
                    <span class="card-title text-base">{{ i === 0 ? '🏆 ' : '' }}{{ m.displayName }}</span>
                    <span class="badge badge-primary badge-soft font-bold">{{ m.matchPercent }}% match</span>
                  </div>
                  <progress class="progress progress-primary w-full" :value="m.matchPercent" max="100" />
                </div>
              </div>
              <p class="text-sm">{{ m.blurb }}</p>
              <div class="flex flex-col justify-between gap-1 text-xs opacity-60 sm:flex-row">
                <span>Typical price: {{ m.typicalPrice }}</span>
                <span>{{ m.reasons.join(' · ') }}</span>
              </div>
              <button type="button" class="btn btn-primary btn-sm mt-1" @click="emit('select', m.slug)">
                Search {{ m.displayName }}s everywhere →
              </button>
            </div>
          </div>
        </div>
        <div class="mt-3 flex flex-wrap items-center gap-2">
          <button type="button" class="btn btn-ghost btn-sm" @click="reset">← Change my answers</button>
          <button
            v-if="!profileSaved"
            type="button"
            class="btn btn-secondary btn-sm"
            @click="saveAsProfile"
          >
            💾 Save as my profile
          </button>
          <span v-else class="text-success text-xs">
            ✓ Saved — adoptable listings are now sorted by fit to you.
          </span>
        </div>
        <p v-if="error && results" class="text-error mt-1 text-xs">{{ error }}</p>
      </template>
    </div>
  </div>
</template>
