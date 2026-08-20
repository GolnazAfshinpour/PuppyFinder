<script setup>
import Icon from './Icon.vue'
import PuppyLogo from './PuppyLogo.vue'
import ThemePicker from './ThemePicker.vue'

// The one nav, and the skip link that precedes it. This markup was pasted verbatim into
// five page components, which is how the icons and the theme toggle had already started
// to drift apart — now there is one copy to be right.
defineProps({
  // The search app: wide container, and the action link points out to the safety guide.
  // Every other page is an entrance, so it gets the narrow container and a link into the app.
  home: { type: Boolean, default: false },
  // "Your dogs" renders only when there is something behind it (saved or recently viewed).
  showSaved: { type: Boolean, default: false },
  savedCount: { type: Number, default: 0 },
})

defineEmits(['open-saved'])
</script>

<template>
  <!-- Visually hidden until focused: the first Tab stop skips the nav (and whatever hero
       follows) for keyboard and screen-reader users. Every page has a #main. -->
  <a
    href="#main"
    class="btn btn-primary btn-sm sr-only focus:not-sr-only focus:fixed focus:top-2 focus:left-2 focus:z-50"
  >
    Skip to content
  </a>

  <!-- Glass sticky nav: identity + global actions, nothing else. -->
  <nav class="bg-base-200/80 sticky top-0 z-40 backdrop-blur-md">
    <div
      class="mx-auto flex items-center justify-between gap-3 px-4 py-2 sm:px-6"
      :class="home ? 'max-w-6xl' : 'max-w-3xl'"
    >
      <a href="/" class="flex items-center gap-2 no-underline">
        <PuppyLogo class="h-9 w-9 shrink-0" />
        <span class="font-display text-xl font-semibold tracking-tight">PuppyFinder</span>
      </a>
      <div class="flex items-center gap-1">
        <!-- Saving is one click and everywhere; the count belongs where it is always visible. -->
        <button
          v-if="showSaved"
          type="button"
          class="btn btn-ghost btn-sm"
          :aria-label="`Your dogs — ${savedCount} saved`"
          @click="$emit('open-saved')"
        >
          <Icon name="heart" class="text-primary/80 h-4 w-4" />
          <span v-if="savedCount" class="badge badge-primary badge-sm">{{ savedCount }}</span>
          <span class="hidden sm:inline">Your dogs</span>
        </button>
        <a v-if="home" href="/safe" class="btn btn-ghost btn-sm">
          <Icon name="shield-check" class="text-primary/80 h-4 w-4" />
          <span class="hidden sm:inline">Buy safely</span>
        </a>
        <a v-else href="/" class="btn btn-ghost btn-sm">
          <Icon name="search" class="text-primary/80 h-4 w-4" />
          <span class="hidden sm:inline">Find a puppy</span>
        </a>
        <ThemePicker />
      </div>
    </div>
  </nav>
</template>
