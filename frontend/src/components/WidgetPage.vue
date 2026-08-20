<script setup>
import { onMounted } from 'vue'
import FeeCheck from './FeeCheck.vue'

// The fee check as an embeddable fragment, for rescues and shelters to iframe into their own
// scam-warning pages. Deliberately bare: no nav, no hero, no links except the credit — inside
// someone else's page, our chrome is noise. noindex, because the content's canonical home is
// /puppy-shipping-fee-scam and a bare fragment ranking instead of it would help nobody.
onMounted(() => {
  document.title = 'Puppy fee scam check'
  const robots = document.createElement('meta')
  robots.setAttribute('name', 'robots')
  robots.setAttribute('content', 'noindex')
  document.head.appendChild(robots)
})
</script>

<template>
  <main class="mx-auto max-w-xl p-3">
    <FeeCheck />
    <p class="mt-2 text-center text-xs opacity-60">
      Free check by
      <a href="/" target="_blank" rel="noopener" class="link">PuppyFinder</a>
      — fees matched against published scam reports.
    </p>
  </main>
</template>
