import { describe, expect, it } from 'vitest'
import { buildSearchQuery, parseSearchUrl } from './searchUrl.js'

const US_STATES = ['TX', 'NY', 'CA', 'ME']

const defaults = { breed: '', state: '', city: '', size: '', age: '', traits: [], goal: 'both', sort: '' }

describe('parseSearchUrl', () => {
  it('returns clean defaults for an empty query', () => {
    expect(parseSearchUrl('', US_STATES)).toEqual(defaults)
  })

  it('restores a full search', () => {
    expect(
      parseSearchUrl(
        '?breed=golden-retriever&state=TX&city=Houston&size=Large&age=Puppy&traits=kids,lowshed&goal=buy&sort=youngest',
        US_STATES,
      ),
    ).toEqual({
      breed: 'golden-retriever',
      state: 'TX',
      city: 'Houston',
      size: 'Large',
      age: 'Puppy',
      traits: ['kids', 'lowshed'],
      goal: 'buy',
      sort: 'youngest',
    })
  })

  it('normalizes case for state, size and age', () => {
    const parsed = parseSearchUrl('?state=tx&size=large&age=SENIOR', US_STATES)
    expect(parsed.state).toBe('TX')
    expect(parsed.size).toBe('Large')
    expect(parsed.age).toBe('Senior')
  })

  it('drops values that fail validation instead of erroring', () => {
    const parsed = parseSearchUrl(
      '?state=ZZ&size=gigantic&age=teenager&goal=steal&traits=&sort=nearest',
      US_STATES,
    )
    expect(parsed).toEqual(defaults)
  })

  it('ignores the retired tab parameter from older shared links', () => {
    expect(parseSearchUrl('?tab=adopt&breed=beagle', US_STATES)).toEqual({ ...defaults, breed: 'beagle' })
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
      age: 'Young',
      traits: ['apartment'],
      goal: 'adopt',
      sort: 'oldest',
    }
    expect(parseSearchUrl(`?${buildSearchQuery(state)}`, US_STATES)).toEqual(state)
  })
})
