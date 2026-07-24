<script setup>
import { computed, ref } from 'vue'

const props = defineProps({
  breed: { type: String, default: '' },
  breedName: { type: String, default: '' },
  state: { type: String, default: '' },
  city: { type: String, default: '' },
  size: { type: String, default: '' },
})

const email = ref('')
const saving = ref(false)
const savedFor = ref('') // human summary of the alert just created
const error = ref('')

const filterSummary = computed(() => {
  const parts = []
  if (props.size) parts.push(props.size)
  parts.push(props.breedName || 'any breed')
  if (props.city && props.state) parts.push(`in ${props.city}, ${props.state}`)
  else if (props.state) parts.push(`in ${props.state}`)
  else parts.push('anywhere we have live feeds')
  return parts.join(' · ')
})

async function save() {
  if (!email.value.trim()) return
  saving.value = true
  error.value = ''
  savedFor.value = ''
  try {
    const res = await fetch('/api/alerts', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        email: email.value.trim(),
        breed: props.breed || null,
        state: props.state || null,
        city: props.state ? props.city.trim() || null : null,
        size: props.size || null,
      }),
    })
    if (!res.ok) throw new Error((await res.text()).replaceAll('"', '') || `API returned ${res.status}`)
    savedFor.value = filterSummary.value
    email.value = ''
  } catch (e) {
    error.value = e.message
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="card bg-base-200 mb-5">
    <div class="card-body gap-2 p-4">
      <p class="text-sm font-semibold">🔔 New dogs matching this search? Get an email.</p>
      <p class="text-xs opacity-60">Watching: {{ filterSummary }} — one email per new dog, unsubscribe anytime.</p>
      <form class="join w-full max-w-md" @submit.prevent="save">
        <input
          v-model="email"
          type="email"
          required
          placeholder="you@example.com"
          class="input input-bordered join-item input-sm flex-1"
        />
        <button type="submit" class="btn btn-primary join-item btn-sm" :disabled="saving">
          <span v-if="saving" class="loading loading-spinner loading-xs" />
          Alert me
        </button>
      </form>
      <p v-if="savedFor" class="text-success text-xs">✓ Alert saved — we'll email you about new dogs ({{ savedFor }}).</p>
      <p v-if="error" class="text-error text-xs">{{ error }}</p>
    </div>
  </div>
</template>
