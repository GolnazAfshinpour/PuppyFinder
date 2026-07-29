<script setup>
import { ref } from 'vue'

// One brand, two modes — the old 35-theme picker diluted the identity.
const DARK = 'goldenhour-dark'
const LIGHT = 'goldenhour'

const isDark = ref(document.documentElement.dataset.theme === DARK)

function toggle() {
  isDark.value = !isDark.value
  const theme = isDark.value ? DARK : LIGHT
  document.documentElement.dataset.theme = theme
  localStorage.setItem('puppyfinder-theme', theme)
}
</script>

<template>
  <button
    type="button"
    class="btn btn-ghost btn-sm btn-circle text-base"
    :title="isDark ? 'Switch to light mode' : 'Switch to dark mode'"
    @click="toggle"
  >
    {{ isDark ? '🌙' : '☀️' }}
  </button>
</template>
