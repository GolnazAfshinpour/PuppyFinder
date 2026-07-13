<script setup>
import { ref } from 'vue'

const THEMES = [
  'light', 'dark', 'cupcake', 'bumblebee', 'emerald', 'corporate', 'synthwave',
  'retro', 'cyberpunk', 'valentine', 'halloween', 'garden', 'forest', 'aqua',
  'lofi', 'pastel', 'fantasy', 'wireframe', 'black', 'luxury', 'dracula',
  'cmyk', 'autumn', 'business', 'acid', 'lemonade', 'night', 'coffee',
  'winter', 'dim', 'nord', 'sunset', 'caramellatte', 'abyss', 'silk',
]

const current = ref(document.documentElement.dataset.theme || 'autumn')

function apply(theme) {
  current.value = theme
  document.documentElement.dataset.theme = theme
  localStorage.setItem('puppyfinder-theme', theme)
  document.activeElement?.blur()
}
</script>

<template>
  <div class="dropdown dropdown-end">
    <div tabindex="0" role="button" class="btn btn-sm btn-ghost gap-1">
      🎨 {{ current }}
      <svg class="h-3 w-3 opacity-60" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="m6 9 6 6 6-6" />
      </svg>
    </div>
    <ul
      tabindex="0"
      class="dropdown-content menu bg-base-200 rounded-box z-50 mt-1 max-h-96 w-44 flex-nowrap overflow-y-auto p-2 shadow-xl"
    >
      <li v-for="t in THEMES" :key="t">
        <button type="button" :class="{ 'menu-active': t === current }" @click="apply(t)">
          <span
            class="grid grid-cols-2 gap-0.5 rounded-sm p-0.5"
            :data-theme="t"
            style="background: var(--color-base-100)"
          >
            <span class="h-1.5 w-1.5 rounded-full" style="background: var(--color-primary)" />
            <span class="h-1.5 w-1.5 rounded-full" style="background: var(--color-secondary)" />
            <span class="h-1.5 w-1.5 rounded-full" style="background: var(--color-accent)" />
            <span class="h-1.5 w-1.5 rounded-full" style="background: var(--color-neutral)" />
          </span>
          {{ t }}
        </button>
      </li>
    </ul>
  </div>
</template>
