import { beforeEach, describe, expect, it, vi } from 'vitest'
import { clearProfile, loadProfile, rankListings, saveProfile, scoreListing } from './adopterProfile.js'

// Node's built-in localStorage is nonfunctional without --localstorage-file;
// stub a real one for the persistence tests.
const store = new Map()
vi.stubGlobal('localStorage', {
  getItem: (k) => store.get(k) ?? null,
  setItem: (k, v) => store.set(k, String(v)),
  removeItem: (k) => store.delete(k),
  clear: () => store.clear(),
})

const scores = [
  { slug: 'siberian-husky', searchName: 'Siberian Husky', matchPercent: 91 },
  { slug: 'beagle', searchName: 'Beagle', matchPercent: 74 },
  { slug: 'pomeranian', searchName: 'Pomeranian', matchPercent: 55 },
]

describe('scoreListing', () => {
  it('matches a quiz breed inside free-text shelter breed names', () => {
    expect(scoreListing({ breed: 'Siberian Husky / Bull Terrier' }, scores)).toBe(91)
  })

  it('takes the best score for a mix of two quiz breeds', () => {
    expect(scoreListing({ breed: 'Pomeranian / Siberian Husky' }, scores)).toBe(91)
  })

  it('returns null when no quiz breed appears', () => {
    expect(scoreListing({ breed: 'Dutch Sheepdog / Shih Tzu' }, scores)).toBeNull()
    expect(scoreListing({ breed: null }, scores)).toBeNull()
  })
})

describe('rankListings', () => {
  it('sorts scored listings first by fit, keeps feed order for ties and unscored', () => {
    const listings = [
      { id: 'a', breed: 'Mixed Breed' },
      { id: 'b', breed: 'Beagle / Labrador Retriever' },
      { id: 'c', breed: 'Siberian Husky' },
      { id: 'd', breed: 'Great Pyrenees' },
      { id: 'e', breed: 'Beagle' },
    ]
    const ranked = rankListings(listings, scores)
    expect(ranked.map((l) => l.id)).toEqual(['c', 'b', 'e', 'a', 'd'])
    expect(ranked[0].fit).toBe(91)
    expect(ranked[3].fit).toBeNull()
  })
})

describe('profile persistence', () => {
  beforeEach(() => localStorage.clear())

  it('round-trips through localStorage and clears', () => {
    expect(loadProfile()).toBeNull()
    saveProfile({ home: 'apartment' }, scores)
    expect(loadProfile()?.scores).toHaveLength(3)
    clearProfile()
    expect(loadProfile()).toBeNull()
  })

  it('treats an empty or corrupt entry as no profile', () => {
    localStorage.setItem('puppyfinder-profile', '{not json')
    expect(loadProfile()).toBeNull()
    localStorage.setItem('puppyfinder-profile', JSON.stringify({ answers: {}, scores: [] }))
    expect(loadProfile()).toBeNull()
  })
})
