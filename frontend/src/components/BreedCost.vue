<script setup>
import { computed, ref, watch } from 'vue'
import PriceProvenance from './PriceProvenance.vue'
import PriceMeter from './PriceMeter.vue'
import { articleFor } from '../article.js'

const props = defineProps({
  breed: { type: Object, default: null }, // the selected breed from /api/breeds
  breeds: { type: Array, default: () => [] },
  photo: { type: String, default: null }, // already fetched in App.vue for the adopt path
})

const emit = defineEmits(['pick-breed', 'open-quiz', 'open-prices'])

// ---- the quote checker, absorbed from PriceCheck.vue
//
// One card rather than two: the verdict belongs *on* the price, not in a separate panel
// below it. Splitting them duplicated the breed name, the "far below market is the most
// reported signal" line, and a heading, and put the answer a scroll away from the question.
const quote = ref('')
const checkedQuote = ref(null)
const verdict = ref(null)
const checking = ref(false)
const error = ref('')

// A verdict is about one breed and one number. Changing breed invalidates it — leaving
// "typical for a Beagle" under a French Bulldog would be worse than showing nothing.
watch(() => props.breed?.slug, () => {
  verdict.value = null
  checkedQuote.value = null
  quote.value = ''
  error.value = ''
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
    if (props.breed?.slug) params.set('breed', props.breed.slug)
    const res = await fetch(`/api/price-check?${params}`)
    if (!res.ok) throw new Error(`API returned ${res.status}`)
    verdict.value = await res.json()
    checkedQuote.value = Math.round(price)
  } catch (e) {
    error.value = `Couldn't check that price (${e.message})`
  } finally {
    checking.value = false
  }
}

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

// Breed-agnostic and factual: the things that legitimately move a purebred puppy's price, so
// a buyer can ask which ones apply to their quote. Deferred behind an accordion — five
// bold-label bullets at full width were most of the card's bulk and none of its point.
//
// Icons are Heroicons outline paths per DESIGN.md §4 (emoji are content warmth only, never
// bullets), carried as `d` attributes so no icon dependency is added for five glyphs.
const PRICE_DRIVERS = [
  {
    label: 'Health testing on the parents',
    why: 'OFA / PennHIP / Embark screening costs the breeder real money, and is the main thing worth paying extra for.',
    icon: 'M4.5 12.75l6 6 9-13.5', // check
  },
  {
    label: 'Pedigree',
    why: 'Champion or titled lines command more. Ask to see the registration, not just hear about it.',
    icon: 'M16.5 18.75h-9m9 0a3 3 0 013 3h-15a3 3 0 013-3m9 0v-3.375c0-.621-.503-1.125-1.125-1.125h-.871M7.5 18.75v-3.375c0-.621.504-1.125 1.125-1.125h.872m5.007 0H9.497m5.007 0a7.454 7.454 0 01-.982-3.172M9.497 14.25a7.454 7.454 0 00.981-3.172M5.25 4.236c-.982.143-1.954.317-2.916.52A6.003 6.003 0 007.73 9.728M5.25 4.236V4.5c0 2.108.966 3.99 2.48 5.228M5.25 4.236V2.721C7.456 2.41 9.71 2.25 12 2.25c2.291 0 4.545.16 6.75.47v1.516M7.73 9.728a6.726 6.726 0 002.748 1.35m8.272-6.842V4.5c0 2.108-.966 3.99-2.48 5.228m2.48-5.492a46.32 46.32 0 012.916.52 6.003 6.003 0 01-5.395 4.972m0 0a6.726 6.726 0 01-2.749 1.35m0 0a6.772 6.772 0 01-3.044 0', // trophy
  },
  {
    label: 'Breeder reputation',
    why: 'Established breeders with waitlists charge more and screen you harder. Both are good signs.',
    icon: 'M15 19.128a9.38 9.38 0 002.625.372 9.337 9.337 0 004.121-.952 4.125 4.125 0 00-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 018.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0111.964-3.07M12 6.375a3.375 3.375 0 11-6.75 0 3.375 3.375 0 016.75 0zm8.25 2.25a2.625 2.625 0 11-5.25 0 2.625 2.625 0 015.25 0z', // users
  },
  {
    label: 'Colour and markings',
    why: '"Rare" colours carry a premium — but in several breeds they are disqualifying colours linked to health problems.',
    icon: 'M4.098 19.902a3.75 3.75 0 005.304 0l6.401-6.402M6.75 21A3.75 3.75 0 013 17.25V4.125C3 3.504 3.504 3 4.125 3h5.25c.621 0 1.125.504 1.125 1.125v4.072M6.75 21a3.75 3.75 0 003.75-3.75V8.197M6.75 21h13.125c.621 0 1.125-.504 1.125-1.125v-5.25c0-.621-.504-1.125-1.125-1.125h-4.072M10.5 8.197l2.88-2.88c.438-.439 1.15-.439 1.59 0l3.712 3.713c.44.44.44 1.152 0 1.59l-2.879 2.88M6.75 17.25h.008v.008H6.75v-.008z', // swatch
  },
  {
    label: 'Location and transport',
    why: 'Whether flight or ground transport is included can swing the total by four figures.',
    icon: 'M6 12L3.269 3.126A59.768 59.768 0 0121.485 12 59.77 59.77 0 013.27 20.876L5.999 12zm0 0h7.5', // paper-airplane
  },
]

// Warnings shout, everything else stays calm — a page that alarms at every number trains
// people to ignore it. A green "success" implies we checked against a sourced range, so it is
// only earned when the range is actually sourced; the card only renders this branch when it
// is, but the guard stays so the rule survives a future caller.
const alertClass = computed(() => {
  if (!verdict.value) return ''
  if (verdict.value.isWarning) return 'alert-error'
  if (verdict.value.level === 'Typical') {
    return verdict.value.confidence === 'verified' ? 'alert-success' : 'alert-info'
  }
  return { Above: 'alert-warning', Unknown: 'alert-info' }[verdict.value.level] ?? 'alert-info'
})

// The checks that don't need a price to be useful — these are the substance of the
// advice while price screening is switched off.
const PRICE_FREE_CHECKS = [
  'Get quotes from three breeders. The one that sharply undercuts the others is the outlier, not the bargain.',
  'See the puppy and its mother in person, or on a video call where you name what they do on the spot — pick the puppy up, show today\'s date. A refusal ends the conversation; a pre-recorded clip proves nothing.',
  'Ask for OFA, PennHIP or Embark results for both parents — on paper, not described.',
  'Never pay by wire transfer, Western Union, MoneyGram, gift card, Zelle or crypto. Those are chosen because they are unrecoverable.',
  'Offer to collect the dog yourself, this week, in your own car. A real puppy can be picked up; a seller who won\'t arrange it has answered the question.',
  'Walk away from any fee that appears after you commit — shipping insurance, a climate-controlled crate, a vaccine deposit. Especially if a "shipping company" you never chose is the one asking.',
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
        <!-- Photo + number + provenance in one row. 56% of users go to images first, and a
             puppy-purchase page previously showed no puppy — the photo was already fetched
             for the adopt path and simply never rendered here. -->
        <div class="flex items-start gap-4">
          <img
            v-if="photo"
            :src="photo"
            :alt="breed.displayName"
            class="bg-base-300 hidden aspect-[4/3] w-28 shrink-0 rounded-xl object-cover sm:block"
          />
          <div class="min-w-0">
            <p class="text-xs font-bold tracking-wide uppercase opacity-60">
              What {{ articleFor(breed.displayName) }} {{ breed.displayName }} costs
            </p>
            <p class="font-display text-primary mt-0.5 text-4xl font-semibold">
              {{ breed.typicalPrice }}
            </p>
            <PriceProvenance :breed="breed" class="mt-1" />
          </div>
        </div>

        <!-- The meter is the point of the card: the band, the quote, the verdict, in one
             glance. The rating sits ON the price rather than in a panel below it. -->
        <PriceMeter
          :low="breed.priceLow"
          :high="breed.priceHigh"
          :quote="checkedQuote"
          :verdict="verdict"
        />

        <form class="flex flex-wrap items-center gap-2" @submit.prevent="check">
          <label class="input input-bordered input-sm flex items-center gap-1">
            <span class="opacity-60">$</span>
            <!-- step="any", not a step value: any step imposes HTML5 constraint validation, so
                 the browser silently refused to submit for any price that wasn't a multiple of
                 it. $1,299 did nothing at all. -->
            <input
              v-model="quote"
              type="number"
              min="0"
              step="any"
              inputmode="numeric"
              class="w-24 grow"
              placeholder="1200"
              aria-label="Price you were quoted, in dollars"
            />
          </label>
          <button type="submit" class="btn btn-primary btn-sm" :disabled="checking">
            <span v-if="checking" class="loading loading-spinner loading-xs" />
            Check a quote
          </button>
          <button
            v-if="sourcedCount > 1"
            type="button"
            class="link ml-auto text-xs font-semibold"
            @click="emit('open-prices')"
          >
            Compare {{ sourcedCount }} breeds →
          </button>
        </form>

        <p v-if="error" class="text-error text-sm">{{ error }}</p>

        <!-- Status colour never carries the meaning alone: the flag, the headline and the
             detail sentence all stay, which is also what makes the sub-3:1 warning tone
             legitimate on this surface. -->
        <div
          v-if="verdict"
          role="alert"
          data-testid="price-verdict"
          class="alert alert-soft items-start gap-2 text-sm"
          :class="alertClass"
        >
          <span class="max-w-prose">
            <strong class="block">{{ verdict.isWarning ? '🚩 ' : '' }}{{ verdict.headline }}</strong>
            <span class="mt-1 block">{{ verdict.detail }}</span>
            <!-- The verdict is about a price, so it lands on the page whose first line is
                 "a price far below the typical range for the breed". -->
            <a href="/safe#red-flags" class="link mt-1 block font-semibold">
              Full safety checklist →
            </a>
          </span>
        </div>

        <!-- Deferred: five bold-label bullets at full width were most of the card's bulk and
             none of its point. An accordion heading is a mini-IA — it lets someone choose to
             read this rather than scroll past it. -->
        <details class="collapse-arrow bg-base-200 collapse">
          <summary class="collapse-title text-sm font-semibold">
            What moves the price inside this range ({{ PRICE_DRIVERS.length }})
          </summary>
          <div class="collapse-content">
            <ul class="space-y-2 text-sm">
              <li v-for="d in PRICE_DRIVERS" :key="d.label" class="flex gap-2.5">
                <svg
                  class="text-primary/80 mt-0.5 h-4 w-4 shrink-0"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="1.8"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  aria-hidden="true"
                >
                  <path :d="d.icon" />
                </svg>
                <span class="max-w-prose"><strong>{{ d.label }}</strong> — {{ d.why }}</span>
              </li>
            </ul>
          </div>
        </details>

        <details class="collapse-arrow bg-base-200 collapse">
          <summary class="collapse-title text-sm font-semibold">
            The purchase price is the smaller half — what else to budget
          </summary>
          <div class="collapse-content">
            <ul class="list-inside list-disc space-y-1 text-sm">
              <li v-for="c in ONGOING_COSTS" :key="c" class="max-w-prose">{{ c }}</li>
            </ul>
            <p class="mt-2 max-w-prose text-xs opacity-60">
              We don't publish dollar estimates for these — they vary too much by city and by
              dog for a number to mean anything. Get quotes from a local vet before you commit.
            </p>
          </div>
        </details>
      </template>

      <!-- No sourced range for this breed. Say which of the three reasons applies, then
           give advice that doesn't depend on a price. -->
      <template v-else>
        <div>
          <p class="font-display text-2xl font-semibold">{{ headline }}</p>
          <p class="mt-1 max-w-prose text-sm opacity-70">{{ explanation }}</p>
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
              <span class="max-w-prose">{{ check }}</span>
            </li>
          </ul>
        </div>

        <div class="flex flex-wrap gap-2">
          <a href="/safe" class="btn btn-primary btn-sm">
            🛡️ Full scam-safety checklist
          </a>
          <button v-if="!breed" type="button" class="btn btn-outline btn-sm" @click="emit('open-quiz')">
            🐾 Not sure which breed? Take the quiz
          </button>
        </div>
      </template>
    </div>
  </section>
</template>
