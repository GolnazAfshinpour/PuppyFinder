<script setup>
import { ref } from 'vue'

defineProps({
  listings: { type: Array, required: true },
  loading: { type: Boolean, default: false },
})

const brokenImages = ref(new Set())

function ageSex(listing) {
  return [listing.age, listing.sex].filter(Boolean).join(' • ')
}

function markImageBroken(id) {
  brokenImages.value = new Set(brokenImages.value).add(id)
}
</script>

<template>
  <section class="listings">
    <h2 class="section-title">🐾 Real adoptable dogs right now</h2>
    <p v-if="loading" class="status">Fetching live listings…</p>
    <p v-else-if="listings.length === 0" class="status">
      No live listings match this breed/state — try widening the search.
    </p>
    <ul v-else class="listing-grid">
      <li v-for="dog in listings" :key="dog.id" class="card">
        <a :href="dog.listingUrl" target="_blank" rel="noopener noreferrer" class="card-media">
          <img
            v-if="dog.imageUrl && !brokenImages.has(dog.id)"
            :src="dog.imageUrl"
            :alt="`${dog.name}, ${dog.breed}`"
            loading="lazy"
            @error="markImageBroken(dog.id)"
          />
          <div v-else class="media-fallback">🐾</div>
          <span class="breed-badge">{{ dog.breed }}</span>
        </a>
        <div class="card-body">
          <div class="card-title">
            <a :href="dog.listingUrl" target="_blank" rel="noopener noreferrer">{{ dog.name }}</a>
            <span v-if="ageSex(dog)" class="age-sex">{{ ageSex(dog) }}</span>
          </div>
          <div class="card-footer">
            <span class="location">📍 {{ dog.city }}, {{ dog.state }}</span>
            <a :href="dog.sourceUrl" target="_blank" rel="noopener noreferrer" class="source-chip">
              {{ dog.source }} ↗
            </a>
          </div>
        </div>
      </li>
    </ul>
  </section>
</template>

<style scoped>
.listings {
  margin-top: 3.5rem;
}

.section-title {
  text-align: center;
  font-size: 1.4rem;
  margin: 0 0 1.5rem;
}

.status {
  text-align: center;
  color: var(--text-muted);
}

.listing-grid {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 1.25rem;
}

.card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  overflow: hidden;
  box-shadow: var(--shadow);
  transition: box-shadow 0.25s, transform 0.25s;
  display: flex;
  flex-direction: column;
}

.card:hover {
  box-shadow: var(--shadow-hover);
  transform: translateY(-3px);
}

.card-media {
  position: relative;
  display: block;
  aspect-ratio: 3 / 2;
  background: var(--accent-soft);
}

.card-media img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

.media-fallback {
  width: 100%;
  height: 100%;
  display: grid;
  place-items: center;
  font-size: 2.5rem;
  background: linear-gradient(135deg, var(--accent-soft), #fdf6e3);
}

.breed-badge {
  position: absolute;
  left: 0.6rem;
  bottom: 0.6rem;
  padding: 0.25rem 0.7rem;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.92);
  color: var(--text-strong);
  font-size: 0.75rem;
  font-weight: 600;
  backdrop-filter: blur(4px);
  box-shadow: 0 1px 4px rgba(28, 24, 38, 0.15);
}

.card-body {
  padding: 0.9rem 1rem 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  flex: 1;
}

.card-title {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 0.5rem;
}

.card-title a {
  font-size: 1.05rem;
  font-weight: 650;
  color: var(--text-strong);
  text-decoration: none;
}

.card-title a:hover {
  color: var(--accent);
}

.age-sex {
  font-size: 0.75rem;
  color: var(--text-muted);
  white-space: nowrap;
}

.card-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.location {
  font-size: 0.8rem;
  color: var(--text-muted);
}

.source-chip {
  font-size: 0.7rem;
  font-weight: 600;
  padding: 0.15rem 0.6rem;
  border-radius: 999px;
  background: var(--accent-soft);
  color: var(--accent);
  text-decoration: none;
}
</style>
