import { describe, expect, it } from 'vitest'
import { TRAITS, breedMatches } from './breedFilters.js'

// Curated breeds carry real trait scores; external (dog.ceo) breeds come from
// the API with every trait null — the null cases below are those breeds.
const golden = { size: 'Large', kidFriendly: 5, apartmentFriendly: 2, shedding: 4 }
const frenchie = { size: 'Small', kidFriendly: 4, apartmentFriendly: 5, shedding: 2 }
const external = { size: null, kidFriendly: null, apartmentFriendly: null, shedding: null }

const filters = (overrides = {}) => ({ size: '', traits: [], ...overrides })

describe('breedMatches', () => {
  it('passes everything when no filters are active', () => {
    for (const breed of [golden, frenchie, external]) {
      expect(breedMatches(breed, filters())).toBe(true)
    }
  })

  it('filters by size, excluding unknown-size external breeds', () => {
    expect(breedMatches(golden, filters({ size: 'Large' }))).toBe(true)
    expect(breedMatches(frenchie, filters({ size: 'Large' }))).toBe(false)
    expect(breedMatches(external, filters({ size: 'Large' }))).toBe(false)
  })

  it('requires kidFriendly >= 4 for the kids trait', () => {
    expect(breedMatches(golden, filters({ traits: ['kids'] }))).toBe(true)
    expect(breedMatches({ ...golden, kidFriendly: 3 }, filters({ traits: ['kids'] }))).toBe(false)
  })

  it('requires apartmentFriendly >= 4 for the apartment trait', () => {
    expect(breedMatches(frenchie, filters({ traits: ['apartment'] }))).toBe(true)
    expect(breedMatches(golden, filters({ traits: ['apartment'] }))).toBe(false)
  })

  it('requires shedding <= 2 for the low-shed trait', () => {
    expect(breedMatches(frenchie, filters({ traits: ['lowshed'] }))).toBe(true)
    expect(breedMatches(golden, filters({ traits: ['lowshed'] }))).toBe(false)
  })

  it('excludes null-trait external breeds from every trait filter', () => {
    // Guards the null <= 2 pitfall: in JS, null <= 2 is true (null coerces to 0),
    // so low-shed must explicitly reject unknown shedding data.
    for (const trait of TRAITS) {
      expect(breedMatches(external, filters({ traits: [trait.key] }))).toBe(false)
    }
  })

  it('requires ALL selected traits, combined with size', () => {
    expect(breedMatches(frenchie, filters({ size: 'Small', traits: ['kids', 'apartment', 'lowshed'] }))).toBe(true)
    expect(breedMatches(golden, filters({ size: 'Large', traits: ['kids', 'lowshed'] }))).toBe(false)
  })

  it('ignores unknown trait keys instead of throwing', () => {
    expect(breedMatches(golden, filters({ traits: ['no-such-trait'] }))).toBe(true)
  })
})

describe('TRAITS', () => {
  it('exposes the three UI chips with unique keys', () => {
    expect(TRAITS.map((t) => t.key).sort()).toEqual(['apartment', 'kids', 'lowshed'])
    for (const t of TRAITS) {
      expect(t.label).toBeTruthy()
      expect(typeof t.matches).toBe('function')
    }
  })
})
