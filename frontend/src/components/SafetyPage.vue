<script setup>
import { onMounted, ref } from 'vue'
import { ARTICLES, articlePath } from '../content/articles.js'
import { loadFavorites, loadRecent, toggleFavorite } from '../favorites.js'
import FeeCheck from './FeeCheck.vue'
import Icon from './Icon.vue'
import PuppyLogo from './PuppyLogo.vue'
import SavedDogs from './SavedDogs.vue'
import SellerCheck from './SellerCheck.vue'
import ThemePicker from './ThemePicker.vue'
import {
  DISCLAIMER,
  GUIDE_META,
  PAYMENTS,
  PAYMENT_STYLE,
  SAFETY_SECTIONS,
  STANDING_RULE,
  findSection,
} from '../content/safety.js'

const props = defineProps({
  // A section to jump to on arrival, from an old /safe/<slug> URL. The hash does the same job
  // for /safe#<slug>; both end up scrolling the same element.
  anchor: { type: String, default: '' },
})

onMounted(() => {
  document.title = GUIDE_META.title
  setMeta('name', 'description', GUIDE_META.description)
  setMeta('property', 'og:title', GUIDE_META.title)
  setMeta('property', 'og:description', GUIDE_META.description)

  // A crawler gets this from the prerendered <head>; the SPA writes the same strings from the
  // same constants, so the two cannot describe the page differently.
  const slug = props.anchor || window.location.hash.slice(1)
  if (!findSection(slug)) return

  // One canonical URL. Arriving via the old /safe/<slug> rewrites the address bar to the
  // anchor rather than leaving two URLs showing identical content.
  if (props.anchor) history.replaceState(null, '', `/safe#${props.anchor}`)

  // Not the browser's native jump: the page is client-rendered, so at hash-handling time the
  // element did not exist yet. `scroll-mt-20` on the article keeps it clear of the sticky nav.
  document.getElementById(slug)?.scrollIntoView()
})

// The guide used to be a one-way door: its nav had no theme toggle and no way back to a
// saved dog, so a reader who arrived from the app lost both until they left. Favorites live
// in localStorage, so this page can serve them without the app's state; opening one
// navigates back into the app, which owns the detail view.
// Loaded on mount, not at setup: this page prerenders at build time, where localStorage
// doesn't exist — and a crawler shouldn't see a "Your dogs" button anyway.
const favorites = ref([])
const recent = ref([])
const savedOpen = ref(false)
onMounted(() => {
  favorites.value = loadFavorites()
  recent.value = loadRecent()
})

function openDog(id) {
  window.location.href = `/dog/${encodeURIComponent(id)}`
}

function unsave(listing) {
  favorites.value = toggleFavorite(listing)
}

function setMeta(attr, key, content) {
  let tag = document.head.querySelector(`meta[${attr}="${key}"]`)
  if (!tag) {
    tag = document.createElement('meta')
    tag.setAttribute(attr, key)
    document.head.appendChild(tag)
  }
  tag.setAttribute('content', content)
}
</script>

<template>
  <a
    href="#main"
    class="btn btn-primary btn-sm sr-only focus:not-sr-only focus:fixed focus:top-2 focus:left-2 focus:z-50"
  >
    Skip to content
  </a>

  <nav class="bg-base-200/80 sticky top-0 z-40 backdrop-blur-md">
    <div class="mx-auto flex max-w-3xl items-center justify-between gap-3 px-4 py-2 sm:px-6">
      <a href="/" class="flex items-center gap-2 no-underline">
        <PuppyLogo class="h-9 w-9 shrink-0" />
        <span class="font-display text-xl font-semibold tracking-tight">PuppyFinder</span>
      </a>
      <div class="flex items-center gap-1">
        <button
          v-if="favorites.length || recent.length"
          type="button"
          class="btn btn-ghost btn-sm"
          :aria-label="`Your dogs — ${favorites.length} saved`"
          @click="savedOpen = true"
        >
          <Icon name="heart" class="text-primary/80 h-4 w-4" />
          <span v-if="favorites.length" class="badge badge-primary badge-sm">{{ favorites.length }}</span>
          <span class="hidden sm:inline">Your dogs</span>
        </button>
        <a href="/" class="btn btn-ghost btn-sm">
          <Icon name="search" class="text-primary/80 h-4 w-4" />
          <span class="hidden sm:inline">Find a puppy</span>
        </a>
        <ThemePicker />
      </div>
    </div>
  </nav>

  <main id="main" class="mx-auto max-w-3xl px-4 pt-8 pb-16 sm:px-6">
    <header class="mb-6">
      <h1 class="font-display text-3xl leading-[1.1] font-semibold tracking-tight sm:text-4xl">
        {{ GUIDE_META.heading }}
      </h1>
      <p class="text-base-content/70 mt-3 max-w-prose">{{ GUIDE_META.description }}</p>
    </header>

    <div role="alert" class="alert alert-warning alert-soft mb-6 py-2 text-sm">
      <span class="max-w-prose">
        <strong>{{ STANDING_RULE.label }}</strong> {{ STANDING_RULE.body }}
      </span>
    </div>

    <!-- A jump list, not navigation: everything it points at is on this page. -->
    <nav class="bg-base-200 rounded-box mb-10 p-4">
      <h2 class="mb-2 text-xs font-bold tracking-wide uppercase opacity-60">In this guide</h2>
      <ul class="grid list-none gap-1 p-0 sm:grid-cols-2">
        <li v-for="s in SAFETY_SECTIONS" :key="s.slug">
          <a :href="`#${s.slug}`" class="link link-hover text-sm">
            <span aria-hidden="true">{{ s.emoji }}</span> {{ s.title }}
          </a>
        </li>
      </ul>
    </nav>

    <article
      v-for="s in SAFETY_SECTIONS"
      :id="s.slug"
      :key="s.slug"
      class="mb-10 scroll-mt-20"
    >
      <h2 class="font-display mb-1 text-2xl font-semibold">
        <span aria-hidden="true">{{ s.emoji }}</span> {{ s.title }}
      </h2>
      <!--
        One line saying what this section decides. On a page this long it is what a skimmer
        reads instead of the bullets, so it states the decision rather than the topic. The
        payments section states its mechanism instead, which is the more useful sentence there.
      -->
      <p class="text-base-content/60 mb-3 max-w-prose text-sm">{{ s.intro ?? s.summary }}</p>

      <div
        v-if="s.lead"
        role="alert"
        class="alert alert-warning alert-soft mb-3 py-2 text-sm"
      >
        <span class="max-w-prose">{{ s.lead }}</span>
      </div>

      <template v-if="s.kind === 'payments'">
        <ul data-testid="payment-recourse" class="list-none space-y-3 p-0 text-sm">
          <li v-for="pay in PAYMENTS" :key="pay.method">
            <div class="flex flex-wrap items-baseline gap-2">
              <strong>{{ pay.method }}</strong>
              <!-- Word and colour together: the badge never carries the meaning alone. -->
              <span class="badge badge-sm" :class="PAYMENT_STYLE[pay.state]">{{ pay.verdict }}</span>
            </div>
            <p class="max-w-prose opacity-80">{{ pay.detail }}</p>
          </li>
        </ul>
        <p class="mt-3 max-w-prose text-xs opacity-60">{{ s.note }}</p>
      </template>

      <ul v-else class="list-inside list-disc space-y-2 text-sm">
        <li v-for="item in s.items" :key="item" class="max-w-prose">{{ item }}</li>
      </ul>

      <!--
        The one interactive thing on this page, and it belongs here rather than only in the
        buying path: this is the section someone lands on from a search, mid-scam, holding a
        message that says "refundable crate deposit". Reading that their fee is invented is
        good; typing it in and being told is better.
      -->
      <FeeCheck v-if="s.slug === 'escalating-fees'" class="mt-5" />
      <!-- Same reasoning as the fee check above: the section explains how to vet a breeder, and
           this is the one part of it that ends in a public database rather than in advice. -->
      <SellerCheck v-if="s.slug === 'vet-a-breeder'" class="mt-5" />
    </article>

    <!-- The guide's exits: each article pairs one of these sections with the tool that
         answers it, so the reader who wants to *check* something has somewhere to go. -->
    <section class="border-base-300 mt-10 border-t pt-6">
      <h2 class="mb-2 text-xs font-bold tracking-wide uppercase opacity-60">
        Check something specific
      </h2>
      <ul class="list-none space-y-1 p-0 text-sm">
        <li v-for="a in ARTICLES" :key="a.slug">
          <a :href="articlePath(a.slug)" class="link">{{ a.h1 }}</a>
        </li>
        <li>
          <a href="/embed" class="link opacity-80">
            Run a rescue or shelter? Embed the scam check on your own site
          </a>
        </li>
      </ul>
    </section>

    <p class="mx-auto mt-8 max-w-prose text-center text-xs opacity-60">{{ DISCLAIMER }}</p>
  </main>

  <SavedDogs
    v-if="savedOpen"
    :favorites="favorites"
    :recent="recent"
    @close="savedOpen = false"
    @open-dog="openDog"
    @unsave="unsave"
  />
</template>
