<script setup>
import PuppyLogo from './PuppyLogo.vue'

defineEmits(['close'])

const SECTIONS = [
  {
    title: '🚩 Red flags that mean walk away',
    open: true,
    items: [
      'A price far below the typical range for the breed (our breed cards show typical ranges) — bargain purebreds are the classic scam bait.',
      'Payment by wire transfer, gift cards, Zelle, Venmo, or crypto to someone you have never met. No legitimate breeder asks for these.',
      'Any surprise fee after you pay — "shipping insurance", "climate-controlled crate", "vaccine deposit". This is the standard scam script; the puppy does not exist.',
      'Seller refuses a live video call with the puppy, or the photos look professional (reverse-image-search them — scammers reuse stolen photos).',
      'Pressure and urgency: "three other families are coming today", "price goes up tomorrow".',
      'Many breeds always available from one seller, or puppies always "ready to ship today" — responsible breeders have waitlists, not inventory.',
    ],
  },
  {
    title: '✅ How to vet a breeder',
    items: [
      'Visit in person. See where the puppies actually live, and meet the mother — her temperament and condition tell you more than any listing.',
      'Ask for the parents\' health-test results (OFA, PennHIP, Embark), not just "vet checked". Reputable breeders volunteer these.',
      'Expect the breeder to interview YOU. Good breeders care where their puppies go; no questions asked is a bad sign.',
      'Get a written contract with a health guarantee and a take-back clause — responsible breeders take their dogs back at any age, no questions.',
      'Verify registration claims: AKC-registered litters can be confirmed with the AKC. "Registration papers available for extra cost" is a red flag.',
      'Ask for references — their vet, and families from previous litters — and actually call them.',
    ],
  },
  {
    title: '📋 What real paperwork looks like',
    items: [
      'Vaccination and deworming records on a veterinary clinic\'s letterhead with dates, product names, and the vet\'s signature — not a handwritten list.',
      'Puppies must be at least 8 weeks old before going home (this is the law in most US states).',
      'A microchip number you can verify, or a written commitment about who registers it.',
      'For adoptions: spay/neuter status, known history, and behavioral notes from the shelter or foster.',
    ],
  },
  {
    title: '🤝 Adoption & rehoming fees',
    items: [
      'Legitimate shelter and rescue adoption fees run roughly $50–$500 and include vaccinations, microchip, and usually spay/neuter — that is not "buying a dog", it is covering care costs.',
      'On classifieds (especially Craigslist), a small rehoming fee ($50–$200) is normal and actually protects the animal from being taken for free by bad actors.',
      'A four-figure "rehoming fee" is a sale wearing a costume — on Craigslist it also violates the site\'s own rules. Treat it with full breeder-level scrutiny or walk away.',
    ],
  },
  {
    title: '🆘 If you were scammed',
    items: [
      'Report it to the FTC at reportfraud.ftc.gov and the FBI\'s IC3 at ic3.gov.',
      'Report the listing to the site it appeared on, and to petscams.com, which tracks fraudulent pet sellers.',
      'If you paid by card, dispute the charge with your bank immediately. Wire transfers and gift cards are usually unrecoverable — which is exactly why scammers insist on them.',
    ],
  },
]
</script>

<template>
  <div class="modal modal-open" @click.self="$emit('close')">
    <div class="modal-box max-w-2xl">
      <button
        type="button"
        class="btn btn-sm btn-circle btn-ghost absolute top-3 right-3"
        @click="$emit('close')"
      >
        ✕
      </button>

      <div class="mb-4 flex items-center gap-3">
        <PuppyLogo class="h-14 w-14 shrink-0 drop-shadow-sm" />
        <div>
          <h2 class="font-display text-3xl leading-none font-semibold tracking-wide">Buy &amp; adopt safely</h2>
          <p class="max-w-prose text-sm opacity-60">
            The rules that keep you from funding a scammer or a puppy mill.
          </p>
        </div>
      </div>

      <div role="alert" class="alert alert-warning alert-soft mb-3 py-2 text-sm">
        <span class="max-w-prose">
          <strong>The one rule that beats every scam:</strong> never send money for a puppy you
          (or someone you trust) haven't seen in person. Video calls are the minimum; in person is the standard.
        </span>
      </div>

      <div class="flex flex-col gap-2">
        <details
          v-for="s in SECTIONS"
          :key="s.title"
          class="collapse-arrow bg-base-200 collapse"
          :open="s.open"
        >
          <summary class="collapse-title font-semibold">{{ s.title }}</summary>
          <div class="collapse-content">
            <ul class="list-inside list-disc space-y-2 text-sm">
              <li v-for="item in s.items" :key="item" class="max-w-prose">{{ item }}</li>
            </ul>
          </div>
        </details>
      </div>

      <p class="mx-auto mt-4 max-w-prose text-center text-xs opacity-60">
        PuppyFinder links to third-party sites and can't vet individual sellers — these checks are yours to run.
      </p>
    </div>
  </div>
</template>
