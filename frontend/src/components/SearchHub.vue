<script setup>
import { computed } from 'vue'
import { TRAITS, breedMatches } from '../breedFilters.js'
import BreedPicker from './BreedPicker.vue'
import { AGES, DEFAULT_GOAL } from '../searchUrl.js'

const props = defineProps({
  breeds: { type: Array, required: true },
  usStates: { type: Array, required: true },
  breed: { type: String, default: '' },
  state: { type: String, default: '' },
  city: { type: String, default: '' },
  size: { type: String, default: '' },
  age: { type: String, default: '' },
  sex: { type: String, default: '' }, // Male | Female — prefix-matched server-side
  traits: { type: Array, default: () => [] },
  // Filters the dogs, from each rescue's own listing. `traits` above prunes the breed list.
  goodWith: { type: Array, default: () => [] },
  goal: { type: String, default: DEFAULT_GOAL },
  coverage: { type: Array, default: () => [] }, // [{ state, count, cities }] — live dogs right now
  locating: { type: Boolean, default: false }, // geolocation lookup in flight
  zip: { type: String, default: '' },
  radius: { type: String, default: '' },
  // Resolved: the ZIP was turned into coordinates, so distance actually applies. A ZIP that
  // resolved to nothing must not look like a working filter.
  zipResolved: { type: Boolean, default: false },
  zipError: { type: String, default: '' },
})

// Petfinder's default is 50 miles, which is a reasonable "my area" for most of the US. "Any
// distance" stays available because rescues transport dogs, and someone willing to drive four
// hours for the right dog should not be told there are none.
const RADIUS_OPTIONS = [
  { value: '', label: 'Any distance' },
  { value: '25', label: 'Within 25 miles' },
  { value: '50', label: 'Within 50 miles' },
  { value: '100', label: 'Within 100 miles' },
  { value: '250', label: 'Within 250 miles' },
]

const emit = defineEmits([
  'update:breed', 'update:state', 'update:city', 'update:size', 'update:age', 'update:sex',
  'update:traits', 'update:goodWith', 'update:goal', 'update:zip', 'update:radius',
  'open-quiz', 'clear', 'near-me', 'close',
])

// Real per-dog data, not breed temperament: RescueGroups publishes isKidsOk / isDogsOk /
// isCatsOk and the rescue caring for the dog fills them in. Offered only when the page is
// showing dogs — in buy mode there are no listings to narrow, and the breed-list narrowers
// below are the only thing that can do any work.
const GOOD_WITH = [
  { key: 'kids', label: 'Kids' },
  { key: 'dogs', label: 'Other dogs' },
  { key: 'cats', label: 'Cats' },
]

// Compared against the app's real default goal — this used to test `!== 'both'` while the
// default is 'buy', so a pristine page permanently showed a "Clear filters" link that did
// nothing visible.
const anyFilterActive = computed(
  () =>
    props.breed || props.state || props.city.trim() || props.size || props.age || props.sex ||
    props.traits.length > 0 || props.goodWith.length > 0 || props.goal !== DEFAULT_GOAL ||
    props.zip.trim(),
)

// Goal leads the panel because it isn't a refiner — it decides whether the page
// shows live adoptable dogs or the vetted breeder marketplaces.
const GOALS = [
  { value: 'adopt', label: '🤝 Adopt a rescue dog' },
  { value: 'buy', label: '🛍️ Buy from a breeder' },
  { value: 'both', label: 'Show me both' },
]

const SIZES = ['Teacup', 'Small', 'Medium', 'Large']

// Age leads the physical filters: it's the single most-asked question of a
// puppy search, and the one filter this site is named after.
const AGE_HINTS = {
  Puppy: 'Under 1 year',
  Young: '1–2 years',
  Adult: '3–7 years',
  Senior: '8 years and up',
}

const liveCount = computed(() =>
  props.coverage.reduce((total, c) => total + c.count, 0),
)

// Size and the breed-narrowing traits prune the breed list; trait data exist for
// the curated breeds only.
const filteredBreeds = computed(() =>
  props.breeds.filter((b) => breedMatches(b, { size: props.size, traits: props.traits })),
)

const narrowed = computed(() => props.size || props.traits.length > 0)

function toggleGoodWith(key) {
  emit(
    'update:goodWith',
    props.goodWith.includes(key) ? props.goodWith.filter((w) => w !== key) : [...props.goodWith, key],
  )
}

function toggleTrait(key) {
  emit(
    'update:traits',
    props.traits.includes(key) ? props.traits.filter((t) => t !== key) : [...props.traits, key],
  )
}
</script>

<template>
  <section class="card bg-base-100 shadow-md">
    <div class="card-body gap-4">
      <div class="flex items-baseline justify-between gap-2">
        <h2 class="font-display card-title text-lg font-semibold">Your search</h2>
        <button
          v-if="anyFilterActive"
          type="button"
          class="link text-xs opacity-60 hover:opacity-100"
          @click="emit('clear')"
        >
          Clear filters
        </button>
      </div>

      <!-- role="group" + aria-pressed on every toggle cluster in this panel: selected state
           used to be colour alone (btn-primary vs btn-outline), invisible to assistive tech
           and a WCAG 1.4.1 miss. The heading spans get ids so each group announces its name. -->
      <div role="group" aria-labelledby="filter-goal-label">
        <span id="filter-goal-label" class="label-text mb-1 block text-xs font-bold tracking-wide uppercase opacity-60">I want to…</span>
        <div class="join join-vertical w-full">
          <button
            v-for="g in GOALS"
            :key="g.value"
            type="button"
            class="btn join-item justify-start"
            :class="goal === g.value ? 'btn-primary' : 'btn-outline'"
            :aria-pressed="goal === g.value"
            @click="emit('update:goal', g.value)"
          >
            {{ g.label }}
          </button>
        </div>
      </div>

      <div class="form-control">
        <span class="label-text mb-1 block text-xs font-bold tracking-wide uppercase opacity-60">Breed</span>
        <!-- A typeahead rather than a 179-option <select>: a native one only jumps to names
             beginning with what you type, so "retriever" found nothing at all. -->
        <BreedPicker
          :breeds="filteredBreeds"
          :model-value="breed"
          @update:model-value="emit('update:breed', $event)"
        />
        <p v-if="narrowed" class="mt-1 text-xs opacity-60">
          Showing {{ filteredBreeds.length }} breeds matching your filters (from our curated list).
        </p>
      </div>

      <div v-if="goal !== 'buy'" role="group" aria-labelledby="filter-age-label">
        <span id="filter-age-label" class="label-text mb-1 block text-xs font-bold tracking-wide uppercase opacity-60">Age</span>
        <div class="flex flex-wrap gap-1.5">
          <button
            type="button"
            class="btn btn-sm"
            :class="age === '' ? 'btn-primary' : 'btn-outline'"
            :aria-pressed="age === ''"
            @click="emit('update:age', '')"
          >
            Any age
          </button>
          <button
            v-for="a in AGES"
            :key="a"
            type="button"
            class="btn btn-sm"
            :class="age === a ? 'btn-primary' : 'btn-outline'"
            :aria-pressed="age === a"
            :title="AGE_HINTS[a]"
            @click="emit('update:age', a)"
          >
            {{ a }}
          </button>
        </div>
        <p v-if="age" class="mt-1 text-xs opacity-60">{{ AGE_HINTS[age] }}</p>
      </div>

      <!--
        Distance leads the location group. "Near me" lives here rather than on State because
        geolocation's actual product is coordinates — attaching it to a state dropdown threw away
        the precision it had just obtained.
      -->
      <div class="form-control">
        <span class="label-text mb-1 flex items-baseline justify-between text-xs font-bold tracking-wide uppercase opacity-60">
          Near you
          <button type="button" class="link font-normal normal-case" @click.prevent="emit('near-me')">
            {{ locating ? 'Locating…' : 'Use my location' }}
          </button>
        </span>
        <div class="flex gap-2">
          <input
            :value="zip"
            type="text"
            inputmode="numeric"
            maxlength="5"
            placeholder="ZIP code"
            aria-label="ZIP code to measure distance from"
            class="input input-bordered w-28"
            @input="emit('update:zip', $event.target.value)"
          />
          <select
            class="select select-bordered flex-1"
            :value="radius"
            aria-label="How far you will travel"
            @change="emit('update:radius', $event.target.value)"
          >
            <option v-for="r in RADIUS_OPTIONS" :key="r.value" :value="r.value">{{ r.label }}</option>
          </select>
        </div>
        <!-- A ZIP that resolved to nothing must not look like a working filter. -->
        <p v-if="zipError" class="mt-1 text-xs text-warning">{{ zipError }}</p>
        <p v-else-if="zipResolved && !radius" class="mt-1 max-w-prose text-xs opacity-60">
          Showing every dog, nearest first. Pick a distance to narrow it.
        </p>
        <p v-else-if="!zip.trim()" class="mt-1 max-w-prose text-xs opacity-60">
          A ZIP lets us sort by how far each dog is from you.
        </p>
      </div>

      <label class="form-control">
        <span class="label-text mb-1 text-xs font-bold tracking-wide uppercase opacity-60">
          State
        </span>
        <select
          class="select select-bordered w-full"
          :value="state"
          @change="emit('update:state', $event.target.value)"
        >
          <option value="">Anywhere in the US</option>
          <option v-for="s in usStates" :key="s" :value="s">
            {{ s }}{{ goal !== 'buy' && coverage.find((c) => c.state === s) ? ` · ${coverage.find((c) => c.state === s).count} live dogs` : '' }}
          </option>
        </select>
        <p v-if="goal === 'buy'" class="mt-1 text-xs opacity-60">
          Used to open each marketplace already filtered to your state.
        </p>
        <p v-else-if="liveCount" class="mt-1 text-xs opacity-60">
          {{ liveCount }} dogs in our live feeds right now. Other states fall back to the site directory.
        </p>
      </label>

      <label class="form-control">
        <span class="label-text mb-1 text-xs font-bold tracking-wide uppercase opacity-60">
          City <span class="normal-case opacity-70">(optional)</span>
        </span>
        <input
          type="text"
          class="input input-bordered w-full"
          :value="city"
          :disabled="!state"
          :placeholder="state ? 'e.g. Houston' : 'Pick a state first'"
          @input="emit('update:city', $event.target.value)"
        />
      </label>

      <!-- Wrapping chips like Age, not a five-across join: five joined segments in a 320px
           rail crushed "Teacup" and "Medium" into clipped text on small phones. -->
      <div role="group" aria-labelledby="filter-size-label">
        <span id="filter-size-label" class="label-text mb-1 block text-xs font-bold tracking-wide uppercase opacity-60">Size</span>
        <div class="flex flex-wrap gap-1.5">
          <button
            type="button"
            class="btn btn-sm"
            :class="size === '' ? 'btn-primary' : 'btn-outline'"
            :aria-pressed="size === ''"
            @click="emit('update:size', '')"
          >
            Any size
          </button>
          <button
            v-for="s in SIZES"
            :key="s"
            type="button"
            class="btn btn-sm"
            :class="size === s ? 'btn-primary' : 'btn-outline'"
            :aria-pressed="size === s"
            @click="emit('update:size', s)"
          >
            {{ s }}
          </button>
        </div>
      </div>

      <!-- Filters the dogs, so hidden in buy mode like good-with. Prefix-matched server-side,
           which is what keeps "Male (neutered)" — most shelter dogs — inside "Male". -->
      <div v-if="goal !== 'buy'" role="group" aria-labelledby="filter-sex-label">
        <span id="filter-sex-label" class="label-text mb-1 block text-xs font-bold tracking-wide uppercase opacity-60">Sex</span>
        <div class="flex flex-wrap gap-1.5">
          <button
            type="button"
            class="btn btn-sm"
            :class="sex === '' ? 'btn-primary' : 'btn-outline'"
            :aria-pressed="sex === ''"
            @click="emit('update:sex', '')"
          >
            Any
          </button>
          <button
            v-for="s in ['Male', 'Female']"
            :key="s"
            type="button"
            class="btn btn-sm"
            :class="sex === s ? 'btn-primary' : 'btn-outline'"
            :aria-pressed="sex === s"
            @click="emit('update:sex', s)"
          >
            {{ s }}
          </button>
        </div>
        <!-- Same rule as size and age, said where the control is: blanks stay in, labelled. -->
        <p v-if="sex" class="mt-1 text-xs opacity-60">
          Dogs whose listing records no sex are kept and labelled.
        </p>
      </div>

      <!--
        Two controls that sound alike and do different jobs, so each says which. This one
        filters the dogs: the rescue caring for the animal recorded how it does with kids,
        other dogs and cats, and that is a fact about this dog rather than about its breed.
        Hidden in buy mode, where there are no listings for it to narrow.
      -->
      <div v-if="goal !== 'buy'" role="group" aria-labelledby="filter-goodwith-label">
        <span id="filter-goodwith-label" class="label-text block text-xs font-bold tracking-wide uppercase opacity-60">
          Good with
        </span>
        <p class="mb-1 text-xs opacity-60">From each rescue's own listing.</p>
        <div class="flex flex-wrap gap-1.5">
          <button
            v-for="g in GOOD_WITH"
            :key="g.key"
            type="button"
            class="btn btn-xs"
            :class="goodWith.includes(g.key) ? 'btn-primary' : 'btn-outline'"
            :aria-pressed="goodWith.includes(g.key)"
            @click="toggleGoodWith(g.key)"
          >
            {{ g.label }}
          </button>
        </div>
        <!-- Said out loud, because it changes how to read the result: most rescues leave these
             blank, and those dogs are kept rather than dropped. -->
        <p v-if="goodWith.length" class="mt-1 text-xs opacity-60">
          Dogs a rescue marked as <em>not</em> suitable are always excluded. Dogs with nothing
          recorded are kept and labelled — ask when you call.
        </p>
      </div>

      <div role="group" aria-labelledby="filter-traits-label">
        <!-- Named for what it actually does. These score against our breed table, so they prune
             the breed list above rather than the results. The per-dog version is the "Good with"
             group above; keeping the labels distinct is the whole point, since a filter that
             quietly does something other than it says is worse than no filter. -->
        <span id="filter-traits-label" class="label-text mb-1 block text-xs font-bold tracking-wide uppercase opacity-60">
          Narrow the breed list
        </span>
        <div class="flex flex-wrap gap-1.5">
          <button
            v-for="t in TRAITS"
            :key="t.key"
            type="button"
            class="btn btn-xs"
            :class="traits.includes(t.key) ? 'btn-primary' : 'btn-outline'"
            :aria-pressed="traits.includes(t.key)"
            @click="toggleTrait(t.key)"
          >
            {{ t.label }}
          </button>
        </div>
      </div>

      <button type="button" class="btn btn-outline mt-1 w-full" @click="emit('open-quiz')">
        🐾 Not sure? Take the breed quiz
      </button>

      <!-- Mobile only: the panel sits above the results in DOM order, so after setting filters
           in a ~10-section drawer the dogs are a long scroll away. One button ends the errand. -->
      <button type="button" class="btn btn-primary w-full lg:hidden" @click="emit('close')">
        Done — show the results
      </button>
    </div>
  </section>
</template>
