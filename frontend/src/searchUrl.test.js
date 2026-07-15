import { describe, expect, it } from 'vitest'
import { buildSearchQuery, parseSearchUrl } from './searchUrl.js'

const US_STATES = ['TX', 'NY', 'CA', 'ME']

const defaults = { breed: '', state: '', city: '', size: '', traits: [], goal: 'both', tab: 'sites' }

describe('parseSearchUrl', () => {
  it('returns clean defaults for an empty query', () => {
    expect(parseSearchUrl('', US_STATES)).toEqual(defaults)
  })

  it('restores a full search', () => {
    expect(
      parseSearchUrl('?breed=golden-retriever&state=TX&city=Houston&size=Large&traits=kids,lowshed&goal=buy&tab=adopt', US_STATES),
    ).toEqual({
      breed: 'golden-retriever',
      state: 'TX',
      city: 'Houston',
      size: 'Large',
      traits: ['kids', 'lowshed'],
      goal: 'buy',
      tab: 'adopt',
    })
  })

  it('normalizes case for state and size', () => {
    const parsed = parseSearchUrl('?state=tx&size=large', US_STATES)
    expect(parsed.state).toBe('TX')
    expect(parsed.size).toBe('Large')
  })

  it('drops values that fail validation instead of erroring', () => {
    const parsed = parseSearchUrl('?state=ZZ&size=gigantic&goal=steal&traits=&tab=nonsense', US_STATES)
    expect(parsed).toEqual(defaults)
  })
})

describe('buildSearchQuery', () => {
  it('returns an empty string when nothing is set', () => {
    expect(buildSearchQuery(defaults)).toBe('')
  })

  it('omits defaults and city-without-state', () => {
    expect(buildSearchQuery({ ...defaults, city: 'Houston', goal: 'both' })).toBe('')
  })

  it('round-trips through parseSearchUrl', () => {
    const state = {
      breed: 'french-bulldog',
      state: 'NY',
      city: 'New York',
      size: 'Small',
      traits: ['apartment'],
      goal: 'adopt',
      tab: 'adopt',
    }
    expect(parseSearchUrl(`?${buildSearchQuery(state)}`, US_STATES)).toEqual(state)
  })
})
