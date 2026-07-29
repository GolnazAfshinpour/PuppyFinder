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
const myAlerts = ref(null) // null = not loaded; [] = loaded, none
const loadingAlerts = ref(false)

function describeAlert(a) {
  const parts = []
  if (a.size) parts.push(a.size)
  parts.push(a.breed ? a.breed.replaceAll('-', ' ') : 'any breed')
  if (a.city && a.state) parts.push(`in ${a.city}, ${a.state}`)
  else if (a.state) parts.push(`in ${a.state}`)
  else parts.push('anywhere')
  return parts.join(' · ')
}

async function loadMyAlerts() {
  if (!email.value.trim()) return
  loadingAlerts.value = true
  error.value = ''
  try {
    const res = await fetch(`/api/alerts?email=${encodeURIComponent(email.value.trim())}`)
    if (!res.ok) throw new Error(`API returned ${res.status}`)
    myAlerts.value = await res.json()
  } catch (e) {
    error.value = e.message
  } finally {
    loadingAlerts.value = false
  }
}

async function removeAlert(alert) {
  try {
    const res = await fetch(
      `/api/alerts/${alert.id}?email=${encodeURIComponent(alert.email)}`,
      { method: 'DELETE' },
    )
    if (res.ok || res.status === 404) {
      myAlerts.value = myAlerts.value.filter((a) => a.id !== alert.id)
    }
  } catch (e) {
    error.value = e.message
  }
}

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
    if (myAlerts.value !== null) await loadMyAlerts()
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

      <button
        v-if="myAlerts === null"
        type="button"
        class="link self-start text-xs opacity-70"
        :disabled="loadingAlerts || !email.trim()"
        @click="loadMyAlerts"
      >
        {{ loadingAlerts ? 'Loading…' : 'Show my existing alerts (enter your email above first)' }}
      </button>
      <div v-else class="text-xs">
        <p v-if="myAlerts.length === 0" class="opacity-60">No alerts saved for that email yet.</p>
        <ul v-else class="space-y-1">
          <li v-for="a in myAlerts" :key="a.id" class="flex items-center gap-2">
            <span>🔔 {{ describeAlert(a) }}</span>
            <button type="button" class="btn btn-ghost btn-xs text-error" @click="removeAlert(a)">
              remove
            </button>
          </li>
        </ul>
      </div>
    </div>
  </div>
</template>
