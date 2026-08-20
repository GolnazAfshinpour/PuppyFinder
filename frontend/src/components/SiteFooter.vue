<script setup>
import { ARTICLES, articlePath } from '../content/articles.js'
import { SAFETY_SECTIONS, safetyPath } from '../content/safety.js'

// The safety guide's route in from every page. A page nothing links to is an orphan however
// good it is — these are real <a href>s, so a crawler can reach them and a reader who wants
// to send one to someone has a URL to copy.
defineProps({
  home: { type: Boolean, default: false },
})
</script>

<template>
  <footer class="border-base-300 border-t">
    <div class="mx-auto px-4 py-8 sm:px-6" :class="home ? 'max-w-6xl' : 'max-w-3xl'">
      <h2 class="mb-3 text-xs font-bold tracking-wide uppercase opacity-60">
        <a href="/safe" class="link link-hover">Buy &amp; adopt safely</a>
      </h2>
      <ul class="grid list-none gap-x-6 gap-y-1 p-0 text-sm sm:grid-cols-2 lg:grid-cols-4">
        <li v-for="s in SAFETY_SECTIONS" :key="s.slug">
          <a :href="safetyPath(s.slug)" class="link link-hover opacity-70">
            <span aria-hidden="true">{{ s.emoji }}</span> {{ s.title }}
          </a>
        </li>
      </ul>
      <h2 class="mt-6 mb-3 text-xs font-bold tracking-wide uppercase opacity-60">Scam guides</h2>
      <ul class="grid list-none gap-x-6 gap-y-1 p-0 text-sm sm:grid-cols-2 lg:grid-cols-4">
        <li v-for="a in ARTICLES" :key="a.slug">
          <a :href="articlePath(a.slug)" class="link link-hover opacity-70">{{ a.h1 }}</a>
        </li>
        <li>
          <a href="/embed" class="link link-hover opacity-70">
            For rescues: a free scam-check widget
          </a>
        </li>
      </ul>
      <!-- No disclaimer here: ResultsFallback already ends the search screens with one, and two
           near-identical ones stacked a scroll apart is how a caveat stops being read. -->
    </div>
  </footer>
</template>
