<script setup>
import { ref } from 'vue'
import Icon from './Icon.vue'

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
  <!-- The icon is decorative; the aria-label carries the control's whole name. -->
  <button
    type="button"
    class="btn btn-ghost btn-sm btn-circle"
    :title="isDark ? 'Switch to light mode' : 'Switch to dark mode'"
    :aria-label="isDark ? 'Switch to light mode' : 'Switch to dark mode'"
    @click="toggle"
  >
    <Icon :name="isDark ? 'moon' : 'sun'" class="h-4.5 w-4.5" />
  </button>
</template>
