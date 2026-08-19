// Matching for the breed typeahead. Extracted so the ranking is testable without mounting a
// component — the same reason priceMeter.js exists.

/**
 * Breeds matching `query`, best first.
 *
 * Ranking, in order:
 *   1. exact name
 *   2. name starts with the query        — "poo" should reach Poodle before Cockapoo
 *   3. any word in the name starts with it — "retriever" reaches Golden Retriever
 *   4. name merely contains it
 *
 * Substring matching rather than prefix-only is the whole point of replacing the native
 * `<select>`: typing into one of those only jumps to names *beginning* with what you type, so
 * "retriever" found nothing and "shepherd" missed Australian Shepherd. Nobody thinks of a
 * Labrador Retriever as an L.
 */
export function rankBreeds(breeds, query) {
  const q = query.trim().toLowerCase()
  if (!q) return [...breeds]

  const scored = []
  for (const breed of breeds) {
    const name = (breed.displayName ?? '').toLowerCase()
    // The slug is matched too, so a pasted "french-bulldog" from a shared URL still resolves.
    const slug = (breed.slug ?? '').toLowerCase()
    let rank = null

    if (name === q) rank = 0
    else if (name.startsWith(q)) rank = 1
    else if (name.split(/[\s(]+/).some((word) => word.startsWith(q))) rank = 2
    else if (name.includes(q)) rank = 3
    else if (slug.includes(q.replace(/\s+/g, '-'))) rank = 4

    if (rank !== null) scored.push({ breed, rank, name })
  }

  return scored
    .sort((a, b) => a.rank - b.rank || a.name.localeCompare(b.name))
    .map((s) => s.breed)
}
