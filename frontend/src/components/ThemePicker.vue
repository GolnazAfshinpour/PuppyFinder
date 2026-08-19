<script setup>
import { ref } from 'vue'

// One brand, two modes — the old 35-theme picker diluted the identity.
const DARK = 'goldenhour-dark'
const LIGHT = 'goldenhour'

// Guarded for the prerender: /safe renders at build time, where no document exists.
const isDark = ref(
  typeof document !== 'undefined' && document.documentElement.dataset.theme === DARK,
)

function toggle() {
  isDark.value = !isDark.value
  const theme = isDark.value ? DARK : LIGHT
  document.documentElement.dataset.theme = theme
  localStorage.setItem('puppyfinder-theme', theme)
}
</script>

<template>
  <!-- aria-label as well as title: with emoji content, the accessible name was "☀️",
       which a screen reader speaks as "sun" with no hint that it's a control. -->
  <button
    type="button"
    class="btn btn-ghost btn-sm btn-circle text-base"
    :title="isDark ? 'Switch to light mode' : 'Switch to dark mode'"
    :aria-label="isDark ? 'Switch to light mode' : 'Switch to dark mode'"
    @click="toggle"
  >
    <span aria-hidden="true">{{ isDark ? '🌙' : '☀️' }}</span>
  </button>
</template>
