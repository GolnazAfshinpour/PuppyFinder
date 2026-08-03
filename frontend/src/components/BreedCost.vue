<script setup>
import { computed } from 'vue'
import PriceProvenance from './PriceProvenance.vue'
import { articleFor } from '../article.js'

const props = defineProps({
  breed: { type: Object, default: null }, // the selected breed from /api/breeds
  breeds: { type: Array, default: () => [] },
})

const emit = defineEmits(['pick-breed', 'open-quiz', 'open-guide'])

// A range is only shown when it's sourced. Displaying "$2,500–$5,000" while
// refusing to check quotes against it would invite the reader to do the same
// comparison in their head, minus the caveat — the harm without the honesty.
const hasRange = computed(
  () => props.breed?.priceLow != null && props.breed?.confidence === 'verified',
)

// A few well-known ranges to orient someone who hasn't chosen yet — real data,
// not a sales pitch, and it makes the "is my quote sane?" question concrete.
const examples = computed(() =>
  props.breeds
    .filter((b) => b.priceLow != null)
    .sort((a, b) => b.priceHigh - a.priceHigh)
    .slice(0, 4),
)

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

      <!-- No sourced range: say what we don't know, then give advice that doesn't
           depend on knowing it. -->
      <template v-else>
        <div>
          <p class="font-display text-2xl font-semibold">
            What to check before you send money
          </p>
          <p class="mt-1 text-sm opacity-70">
            We're not publishing price ranges or checking quotes yet. Calling a quote a scam
            means measuring it against a number we can stand behind, and we'd rather say
            nothing than wrongly accuse a legitimate breeder — or reassure you about a real
            one. Everything below works without a price.
          </p>
        </div>

        <ul class="space-y-1.5 text-sm">
          <li v-for="check in PRICE_FREE_CHECKS" :key="check" class="flex gap-2">
            <span class="text-primary/80 shrink-0">•</span>
            <span>{{ check }}</span>
          </li>
        </ul>

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
