<script setup>
import { computed } from 'vue'
import Icon from './Icon.vue'

// Editorial hero: one headline doing the brand work, numeric trust under it.
//
// Mode-aware, because it wasn't: "Buy a puppy. Don't get scammed." sat above a grid of
// rescue dogs, and the subhead talked about which marketplaces vet their breeders while
// you were browsing shelters. The headline contradicted the page under it.
const props = defineProps({
  goal: { type: String, required: true },
  verifiedBreedCount: { type: Number, default: 0 },
  liveCount: { type: Number, default: 0 },
  coverageStates: { type: Number, default: 0 },
})

const emit = defineEmits(['update:goal', 'open-prices'])

const buying = computed(() => props.goal === 'buy')
const showBuy = computed(() => props.goal !== 'adopt')

function toggleGoal() {
  emit('update:goal', props.goal === 'adopt' ? 'buy' : 'adopt')
}
</script>

<template>
  <header class="mb-8 text-center">
    <h1 class="font-display display-wonk mx-auto max-w-3xl text-4xl leading-[1.1] font-semibold tracking-tight sm:text-6xl">
      <template v-if="buying">
        Buy a puppy.
        <span class="text-primary">Don't get scammed.</span>
      </template>
      <template v-else-if="goal === 'both'">
        Adopt or buy.
        <span class="text-primary">Don't get scammed either way.</span>
      </template>
      <template v-else>
        Adopt a dog.
        <span class="text-primary">They're already waiting.</span>
      </template>
    </h1>
    <p class="text-base-content/70 mx-auto mt-3 max-w-xl text-base">
      <template v-if="buying">
        Which marketplaces actually vet their breeders, which ones have a complaint
        record, and the checks that catch a scam before you send a cent.
      </template>
      <template v-else-if="goal === 'both'">
        Live shelter dogs next to honestly rated breeder marketplaces — and the checks
        that catch a scam before you send a cent.
      </template>
      <template v-else>
        Real dogs from public shelter feeds — photo, age, size and the shelter's own phone
        number. No listing fees, no middlemen, and most are already vaccinated and neutered.
      </template>
    </p>
    <!--
      The clickable chips carry an arrow and an underline; the static one carries neither.
      Before this they were all `badge badge-outline` and visually identical, so three
      buttons sat in a row of four chips with nothing to say they did anything — and
      cursor-pointer only helps after you have already guessed.
    -->
    <div class="mt-4 flex flex-wrap justify-center gap-2">
      <button
        v-if="showBuy && verifiedBreedCount"
        type="button"
        class="badge badge-lg badge-outline hover:badge-primary cursor-pointer underline decoration-dotted underline-offset-2"
        @click="emit('open-prices')"
      >
        {{ verifiedBreedCount }} sourced price ranges →
      </button>
      <span v-if="buying" class="badge badge-lg badge-outline opacity-70">
        7 breeder marketplaces, honestly rated
      </span>
      <!-- Adopting: the honest headline number is coverage, and it is already computed. -->
      <span v-else-if="coverageStates" class="badge badge-lg badge-outline opacity-70">
        {{ liveCount }} dogs live across {{ coverageStates }}
        {{ coverageStates === 1 ? 'state' : 'states' }}
      </span>
      <a
        href="/safe"
        class="badge badge-lg badge-outline hover:badge-primary cursor-pointer underline decoration-dotted underline-offset-2"
      >
        <Icon name="shield-check" class="h-3.5 w-3.5" /> Scam-safety checklist →
      </a>
      <!-- Underlined like the others because it is clickable; no arrow, because it toggles
           the view rather than opening something. Hidden in both mode, where each half of
           its label would describe something already on the page. -->
      <button
        v-if="liveCount && goal !== 'both'"
        type="button"
        class="badge badge-lg cursor-pointer underline decoration-dotted underline-offset-2"
        :class="goal === 'adopt' ? 'badge-primary' : 'badge-outline hover:badge-primary'"
        @click="toggleGoal"
      >
        {{ goal === 'adopt' ? '🛍️ Or buy from a breeder' : `🤝 Or adopt (${liveCount} live)` }}
      </button>
    </div>
  </header>
</template>
