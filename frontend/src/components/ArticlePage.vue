<script setup>
import { computed, onMounted, ref } from 'vue'
import { ARTICLES, articlePath, findArticle } from '../content/articles.js'
import { DISCLAIMER, SAFETY_SECTIONS, safetyPath } from '../content/safety.js'
import { loadFavorites, loadRecent, toggleFavorite } from '../favorites.js'
import { fetchBreedImage } from '../dogImages.js'
import BreedCost from './BreedCost.vue'
import BreedPicker from './BreedPicker.vue'
import FeeCheck from './FeeCheck.vue'
import Icon from './Icon.vue'
import PuppyLogo from './PuppyLogo.vue'
import SavedDogs from './SavedDogs.vue'
import SellerCheck from './SellerCheck.vue'
import ThemePicker from './ThemePicker.vue'

const props = defineProps({
  slug: { type: String, required: true },
})

const article = computed(() => findArticle(props.slug))

// Same nav as the safety guide: these pages are entrances — most readers arrive from a
// search, not from the app — so the way in and the way onward both have to be here.
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

// The quiz and the sourced-price table live in the app, not here — navigate rather than
// half-reimplementing them on an article page.
function goToBuyPath() {
  window.location.href = '/?goal=buy'
}

// ---- the embedded price checker, for articles that carry one ----
//
// The article is an entrance with no app behind it, so the tool brings its own data:
// breeds fetched on mount (never at setup — these pages prerender), the same BreedPicker
// and BreedCost the buying path uses, so the check here can never disagree with the check
// there. Quiz/prices actions navigate into the app, which owns those views.
const needsPriceCheck = computed(() =>
  article.value?.blocks.some((b) => b.kind === 'tool' && b.tool === 'price-check'))
const breeds = ref([])
const selectedSlug = ref('')
const breedPhoto = ref(null)
const selectedBreed = computed(
  () => breeds.value.find((b) => b.slug === selectedSlug.value) ?? null)

async function pickBreed(slug) {
  selectedSlug.value = slug
  breedPhoto.value = null
  const imagePath = breeds.value.find((b) => b.slug === slug)?.imagePath
  const url = await fetchBreedImage(imagePath)
  if (selectedSlug.value === slug) breedPhoto.value = url
}

onMounted(async () => {
  if (!needsPriceCheck.value) return
  try {
    const res = await fetch('/api/breeds')
    if (res.ok) breeds.value = await res.json()
  } catch {
    // The article still reads without the tool's dropdown; BreedCost explains itself.
  }
})

const related = computed(() =>
  (article.value?.related ?? []).map(findArticle).filter(Boolean))
const safeLinks = computed(() =>
  (article.value?.safeAnchors ?? [])
    .map((slug) => SAFETY_SECTIONS.find((s) => s.slug === slug))
    .filter(Boolean))
const otherArticles = computed(() =>
  ARTICLES.filter((a) => a.slug !== props.slug))

onMounted(() => {
  if (!article.value) return
  document.title = article.value.meta.title
  setMeta('name', 'description', article.value.meta.description)
  setMeta('property', 'og:title', article.value.meta.title)
  setMeta('property', 'og:description', article.value.meta.description)
})

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

  <main v-if="article" id="main" class="mx-auto max-w-3xl px-4 pt-8 pb-16 sm:px-6">
    <header class="mb-6">
      <h1 class="font-display text-3xl leading-[1.1] font-semibold tracking-tight sm:text-4xl">
        {{ article.h1 }}
      </h1>
      <p class="text-base-content/70 mt-3 max-w-prose">{{ article.lede }}</p>
    </header>

    <template v-for="(block, i) in article.blocks" :key="i">
      <h2 v-if="block.kind === 'h2'" class="font-display mt-8 mb-2 text-2xl font-semibold">
        {{ block.text }}
      </h2>

      <p v-else-if="block.kind === 'p'" class="mb-3 max-w-prose text-sm leading-relaxed">
        {{ block.text }}
      </p>

      <ul v-else-if="block.kind === 'list'" class="mb-3 list-inside list-disc space-y-2 text-sm">
        <li v-for="item in block.items" :key="item" class="max-w-prose">{{ item }}</li>
      </ul>

      <div
        v-else-if="block.kind === 'callout'"
        role="alert"
        class="alert alert-soft mb-4 py-2 text-sm"
        :class="block.tone === 'warning' ? 'alert-warning' : 'alert-info'"
      >
        <span class="max-w-prose">{{ block.text }}</span>
      </div>

      <section v-else-if="block.kind === 'tool'" class="my-6">
        <p class="mb-2 max-w-prose text-sm font-semibold">{{ block.lead }}</p>

        <template v-if="block.tool === 'price-check'">
          <div class="mb-3 max-w-sm">
            <span class="label-text mb-1 block text-xs font-bold tracking-wide uppercase opacity-60">Breed</span>
            <BreedPicker :breeds="breeds" :model-value="selectedSlug" @update:model-value="pickBreed" />
          </div>
          <BreedCost
            :breed="selectedBreed"
            :breeds="breeds"
            :photo="breedPhoto"
            @pick-breed="pickBreed"
            @open-quiz="goToBuyPath"
            @open-prices="goToBuyPath"
          />
        </template>
        <FeeCheck v-else-if="block.tool === 'fee-check'" />
        <SellerCheck v-else-if="block.tool === 'seller-check'" />
      </section>
    </template>

    <!-- Onward links: an entrance page that dead-ends wastes the arrival. -->
    <section class="border-base-300 mt-10 border-t pt-6">
      <h2 class="mb-2 text-xs font-bold tracking-wide uppercase opacity-60">Keep reading</h2>
      <ul class="list-none space-y-1 p-0 text-sm">
        <li v-for="r in related" :key="r.slug">
          <a :href="articlePath(r.slug)" class="link">{{ r.h1 }}</a>
        </li>
        <li v-for="s in safeLinks" :key="s.slug">
          <a :href="safetyPath(s.slug)" class="link">
            <span aria-hidden="true">{{ s.emoji }}</span> {{ s.title }} — the full guide section
          </a>
        </li>
        <li v-for="a in otherArticles.filter((o) => !article.related.includes(o.slug))" :key="a.slug">
          <a :href="articlePath(a.slug)" class="link opacity-80">{{ a.h1 }}</a>
        </li>
      </ul>
    </section>

    <section class="mt-8">
      <h2 class="mb-2 text-xs font-bold tracking-wide uppercase opacity-60">Sources</h2>
      <ul class="list-none space-y-1 p-0 text-xs">
        <li v-for="s in article.sources" :key="s.url" class="max-w-prose">
          <a :href="s.url" target="_blank" rel="noopener noreferrer" class="link opacity-80">
            {{ s.name }}
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
