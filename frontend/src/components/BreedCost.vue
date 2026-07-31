<script setup>
import { computed } from 'vue'
import PriceProvenance from './PriceProvenance.vue'

const props = defineProps({
  breed: { type: Object, default: null }, // the selected breed from /api/breeds
  breeds: { type: Array, default: () => [] },
})

const emit = defineEmits(['pick-breed', 'open-quiz'])

const hasRange = computed(() => props.breed?.priceLow != null)

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
            What a {{ breed.displayName }} actually costs
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

      <!-- Breed chosen, but we have no range for it at all. Say so plainly. -->
      <template v-else-if="breed">
        <div>
          <p class="text-xs font-bold tracking-wide uppercase opacity-60">
            {{ breed.displayName }}
          </p>
          <p class="mt-1 text-lg font-semibold">We don't have a price range for this breed yet</p>
          <p class="mt-1 text-sm opacity-70">
            Get quotes from at least three breeders to find the going rate yourself — then treat
            any quote that undercuts the rest sharply as the outlier, not the bargain.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <button
            v-for="b in examples"
            :key="b.slug"
            type="button"
            class="btn btn-outline btn-sm"
            @click="emit('pick-breed', b.slug)"
          >
            {{ b.displayName }} {{ b.typicalPrice }}
          </button>
        </div>
      </template>

      <!-- Nothing chosen yet. -->
      <template v-else>
        <div>
          <p class="font-display text-2xl font-semibold">Know the price before you talk to a seller</p>
          <p class="mt-1 text-sm opacity-70">
            Pick a breed and we'll show what it typically sells for, so you can spot a quote
            that's too good to be true. That one number stops most puppy scams.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <button
            v-for="b in examples"
            :key="b.slug"
            type="button"
            class="btn btn-outline btn-sm"
            @click="emit('pick-breed', b.slug)"
          >
            {{ b.displayName }} {{ b.typicalPrice }}
          </button>
        </div>
        <button type="button" class="link self-start text-sm" @click="emit('open-quiz')">
          Not sure which breed yet? Take the quiz →
        </button>
      </template>
    </div>
  </section>
</template>
