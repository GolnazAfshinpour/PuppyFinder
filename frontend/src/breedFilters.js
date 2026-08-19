// Breed-narrowing filters backed by the curated catalog's trait scores (1–5).
// These only narrow OUR breed dropdown (the chosen breed then carries to every
// site via its link) — they never claim to filter the external sites themselves.
// External (dog.ceo) breeds report null traits, so they drop out whenever a
// trait filter is active — same behavior the Size filter already has.

export const TRAITS = [
  // "Breeds" is doing real work in this label. There is now a per-dog "Good with kids" filter
  // fed by the rescues' own listings, and two controls reading "Good with kids" would have
  // produced two identical removable chips above the results meaning different things.
  { key: 'kids', label: '🧒 Kid-friendly breeds', matches: (b) => b.kidFriendly >= 4 },
  { key: 'apartment', label: '🏢 Apartment-friendly', matches: (b) => b.apartmentFriendly >= 4 },
  { key: 'lowshed', label: '🧥 Low-shedding', matches: (b) => b.shedding !== null && b.shedding <= 2 },
]

export function breedMatches(breed, { size, traits }) {
  if (size && breed.size !== size) return false
  return traits.every((key) => {
    const trait = TRAITS.find((t) => t.key === key)
    return trait ? trait.matches(breed) : true
  })
}
