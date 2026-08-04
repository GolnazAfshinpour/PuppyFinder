<script setup>
import { computed } from 'vue'
import { markerPosition, meterZones, verdictRole } from '../priceMeter.js'

const props = defineProps({
  low: { type: Number, required: true },
  high: { type: Number, required: true },
  // The checked quote, and the verdict for it. Both null before anything is checked — the
  // meter still draws the band, which is the state most visitors will ever see.
  // The quote comes in separately because /api/price-check does not echo the price back.
  quote: { type: Number, default: null },
  verdict: { type: Object, default: null },
})

const zones = computed(() => meterZones(props.low, props.high))
const marker = computed(() =>
  props.quote == null ? null : markerPosition(props.quote, props.low, props.high),
)

// Status colour is a *token*, never a hex, so the meter cannot drift from the theme. Only one
// of these is ever on screen at once, which is why status hues never sit adjacent here and
// colour-vision adjacency between them doesn't arise.
const ROLE = {
  critical: { fill: 'bg-error', text: 'text-error' },
  warning: { fill: 'bg-warning', text: 'text-warning' },
  serious: { fill: 'bg-secondary', text: 'text-secondary' },
  good: { fill: 'bg-success', text: 'text-success' },
  neutral: { fill: 'bg-base-content', text: 'text-base-content' },
}
const role = computed(() => ROLE[verdictRole(props.verdict?.level)] ?? ROLE.neutral)

const money = (n) => `$${Math.round(n).toLocaleString()}`

// One spoken sentence: read out piecewise the geometry means nothing, and the verdict text
// below already carries the conclusion. Colour is decoration for this component.
const spokenLabel = computed(() => {
  const band = `Typical range ${money(props.low)} to ${money(props.high)}.`
  if (!marker.value) return `${band} No quote checked yet.`
  return `${band} You were quoted ${money(props.quote)}. ${props.verdict?.headline ?? ''}`.trim()
})
</script>

<template>
  <div v-if="zones" class="pt-1" data-testid="price-meter">
    <!-- Zone labels above the track, so the marker and its value own the space below. -->
    <div class="text-base-content/55 relative mb-1 h-4 text-[11px] font-semibold tracking-wide">
      <span class="absolute left-0">scam</span>
      <span
        class="absolute -translate-x-1/2"
        :style="{ left: `${(zones.bandStart + zones.bandEnd) / 2}%` }"
      >typical</span>
      <span class="absolute right-0">premium</span>
    </div>

    <div class="relative h-3" role="img" :aria-label="spokenLabel">
      <!-- Track: recessive and neutral. The band is the emphasis, the rest is context. -->
      <div class="bg-base-300 absolute inset-0 rounded-full" />

      <!-- The sourced band, with rounded ends per the mark spec. -->
      <div
        class="bg-primary/70 absolute inset-y-0 rounded-full"
        :style="{ left: `${zones.bandStart}%`, width: `${zones.bandEnd - zones.bandStart}%` }"
      />

      <!-- The 0.5x far-below boundary as a 2px rule. This is the rule that decides whether a
           quote is "far below" rather than merely under market, and until now it existed only
           inside a sentence. -->
      <div
        class="bg-error/45 absolute inset-y-0 w-0.5"
        :style="{ left: `${zones.scamEnd}%` }"
        aria-hidden="true"
      />

      <!-- The quote. A 2px surface ring keeps it legible over the band it overlaps. -->
      <div
        v-if="marker"
        class="ring-base-100 absolute top-1/2 h-4 w-4 -translate-x-1/2 -translate-y-1/2 rounded-full ring-2"
        :class="role.fill"
        :style="{ left: `${marker.percent}%` }"
        aria-hidden="true"
      />
    </div>

    <!-- Selective direct labels: the band ends always, the quote when there is one. Never a
         number on every tick. -->
    <div class="text-base-content/60 relative mt-1.5 h-9 text-xs">
      <span class="absolute" :style="{ left: `${zones.bandStart}%` }">{{ money(low) }}</span>
      <span class="absolute -translate-x-full" :style="{ left: `${zones.bandEnd}%` }">
        {{ money(high) }}
      </span>
      <span
        v-if="marker"
        class="absolute -translate-x-1/2 font-semibold whitespace-nowrap"
        :class="role.text"
        :style="{ left: `${marker.percent}%`, top: '1.15rem' }"
      >
        <!-- An off-scale quote is disclosed rather than implied to sit exactly at the end. -->
        {{ marker.offScale ? 'over ' : '' }}{{ money(marker.offScale ? zones.domainMax : quote) }}
      </span>
    </div>
  </div>
</template>
