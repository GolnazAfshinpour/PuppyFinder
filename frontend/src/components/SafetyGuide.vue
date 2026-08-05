<script setup>
import PuppyLogo from './PuppyLogo.vue'
import { useModal } from '../useModal.js'

const emit = defineEmits(['close'])

// Escape closed the dog detail and did nothing here, which teaches the key and then ignores
// it — worse than never supporting it.
useModal(() => emit('close'))

// What you can get back, by method. Three states, each always paired with a word — colour
// never carries the meaning alone.
//
// The mechanism, because it is the opposite of most people's intuition: credit-card rights
// (Reg Z) turn on *what you bought*, and cover goods "not delivered as agreed". Bank-transfer
// and app rights (Reg E) turn on *who initiated the payment* — if you sent it yourself, you
// authorised it, and the protection largely does not reach you however thoroughly you were
// deceived.
const PAYMENTS = [
  {
    method: 'Credit card',
    state: 'good',
    verdict: 'Usually recoverable',
    detail: 'A puppy that never arrives is "goods not delivered as agreed", which US law treats as a '
      + 'billing error. Dispute it in writing to the billing-inquiries address on your statement '
      + 'within 60 days of the first statement showing the charge. While it is disputed you need not '
      + 'pay that amount, and they cannot report it delinquent.',
  },
  {
    method: 'Debit card',
    state: 'warning',
    verdict: 'Much weaker than credit',
    detail: 'Same piece of plastic, different rules. Debit falls under the bank-transfer regime, which '
      + 'protects you when someone else moves your money — not when you were persuaded to move it '
      + 'yourself. Report it anyway and immediately; some banks go beyond the legal minimum.',
  },
  {
    method: 'Zelle, Cash App, Venmo',
    state: 'critical',
    verdict: 'Rarely recoverable',
    detail: 'This is the trap. People refuse a wire because it feels risky, then pay by app believing '
      + 'it is protected. If you knowingly sent the money, it counts as authorised and the protection '
      + 'for "unauthorised" transfers does not apply. It is different if someone took over your '
      + 'account or stole your login — that is unauthorised, and you should dispute it.',
  },
  {
    method: 'PayPal',
    state: 'warning',
    verdict: 'Only if you pay for Goods and Services',
    detail: 'PayPal\'s own buyer protection covers Goods and Services payments, not "Friends and '
      + 'Family" — which is exactly what a scammer will ask for, framed as saving you the fee. '
      + 'Paying by card through PayPal keeps your card rights as well.',
  },
  {
    method: 'Wire transfer',
    state: 'critical',
    verdict: 'Minutes, then gone',
    detail: 'A wire can sometimes be recalled if you call the bank before it settles. After that it '
      + 'has been collected in cash and there is nothing to claw back. Speed is the entire reason '
      + 'scammers ask for it.',
  },
  {
    method: 'Gift cards',
    state: 'critical',
    verdict: 'Almost never',
    detail: 'Still worth calling the card issuer straight away and reading them the numbers — very '
      + 'occasionally an unspent balance can be frozen. No legitimate breeder has ever been paid in '
      + 'gift cards.',
  },
  {
    method: 'Crypto',
    state: 'critical',
    verdict: 'Irreversible',
    detail: 'There is no dispute process and no one to appeal to. A transfer cannot be undone by '
      + 'anybody, including the exchange you sent it from.',
  },
]

// Measured, not chosen by eye: at 12px the soft error badge came out at 4.01:1 against the
// light surface, under the 4.5 WCAG 1.4.3 asks for normal-size text — and it was carrying the
// four rows that matter most. Solid error measures 4.80 light / 5.15 dark; solid success fails
// the other way at 2.91 light. So soft, soft, solid, which also gives the irreversible methods
// the most visual weight.
const PAYMENT_STYLE = {
  good: 'badge-soft badge-success',
  warning: 'badge-soft badge-warning',
  critical: 'badge-error',
}

const SECTIONS = [
  {
    title: '🚩 Red flags that mean walk away',
    open: true,
    items: [
      'A price far below the typical range for the breed (our breed cards show typical ranges) — bargain purebreds are the classic scam bait.',
      'Payment by wire transfer, gift cards, Zelle, Venmo, or crypto to someone you have never met. No legitimate breeder asks for these.',
      'Any surprise fee after you pay — "shipping insurance", "climate-controlled crate", "vaccine deposit". This is the standard scam script; the puppy does not exist.',
      'Seller refuses a live video call, or will only send pre-recorded clips. A refusal is still damning — but a call happening is no longer proof by itself (see below).',
      'Photos that look professional or stock-like. Reverse-image-search them: a hit is damning. A clean result no longer clears anyone, because an AI-generated photo appears nowhere else.',
      'Pressure and urgency: "three other families are coming today", "price goes up tomorrow".',
      'Many breeds always available from one seller, or puppies always "ready to ship today" — responsible breeders have waitlists, not inventory.',
    ],
  },
  {
    // Added August 2026. "Have a video call" was this guide's central recommendation and BBB
    // now warns that advice "may be going away" because generated video can satisfy it. The
    // answer isn't to drop the call — it's to make it interactive on the buyer's terms, which
    // a pre-rendered or replayed video cannot survive.
    title: '📹 Make the video call prove something',
    items: [
      'Name the test yourself, during the call: ask them to pick the puppy up, turn it over, and show its belly and paws. Generated and recycled footage cannot take instructions.',
      'Ask them to hold up something you choose on the spot — today\'s date on a handwritten note, a specific number of fingers, a spoon.',
      'Ask for one continuous pan from the puppy to the mother to the room, without cutting. Scam footage is short, tightly cropped, and never shows the surroundings.',
      'Watch for the tells of a replayed clip: no response to what you just asked, audio that does not match the mouth, a loop, or a "bad connection" the moment you make a specific request.',
      'Do it twice, days apart, and ask for something different each time. One good call can be staged or borrowed; two on your terms is much harder.',
      'Best of all, still visit in person. Everything above exists because that is not always possible — it is a substitute, not an equal.',
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
        <!-- Red flags first and open, because "walk away" outranks everything else here. -->
        <details
          v-for="s in SECTIONS.slice(0, 1)"
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
        <!--
          Second, after the red flags: the guide reads in the order the decision happens —
          spot the scam, understand what your payment method can and can't recover, then vet,
          then paperwork, then recover. The app already said which methods to avoid; it never
          said what you can get back, and BBB documents a victim who refused a wire as too risky
          and then paid by Zelle believing it was protected.
        -->
        <details class="collapse-arrow bg-base-200 collapse">
          <summary class="collapse-title font-semibold">💳 What you can actually get back</summary>
          <div class="collapse-content">
            <p class="mb-3 max-w-prose text-sm opacity-70">
              The rule is the opposite of most people's intuition. Credit-card protection depends on
              <strong>what you bought</strong>, so a puppy that never arrives is covered. Bank
              transfers and payment apps depend on <strong>who moved the money</strong> — and if you
              sent it yourself, you authorised it, however thoroughly you were deceived.
            </p>
            <ul data-testid="payment-recourse" class="space-y-3 text-sm">
              <li v-for="pay in PAYMENTS" :key="pay.method">
                <div class="flex flex-wrap items-baseline gap-2">
                  <strong>{{ pay.method }}</strong>
                  <!-- Word and colour together: the badge never carries the meaning alone. -->
                  <span class="badge badge-sm" :class="PAYMENT_STYLE[pay.state]">
                    {{ pay.verdict }}
                  </span>
                </div>
                <p class="max-w-prose opacity-80">{{ pay.detail }}</p>
              </li>
            </ul>
            <p class="mt-3 max-w-prose text-xs opacity-60">
              General information, not legal advice, and it describes US rules. Whatever you paid
              with, report it — the FTC and IC3 links are in the last section.
            </p>
          </div>
        </details>
        <details
          v-for="s in SECTIONS.slice(1)"
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
