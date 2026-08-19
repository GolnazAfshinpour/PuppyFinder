<script setup>
import { computed, ref } from 'vue'

// The only check in the app that ends in a public database rather than in advice.
//
// Under the Animal Welfare Act a breeder needs a USDA Class A licence when they keep more than
// four breeding females AND sell sight-unseen. The line that does the work: a puppy shipped to
// you is not a face-to-face sale, so a seller who won't let you see the dog first cannot be
// leaning on the retail exemption.
//
// All the judgement lives in backend/Services/SellerCheck.cs, same as the fee and price checks.

const delivery = ref('')
const licence = ref('')
const verdict = ref(null)
const checking = ref(false)
const error = ref('')

const DELIVERY = [
  { value: 'sight-unseen', label: "They'd ship it to me" },
  { value: 'in-person', label: "I'd see the puppy first" },
]

// Only asked once we know the sale is sight-unseen — in person the rule doesn't apply and the
// question would be noise dressed up as diligence.
const LICENCE = [
  { value: 'given', label: 'They gave me a number' },
  { value: 'exempt', label: "They say they don't need one" },
  { value: 'refused', label: "They won't say" },
]

const shipping = computed(() => delivery.value === 'sight-unseen')

const alertClass = computed(() => {
  if (!verdict.value) return ''
  if (verdict.value.isWarning) return 'alert-error'
  return verdict.value.level === 'Exempt' ? 'alert-success' : 'alert-info'
})

async function check() {
  if (!delivery.value) {
    error.value = 'Pick how you would get the puppy.'
    return
  }
  checking.value = true
  error.value = ''
  try {
    const params = new URLSearchParams({ delivery: delivery.value })
    if (licence.value) params.set('licence', licence.value)
    const res = await fetch(`/api/seller-check?${params}`)
    if (!res.ok) throw new Error(`API returned ${res.status}`)
    verdict.value = await res.json()
  } catch (e) {
    error.value = `Couldn't check that (${e.message})`
  } finally {
    checking.value = false
  }
}

function setDelivery(value) {
  delivery.value = value
  // The licence answer only means anything alongside a delivery method, so changing one
  // invalidates the other rather than leaving a stale pairing on screen.
  if (value !== 'sight-unseen') licence.value = ''
  verdict.value = null
}

function setLicence(value) {
  licence.value = value
  verdict.value = null
}
</script>

<template>
  <section class="card bg-base-100 card-lift" data-testid="seller-check">
    <div class="card-body gap-4">
      <div>
        <p class="text-xs font-bold tracking-wide uppercase opacity-60">Before you send a deposit</p>
        <h2 class="font-display mt-0.5 text-2xl font-semibold">Is this breeder licensed?</h2>
        <p class="mt-1 max-w-prose text-sm opacity-70">
          A breeder who keeps more than four breeding females <em>and</em> sells sight-unseen is
          required by federal law to hold a USDA licence — and you can look it up.
        </p>
      </div>

      <form class="flex flex-col gap-3" @submit.prevent="check">
        <fieldset class="flex flex-wrap items-center gap-2">
          <legend class="mb-1 text-xs font-bold tracking-wide uppercase opacity-60">
            How would you get the puppy?
          </legend>
          <button
            v-for="option in DELIVERY"
            :key="option.value"
            type="button"
            class="btn btn-sm"
            :class="delivery === option.value ? 'btn-primary' : 'btn-outline'"
            :aria-pressed="delivery === option.value"
            @click="setDelivery(option.value)"
          >
            {{ option.label }}
          </button>
        </fieldset>

        <!-- Only relevant for a sight-unseen sale. Asking it of someone collecting the puppy in
             person would imply the answer matters there, and it doesn't. -->
        <fieldset v-if="shipping" class="flex flex-wrap items-center gap-2">
          <legend class="mb-1 text-xs font-bold tracking-wide uppercase opacity-60">
            Did you ask for their USDA licence number?
          </legend>
          <button
            v-for="option in LICENCE"
            :key="option.value"
            type="button"
            class="btn btn-sm"
            :class="licence === option.value ? 'btn-primary' : 'btn-outline'"
            :aria-pressed="licence === option.value"
            @click="setLicence(option.value)"
          >
            {{ option.label }}
          </button>
        </fieldset>

        <button type="submit" class="btn btn-primary self-start" :disabled="checking">
          {{ checking ? 'Checking…' : 'What does that mean?' }}
        </button>
      </form>

      <p v-if="error" class="text-error text-sm">{{ error }}</p>

      <div
        v-if="verdict"
        role="alert"
        data-testid="seller-verdict"
        class="alert alert-soft items-start gap-2 text-sm"
        :class="alertClass"
      >
        <div class="max-w-prose">
          <strong class="block">{{ verdict.isWarning ? '🚩 ' : '' }}{{ verdict.headline }}</strong>
          <p class="mt-1">{{ verdict.detail }}</p>

          <template v-if="verdict.actions?.length">
            <p class="mt-3 text-xs font-bold tracking-wide uppercase opacity-60">What to do now</p>
            <ul data-testid="seller-actions" class="mt-1 list-none space-y-2 p-0">
              <li v-for="action in verdict.actions" :key="action.text" class="flex gap-2">
                <span class="shrink-0 opacity-60">•</span>
                <span>
                  {{ action.text }}
                  <a
                    v-if="action.href"
                    :href="action.href"
                    target="_blank"
                    rel="noopener noreferrer"
                    class="link font-semibold whitespace-nowrap"
                  >Open the USDA search ↗</a>
                </span>
              </li>
            </ul>
          </template>

          <a href="/safe#vet-a-breeder" class="link mt-3 block font-semibold">
            The rest of how to vet a breeder →
          </a>
        </div>
      </div>
    </div>
  </section>
</template>
