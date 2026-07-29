import { beforeEach, describe, expect, it, vi } from 'vitest'
import { loadFavorites, loadRecent, recordViewed, toggleFavorite } from './favorites.js'

const store = new Map()
vi.stubGlobal('localStorage', {
  getItem: (k) => store.get(k) ?? null,
  setItem: (k, v) => store.set(k, String(v)),
  removeItem: (k) => store.delete(k),
})

const dog = (id, name = id) => ({
  id, name, breed: 'Beagle', imageUrl: null, listingUrl: `https://x/${id}`,
  city: 'Kent', state: 'WA', source: 'Shelter', contactInfo: null, animalRef: null,
  fit: 90, description: 'not persisted',
})

beforeEach(() => store.clear())

describe('favorites', () => {
  it('starts empty and toggles on/off', () => {
    expect(loadFavorites()).toEqual([])
    expect(toggleFavorite(dog('a'))).toHaveLength(1)
    expect(loadFavorites()[0].name).toBe('a')
    expect(toggleFavorite(dog('a'))).toHaveLength(0)
  })

  it('stores a snapshot without transient fields like fit or description', () => {
    toggleFavorite(dog('a'))
    const saved = loadFavorites()[0]
    expect(saved.listingUrl).toBe('https://x/a')
    expect(saved).not.toHaveProperty('fit')
    expect(saved).not.toHaveProperty('description')
  })

  it('survives corrupt storage', () => {
    store.set('puppyfinder-favorites', '{broken')
    expect(loadFavorites()).toEqual([])
  })
})

describe('recently viewed', () => {
  it('dedupes to most-recent-first and caps at 12', () => {
    for (let i = 0; i < 15; i++) recordViewed(dog(`d${i}`))
    recordViewed(dog('d5'))
    const recent = loadRecent()
    expect(recent).toHaveLength(12)
    expect(recent[0].id).toBe('d5')
    expect(recent.filter((r) => r.id === 'd5')).toHaveLength(1)
  })
})
