// The saved adopter profile: quiz answers plus a per-breed fit score from the
// API, kept in localStorage. Used to re-rank live shelter listings by fit —
// a listing inherits the best score among quiz breeds named in its breed text.

const KEY = 'puppyfinder-profile'

export function loadProfile() {
  try {
    const raw = localStorage.getItem(KEY)
    if (!raw) return null
    const profile = JSON.parse(raw)
    return Array.isArray(profile?.scores) && profile.scores.length > 0 ? profile : null
  } catch {
    return null
  }
}

export function saveProfile(answers, scores) {
  const profile = { answers, scores, savedAt: new Date().toISOString() }
  localStorage.setItem(KEY, JSON.stringify(profile))
  return profile
}

export function clearProfile() {
  localStorage.removeItem(KEY)
}

/// Best matchPercent among quiz breeds appearing in the listing's breed text
/// ("Pomeranian / Siberian Husky" → max of both), or null when no quiz breed matches.
export function scoreListing(listing, scores) {
  const breedText = (listing.breed ?? '').toLowerCase()
  let best = null
  for (const s of scores) {
    if (!s.searchName) continue
    if (breedText.includes(s.searchName.toLowerCase()) && (best === null || s.matchPercent > best)) {
      best = s.matchPercent
    }
  }
  return best
}

/// Scored listings first (best fit first, ties keep feed order), unscored after.
export function rankListings(listings, scores) {
  return listings
    .map((listing, index) => ({ listing, index, fit: scoreListing(listing, scores) }))
    .sort((a, b) => (b.fit ?? -1) - (a.fit ?? -1) || a.index - b.index)
    .map((entry) => ({ ...entry.listing, fit: entry.fit }))
}
