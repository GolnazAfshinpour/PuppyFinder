<script setup>
import { computed, ref } from 'vue'

// The price check answers "is this quote plausible for this breed", which needs a sourced range
// and is therefore silent for most breeds. This answers "they are asking me for $350, should I
// send it" — no range required, so it works for all 174 breeds, and it is the only check in the
// app pointed at someone who has already paid.
//
// All the judgement lives in the API (backend/Services/FeeCheck.cs), the way the price gate
// does: this file must not be the place a verdict gets decided.

const fee = ref('')
// Both questions start unanswered, and neither is defaulted. Guessing either would hand the
// calmer answer to the reader who least needs it.
const alreadyPaid = ref(null)
const asker = ref('')
const verdict = ref(null)
const checking = ref(false)
const error = ref('')

// Real phrasings, not category names. Someone mid-scam is holding a message that says
// "refundable crate deposit", and recognising their own words is most of the value.
const EXAMPLES = [
  'a $350 refundable crate deposit',
  'shipping insurance',
  'she\'s stuck at the airport, release fee',
  'a deposit to hold a puppy',
]

// The scam has two actors: BBB's script is that after the first payment a second party appears
// posing as a shipping company, and every fee from there comes from them. Distinguishing a
// transporter who contacted you from one you found yourself is the whole reason to ask — the
// second is a real company sending a real invoice.
const ASKERS = [
  { value: 'seller', label: 'The seller or breeder' },
  { value: 'transporter-contacted-me', label: 'A transport company that contacted me' },
  { value: 'transporter-i-booked', label: 'A transporter I found and booked' },
]

const alertClass = computed(() => {
  if (!verdict.value) return ''
  if (verdict.value.isWarning) return 'alert-error'
  return verdict.value.level === 'Real' ? 'alert-info' : 'alert-warning'
})

async function check() {
  if (!fee.value.trim()) {
    error.value = 'Type what they are asking money for.'
    return
  }
  checking.value = true
  error.value = ''
  try {
    const params = new URLSearchParams({
      fee: fee.value.trim(),
      paid: String(alreadyPaid.value === true),
    })
    if (asker.value) params.set('asker', asker.value)
    const res = await fetch(`/api/fee-check?${params}`)
    if (!res.ok) throw new Error(`API returned ${res.status}`)
    verdict.value = await res.json()
  } catch (e) {
    error.value = `Couldn't check that (${e.message})`
  } finally {
    checking.value = false
  }
}

// Changing an answer invalidates the verdict: "stop paying" and "don't send it" are different
// instructions, and leaving the wrong one on screen under a changed answer is worse than none.
//
// Three functions rather than one taking the ref: refs auto-unwrap in templates, so a generic
// setter would be handed the string value and silently assign to nothing.
function setFee(value) {
  fee.value = value
  verdict.value = null
}
function setPaid(value) {
  alreadyPaid.value = value
  verdict.value = null
}
function setAsker(value) {
  asker.value = value
  verdict.value = null
}
</script>

<template>
  <section class="card bg-base-100 card-lift" data-testid="fee-check">
    <div class="card-body gap-4">
      <div>
        <p class="text-xs font-bold tracking-wide uppercase opacity-60">Before you send it</p>
        <h2 class="font-display mt-0.5 text-2xl font-semibold">They're asking for a fee</h2>
        <p class="mt-1 max-w-prose text-sm opacity-70">
          Type what the seller wants money for. You don't need to know what the breed costs —
          this asks whether the fee itself is real.
        </p>
      </div>

      <form class="flex flex-col gap-3" @submit.prevent="check">
        <label class="flex flex-col gap-1">
          <span class="sr-only">What are they asking money for?</span>
          <input
            v-model="fee"
            type="text"
            class="input input-bordered w-full"
            placeholder="e.g. a $350 refundable crate deposit"
            aria-label="What the seller is asking money for"
          />
        </label>

        <div class="flex flex-wrap gap-1.5">
          <button
            v-for="example in EXAMPLES"
            :key="example"
            type="button"
            class="badge badge-outline hover:badge-primary cursor-pointer py-3"
            @click="setFee(example)"
          >
            {{ example }}
          </button>
        </div>

        <!--
          The question that decides the answer. BBB's finding is that the scam is profitable
          because a "multi-tiered setup" lets the seller come back for money several times, so
          whether money has already moved changes the instruction from "don't send it" to
          "stop".
        -->
        <fieldset class="flex flex-wrap items-center gap-2">
          <legend class="mb-1 text-xs font-bold tracking-wide uppercase opacity-60">
            Have you already sent this seller money?
          </legend>
          <button
            type="button"
            class="btn btn-sm"
            :class="alreadyPaid === false ? 'btn-primary' : 'btn-outline'"
            :aria-pressed="alreadyPaid === false"
            @click="setPaid(false)"
          >
            Not yet
          </button>
          <button
            type="button"
            class="btn btn-sm"
            :class="alreadyPaid === true ? 'btn-primary' : 'btn-outline'"
            :aria-pressed="alreadyPaid === true"
            @click="setPaid(true)"
          >
            Yes, I've paid
          </button>
        </fieldset>

        <!--
          The second actor. A shipping company that made contact on its own — one the buyer never
          chose — is the handoff itself, and that is a finding whatever the fee is called. Asked
          separately from "who did you pay" because the two are different people in this scam and
          the buyer is usually not aware that is the point.
        -->
        <fieldset class="flex flex-wrap items-center gap-2">
          <legend class="mb-1 text-xs font-bold tracking-wide uppercase opacity-60">
            Who is asking for it?
          </legend>
          <button
            v-for="option in ASKERS"
            :key="option.value"
            type="button"
            class="btn btn-sm"
            :class="asker === option.value ? 'btn-primary' : 'btn-outline'"
            :aria-pressed="asker === option.value"
            @click="setAsker(option.value)"
          >
            {{ option.label }}
          </button>
        </fieldset>

        <button type="submit" class="btn btn-primary self-start" :disabled="checking">
          {{ checking ? 'Checking…' : 'Check this fee' }}
        </button>
      </form>

      <p v-if="error" class="text-error text-sm">{{ error }}</p>

      <div
        v-if="verdict"
        role="alert"
        data-testid="fee-verdict"
        class="alert alert-soft items-start gap-2 text-sm"
        :class="alertClass"
      >
        <div class="max-w-prose">
          <strong class="block">{{ verdict.isWarning ? '🚩 ' : '' }}{{ verdict.headline }}</strong>
          <!-- Named back so the reader can tell we understood them, and so a wrong match is
               visible rather than silent. -->
          <p v-if="verdict.matched" class="mt-1 text-xs opacity-70">
            Read as: {{ verdict.matched }}<template v-if="verdict.amount"> · ${{ verdict.amount.toLocaleString() }}</template>
          </p>
          <p class="mt-1">{{ verdict.detail }}</p>

          <!--
            Separated from the prose on purpose. The detail explains; these are the things to go
            and do, and burying a test that settles the question inside a paragraph is how it
            gets skipped.
          -->
          <template v-if="verdict.actions?.length">
            <p class="mt-3 text-xs font-bold tracking-wide uppercase opacity-60">What to do now</p>
            <ul data-testid="fee-actions" class="mt-1 list-none space-y-2 p-0">
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
                  >Open the directory ↗</a>
                </span>
              </li>
            </ul>
          </template>

          <a href="/safe#escalating-fees" class="link mt-3 block font-semibold">
            What happens next, and what to save →
          </a>
        </div>
      </div>
    </div>
  </section>
</template>
