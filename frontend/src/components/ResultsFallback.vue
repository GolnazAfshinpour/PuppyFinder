<script setup>
import { computed } from 'vue'
import SiteCard from './SiteCard.vue'

const props = defineProps({
  sites: { type: Array, default: () => [] },
  wanted: { type: Array, default: () => [] }, // which filters the user set
  goal: { type: String, default: 'both' },
  breedName: { type: String, default: '' },
  state: { type: String, default: '' },
  coverage: { type: Array, default: () => [] }, // [{ state, count, cities }]
  resultCount: { type: Number, default: 0 },
  // Buy mode leads the page with this section, so it drops the divider and the
  // gap it needs when it sits underneath a grid of dogs.
  flush: { type: Boolean, default: false },
})

// Which site we'd actually send someone to, in order: no caution label first,
// then the link that carries the most of their filters, then our own ranking.
// One recommendation beats fourteen tabs — the old "open all sites" button
// handed the work straight back to the user.
const PREFERENCE = {
  adopt: ['adoptapet', 'petfinder', 'rescueme', 'bestfriends', 'akcrescue', 'aspca', 'craigslist'],
  buy: ['gooddog', 'akc', 'puppyspot', 'pawrade', 'puppies', 'lancaster', 'greenfield'],
}

const buying = computed(() => props.goal === 'buy')
// "Show me both" reaches here as goal="both": one directory carrying both kinds of site,
// rather than a second copy of this section a scroll below the first.
const showAll = computed(() => props.goal === 'both')

const relevantSites = computed(() => {
  if (showAll.value) return props.sites
  return buying.value
    ? props.sites.filter((s) => s.kind !== 'Adopt')
    : props.sites.filter((s) => s.kind === 'Adopt')
})

const recommended = computed(() => {
  // Adoption leads the combined order: the rescue sites carry no caution labels, and the
  // reader who said "both" has not yet chosen to spend four figures.
  const order = showAll.value
    ? [...PREFERENCE.adopt, ...PREFERENCE.buy]
    : PREFERENCE[buying.value ? 'buy' : 'adopt']
  const rank = (site) => {
    const index = order.indexOf(site.id)
    return [site.caution ? 1 : 0, -site.appliedFilters.length, index < 0 ? order.length : index]
  }
  return [...relevantSites.value].sort((a, b) => {
    const [ac, af, ao] = rank(a)
    const [bc, bf, bo] = rank(b)
    return ac - bc || af - bf || ao - bo
  })[0]
})

const others = computed(() => relevantSites.value.filter((s) => s.id !== recommended.value?.id))

// Plain-language statement of where our own feeds do and don't reach. Saying it
// out loud beats an empty grid that reads as "there are no dogs".
const coverageLine = computed(() => {
  if (!props.coverage.length) return 'Our live shelter feeds are offline right now.'
  const where = props.coverage.map((c) => `${c.state} (${c.count})`).join(' and ')
  // In both mode the list also holds breeder marketplaces, which are rated, not coverage.
  const what = showAll.value ? 'The adoption sites below' : 'These national sites'
  if (props.state && !props.coverage.some((c) => c.state === props.state)) {
    return `No shelter feed covers ${props.state} yet — our live data is ${where}. ${what} do cover it.`
  }
  return `Our live feeds cover ${where}. ${what} cover the rest of the country.`
})

const filterSummary = computed(() =>
  [props.breedName && `${props.breedName}s`, props.state && `in ${props.state}`].filter(Boolean).join(' '),
)
</script>

<template>
  <section :class="flush ? '' : 'border-base-300 mt-12 border-t pt-8'">
    <template v-if="buying">
      <h2 class="font-display mb-1 text-2xl font-semibold tracking-tight">Puppies from breeders</h2>
      <p class="text-base-content/70 mb-5 max-w-prose text-sm">
        We don't list breeder puppies directly — no breeder marketplace offers a legitimate
        data feed. What we can do is tell you which ones actually vet their breeders and
        which ones have a complaint record, before any money changes hands.
      </p>
    </template>
    <template v-else>
      <h2 class="font-display mb-1 text-2xl font-semibold tracking-tight">
        {{ resultCount ? 'Want to search wider?' : 'Where to look instead' }}
      </h2>
      <p class="text-base-content/70 mb-5 max-w-prose text-sm">{{ coverageLine }}</p>
    </template>

    <div v-if="recommended" class="card bg-base-100 card-lift mb-4">
      <div class="card-body gap-3">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <h3 class="font-display card-title text-xl font-semibold">
            {{ recommended.name }}
            <span class="badge badge-primary badge-soft">Our pick</span>
          </h3>
        </div>
        <p class="max-w-prose text-sm opacity-80">{{ recommended.description }}</p>
        <p class="max-w-prose text-sm"><strong>Best for:</strong> {{ recommended.bestFor }}</p>
        <div v-if="wanted.length" class="flex flex-wrap items-center gap-1">
          <span class="text-xs opacity-60">This link carries:</span>
          <span
            v-for="f in wanted"
            :key="f"
            class="badge badge-sm"
            :class="recommended.appliedFilters.includes(f) ? 'badge-success badge-soft' : 'badge-ghost opacity-50'"
          >
            {{ recommended.appliedFilters.includes(f) ? '✓' : '✕' }} {{ f }}
          </span>
        </div>
        <a
          class="btn btn-primary btn-block"
          :href="recommended.linkUrl"
          target="_blank"
          rel="noopener noreferrer"
        >
          Search {{ filterSummary || 'dogs' }} on {{ recommended.name }} ↗
        </a>
      </div>
    </div>

    <details v-if="others.length" class="collapse-arrow border-base-300 bg-base-100 collapse border">
      <summary class="collapse-title text-sm font-semibold">
        Compare all {{ relevantSites.length }}
        {{ buying ? 'breeder marketplaces' : showAll ? 'adoption sites and breeder marketplaces' : 'adoption sites' }}
        — vetting, prices, and cautions
      </summary>
      <div class="collapse-content">
        <ul class="grid list-none gap-6 p-0 sm:grid-cols-2">
          <SiteCard
            v-for="site in others"
            :key="site.id"
            :site="site"
            :wanted="wanted"
          />
        </ul>
      </div>
    </details>

    <p class="text-base-content/60 mx-auto mt-6 max-w-prose text-center text-sm">
      PuppyFinder links you straight to each site's own listings — always verify a breeder
      or rescue yourself before sending money.
    </p>
  </section>
</template>
