<script setup>
import { computed } from 'vue'
import PriceProvenance from './PriceProvenance.vue'
import { articleFor } from '../article.js'

const props = defineProps({
  breed: { type: Object, default: null }, // the selected breed from /api/breeds
  breeds: { type: Array, default: () => [] },
})

const emit = defineEmits(['pick-breed', 'open-quiz', 'open-guide', 'open-prices'])

// A range is only shown when it's sourced. Displaying "$2,500–$5,000" while
// refusing to check quotes against it would invite the reader to do the same
// comparison in their head, minus the caveat — the harm without the honesty.
const hasRange = computed(
  () => props.breed?.priceLow != null && props.breed?.confidence === 'verified',
)

const sourcedCount = computed(
  () => props.breeds.filter((b) => b.confidence === 'verified').length,
)

// Real ranges to orient someone who hasn't picked a breed yet, and a way in: each is
// clickable. Ordered by how many live listings back them, which is a data-driven proxy for
// "breeds people are actually shopping for" — sorting by price instead surfaced only the
// most expensive breeds, which reads as a sales pitch rather than orientation.
const examples = computed(() =>
  props.breeds
    .filter((b) => b.confidence === 'verified' && b.priceLow != null)
    .sort((a, b) => (b.sourceCount ?? 0) - (a.sourceCount ?? 0))
    .slice(0, 6),
)

// Three genuinely different situations, which the old copy answered with one sentence —
// "We're not publishing price ranges or checking quotes yet". That was true when nothing was
// sourced and is now false for 50 breeds: it understated what the app has, having previously
// been written to avoid overstating it.
const state = computed(() => {
  if (!props.breed) return 'pick'
  return props.breed.priceLow == null ? 'no-data' : 'below-bar'
})

const headline = computed(() => {
  const name = props.breed?.displayName
  switch (state.value) {
    case 'pick':
      return 'Check a quote against the real market'
    case 'no-data':
      return `We have no price data for ${articleFor(name)} ${name}`
    default:
      return `No sourced range for ${articleFor(name)} ${name} yet`
  }
})

const explanation = computed(() => {
  const name = props.breed?.displayName
  switch (state.value) {
    case 'pick':
      return `${sourcedCount.value} breeds have a range built from live asking prices, so a quote`
        + ' can be checked against what people are really charging. Pick a breed to see its'
        + ' range — or use the checks below, which work whatever the price says.'
    case 'no-data':
      return `${name} is rare enough on the open market that there aren't enough live listings`
        + ' to build a range from — and we would rather show nothing than a number we invented.'
        + ' The checks below do not depend on knowing the price.'
    default:
      return `We have figures for ${name}, but they don't clear the bar: too few independent`
        + ' sources, or they disagree too widely to call anything typical. Calling a quote a scam'
        + ' means measuring it against something we can stand behind, so screening stays off for'
        + ' this breed until it is.'
  }
})

// Breed-agnostic and factual: these are the things that legitimately move a
// purebred puppy's price, so a buyer can ask which ones apply to their quote.
const PRICE_DRIVERS = [
  ['Health testing on the parents', 'OFA / PennHIP / Embark screening costs the breeder real money and is the main thing worth paying extra for.'],
  ['Pedigree', 'Champion or titled lines command more. Ask to see the registration, not just hear about it.'],
  ['Breeder reputation', 'Established breeders with waitlists charge more and screen you harder. Both are good signs.'],
  ['Colour and markings', '"Rare" colours carry a premium — but in several breeds they are disqualifying colours linked to health problems.'],
  ['Location and transport', 'Whether flight or ground transport is included can swing the total by four figures.'],
]

// The checks that don't need a price to be useful — these are the substance of the
// advice while price screening is switched off.
const PRICE_FREE_CHECKS = [
  'Get quotes from three breeders. The one that sharply undercuts the others is the outlier, not the bargain.',
  'See the puppy and its mother on a live video call, or in person. A refusal here ends the conversation.',
  'Ask for OFA, PennHIP or Embark results for both parents — on paper, not described.',
  'Never pay by wire transfer, gift card, Zelle or crypto. Those are chosen because they are unrecoverable.',
  'Walk away from any fee that appears after you commit — shipping insurance, a climate-controlled crate, a vaccine deposit.',
]

// No invented totals: the point is that the sticker price is not the cost.
const ONGOING_COSTS = [
  'First-year vet care: exam series, core vaccinations, deworming, microchip',
  'Spay or neuter, unless the contract requires you to wait',
  'Food, crate, bed, leads, and the things you break in month one',
  'Training — non-negotiable for large or high-energy breeds',
  'Pet insurance or a savings buffer, before a problem appears rather than after',
]
</script>

<template>
  <section class="card bg-base-100 card-lift">
    <div class="card-body gap-4">
      <template v-if="hasRange">
        <div>
          <p class="text-xs font-bold tracking-wide uppercase opacity-60">
            What {{ articleFor(breed.displayName) }} {{ breed.displayName }} actually costs
          </p>
          <p class="font-display text-primary mt-1 text-4xl font-semibold">{{ breed.typicalPrice }}</p>
          <p class="mt-1 text-sm opacity-70">
            What a puppy from a breeder tends to go for. Anything far below this is the
            single most reported puppy-scam signal — check a quote below.
          </p>
          <!-- The range labels its own reliability; the copy never asserts more than
               the data supports. -->
          <PriceProvenance :breed="breed" class="mt-2" />
          <button
            v-if="sourcedCount > 1"
            type="button"
            class="link mt-2 block text-xs font-semibold"
            @click="emit('open-prices')"
          >
            Compare with the other {{ sourcedCount - 1 }} sourced ranges →
          </button>
        </div>

        <p v-if="breed.blurb" class="border-base-300 border-l-2 pl-3 text-sm italic opacity-80">
          {{ breed.blurb }}
        </p>

        <div>
          <p class="mb-2 text-sm font-semibold">What moves the price inside that range</p>
          <ul class="space-y-1.5 text-sm">
            <li v-for="[label, why] in PRICE_DRIVERS" :key="label" class="flex gap-2">
              <span class="text-primary/80 shrink-0">•</span>
              <span><strong>{{ label }}</strong> — {{ why }}</span>
            </li>
          </ul>
        </div>

        <details class="collapse-arrow bg-base-200 collapse">
          <summary class="collapse-title text-sm font-semibold">
            The purchase price is the smaller half — what else to budget
          </summary>
          <div class="collapse-content">
            <ul class="list-inside list-disc space-y-1 text-sm">
              <li v-for="c in ONGOING_COSTS" :key="c">{{ c }}</li>
            </ul>
            <p class="mt-2 text-xs opacity-60">
              We don't publish dollar estimates for these — they vary too much by city and
              by dog for a number to mean anything. Get quotes from a local vet before you commit.
            </p>
          </div>
        </details>
      </template>

      <!-- No sourced range for this breed. Say which of the three reasons applies, then
           give advice that doesn't depend on a price. -->
      <template v-else>
        <div>
          <p class="font-display text-2xl font-semibold">{{ headline }}</p>
          <p class="mt-1 text-sm opacity-70">{{ explanation }}</p>
        </div>

        <!-- Nothing picked yet: show real ranges and let them be the way in. Naming the
             breeds we *can* answer for beats asking someone to guess which those are. -->
        <div v-if="state === 'pick' && examples.length">
          <p class="mb-2 text-sm font-semibold">Breeds with a sourced range</p>
          <div class="flex flex-wrap gap-2">
            <button
              v-for="b in examples"
              :key="b.slug"
              type="button"
              class="btn btn-outline btn-sm normal-case"
              @click="emit('pick-breed', b.slug)"
            >
              {{ b.displayName }}
              <span class="opacity-60">{{ b.typicalPrice }}</span>
            </button>
            <!-- Six of fifty, so the way to the rest belongs right here rather than only in
                 the header. This is where someone asking "which breeds do you cover?" is
                 looking. -->
            <button
              v-if="sourcedCount > examples.length"
              type="button"
              class="btn btn-ghost btn-sm normal-case"
              @click="emit('open-prices')"
            >
              See all {{ sourcedCount }} →
            </button>
          </div>
        </div>

        <div>
          <p class="mb-2 text-sm font-semibold">What to check before you send money</p>
          <ul class="space-y-1.5 text-sm">
            <li v-for="check in PRICE_FREE_CHECKS" :key="check" class="flex gap-2">
              <span class="text-primary/80 shrink-0">•</span>
              <span>{{ check }}</span>
            </li>
          </ul>
        </div>

        <div class="flex flex-wrap gap-2">
          <button type="button" class="btn btn-primary btn-sm" @click="emit('open-guide')">
            🛡️ Full scam-safety checklist
          </button>
          <button v-if="!breed" type="button" class="btn btn-outline btn-sm" @click="emit('open-quiz')">
            🐾 Not sure which breed? Take the quiz
          </button>
        </div>
      </template>
    </div>
  </section>
</template>
