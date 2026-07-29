// Anonymous favorites + recently-viewed, in localStorage (no accounts — forced
// signup kills save features, per Baymard). We store snapshots rather than ids:
// shelter listings vanish from the feeds when dogs are adopted, but a saved
// dog's card should survive with its name, photo, and link intact.

const FAV_KEY = 'puppyfinder-favorites'
const RECENT_KEY = 'puppyfinder-recent'
const RECENT_MAX = 12

function read(key) {
  try {
    const value = JSON.parse(localStorage.getItem(key))
    return Array.isArray(value) ? value : []
  } catch {
    return []
  }
}

function write(key, value) {
  localStorage.setItem(key, JSON.stringify(value))
}

function snapshot(listing) {
  const { id, name, breed, imageUrl, listingUrl, city, state, source, contactInfo, animalRef } = listing
  return { id, name, breed, imageUrl, listingUrl, city, state, source, contactInfo, animalRef }
}

export function loadFavorites() {
  return read(FAV_KEY)
}

/// Adds the listing if absent, removes it if present; returns the new list.
export function toggleFavorite(listing) {
  const all = read(FAV_KEY)
  const index = all.findIndex((f) => f.id === listing.id)
  if (index >= 0) all.splice(index, 1)
  else all.unshift(snapshot(listing))
  write(FAV_KEY, all)
  return all
}

export function loadRecent() {
  return read(RECENT_KEY)
}

/// Most-recent-first, deduped, capped; returns the new list.
export function recordViewed(listing) {
  const all = read(RECENT_KEY).filter((r) => r.id !== listing.id)
  all.unshift(snapshot(listing))
  const capped = all.slice(0, RECENT_MAX)
  write(RECENT_KEY, capped)
  return capped
}
