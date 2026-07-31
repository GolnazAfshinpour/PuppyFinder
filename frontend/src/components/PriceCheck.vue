<script setup>
import { computed, ref, watch } from 'vue'

const props = defineProps({
  breedSlug: { type: String, default: '' },
  breedName: { type: String, default: '' },
})

defineEmits(['open-guide'])

const quote = ref('')
const verdict = ref(null)
const checking = ref(false)
const error = ref('')

// A verdict is about one breed + one number. Changing the breed invalidates it —
// leaving a stale "typical for a Beagle" under a French Bulldog search would be
// worse than showing nothing.
watch(() => props.breedSlug, () => {
  verdict.value = null
})

async function check() {
  const price = Number(quote.value)
  if (quote.value === '' || Number.isNaN(price) || price < 0) {
    error.value = 'Enter the price you were quoted, in dollars.'
    return
  }
  checking.value = true
  error.value = ''
  try {
    const params = new URLSearchParams({ price: String(Math.round(price)) })
    if (props.breedSlug) params.set('breed', props.breedSlug)
    const res = await fetch(`/api/price-check?${params}`)
    if (!res.ok) throw new Error(`API returned ${res.status}`)
    verdict.value = await res.json()
  } catch (e) {
    error.value = `Couldn't check that price (${e.message})`
  } finally {
    checking.value = false
  }
}

// Warnings shout, everything else stays calm — a page that alarms at every number
// trains people to ignore it.
// One exception to the calm/shout split: a green "success" on a Typical verdict
// implies we checked the range, so it's only earned when the range is actually
// sourced. Against an unsourced estimate it drops to neutral info — the verdict text
// already says so, and the colour shouldn't contradict it.
const alertClass = computed(() => {
  if (!verdict.value) return ''
  if (verdict.value.isWarning) return 'alert-error'
  if (verdict.value.level === 'Typical') {
    return verdict.value.confidence === 'verified' ? 'alert-success' : 'alert-info'
  }
  return { Above: 'alert-warning', Unknown: 'alert-info' }[verdict.value.level] ?? 'alert-info'
})
</script>

<template>
  <section class="card border-primary/30 bg-base-100 border">
    <div class="card-body gap-3">
      <div>
        <h3 class="font-display text-xl font-semibold">Been quoted a price? Check it.</h3>
        <p class="text-sm opacity-70">
          A price far below market is the most reported hook in puppy fraud. This compares a
          quote against
          <template v-if="breedName">our range for a {{ breedName }}.</template>
          <template v-else>the breed's real range — pick a breed for the sharpest answer.</template>
        </p>
      </div>

      <form class="flex flex-wrap items-center gap-2" @submit.prevent="check">
        <label class="input input-bordered flex items-center gap-1">
          <span class="opacity-60">$</span>
          <input
            v-model="quote"
            type="number"
            min="0"
            step="50"
            inputmode="numeric"
            class="w-28 grow"
            placeholder="1200"
            aria-label="Price you were quoted, in dollars"
          />
        </label>
        <button type="submit" class="btn btn-primary" :disabled="checking">
          <span v-if="checking" class="loading loading-spinner loading-xs" />
          Check this price
        </button>
      </form>

      <p v-if="error" class="text-error text-sm">{{ error }}</p>

      <div
        v-if="verdict"
        role="alert"
        data-testid="price-verdict"
        class="alert alert-soft items-start gap-2 text-sm"
        :class="alertClass"
      >
        <span>
          <strong class="block">{{ verdict.isWarning ? '🚩 ' : '' }}{{ verdict.headline }}</strong>
          <span class="mt-1 block">{{ verdict.detail }}</span>
          <button type="button" class="link mt-1 font-semibold" @click="$emit('open-guide')">
            Full safety checklist →
          </button>
        </span>
      </div>
    </div>
  </section>
</template>
