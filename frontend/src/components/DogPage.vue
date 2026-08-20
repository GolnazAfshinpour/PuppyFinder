<script setup>
import { computed, onMounted, ref } from 'vue'
import { loadFavorites, loadRecent, recordViewed, toggleFavorite } from '../favorites.js'
import { setMeta } from '../meta.js'
import DogDetail from './DogDetail.vue'
import SavedDogs from './SavedDogs.vue'
import SiteFooter from './SiteFooter.vue'
import SiteHeader from './SiteHeader.vue'

// A dog you can link to. The in-app detail stays a dialog (closing it returns to the same
// search), but the same content deserves a real URL: a shared /dog/<id> opens as a page
// with its own title and preview card, not a modal floating over someone else's defaults.

const props = defineProps({
  id: { type: String, required: true },
})

// Same nav as the article pages: most readers of a shared link arrive from outside the app.
const favorites = ref([])
const recent = ref([])
const savedOpen = ref(false)
onMounted(() => {
  favorites.value = loadFavorites()
  recent.value = loadRecent()
})

const dog = ref(null)
const favorite = computed(() => favorites.value.some((f) => f.id === props.id))
function onToggleFavorite() {
  if (dog.value) favorites.value = toggleFavorite(dog.value)
}

function openDog(id) {
  window.location.href = `/dog/${encodeURIComponent(id)}`
}

// The "gone" state's escape hatch: this page has no grid, so it lands in the app's.
function searchSimilar() {
  window.location.href = '/?goal=adopt'
}

function unsave(listing) {
  favorites.value = toggleFavorite(listing)
}

// The share preview is the dog: name, breed and place in the title, the rescue's own photo
// as the image, their words (or an honest summary) as the description.
function onLoaded(loaded) {
  dog.value = loaded
  recent.value = recordViewed(loaded)
  const title = `Adopt ${loaded.name} — ${loaded.breed} in ${loaded.city}, ${loaded.state}`
  const description = loaded.description?.slice(0, 160)
    || `${loaded.name} is a ${[loaded.age, loaded.size, loaded.breed].filter(Boolean).join(' ')}`
      + ` waiting for a home in ${loaded.city}, ${loaded.state}.`
  document.title = `${title} | PuppyFinder`
  setMeta('name', 'description', description)
  setMeta('property', 'og:title', title)
  setMeta('property', 'og:description', description)
  const photo = loaded.photos?.[0] ?? loaded.imageUrl
  if (photo) setMeta('property', 'og:image', photo)
}
</script>

<template>
  <SiteHeader
    :show-saved="Boolean(favorites.length || recent.length)"
    :saved-count="favorites.length"
    @open-saved="savedOpen = true"
  />

  <main id="main" class="mx-auto max-w-3xl px-4 pt-6 pb-16 sm:px-6">
    <p class="mb-4">
      <a href="/?goal=adopt" class="link link-hover text-sm opacity-70">← All adoptable dogs</a>
    </p>

    <DogDetail
      page
      :listing-id="id"
      :favorite="favorite"
      @loaded="onLoaded"
      @toggle-favorite="onToggleFavorite"
      @search-similar="searchSimilar"
    />
  </main>

  <SiteFooter />

  <SavedDogs
    v-if="savedOpen"
    :favorites="favorites"
    :recent="recent"
    @close="savedOpen = false"
    @open-dog="openDog"
    @unsave="unsave"
  />
</template>
