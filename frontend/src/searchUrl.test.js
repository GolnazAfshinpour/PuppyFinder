import { describe, expect, it } from 'vitest'
import { buildSearchQuery, parseSearchUrl } from './searchUrl.js'

const US_STATES = ['TX', 'NY', 'CA', 'ME']

const defaults = { breed: '', state: '', city: '', size: '', age: '', traits: [], goal: 'buy', sort: '', dog: '' }

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
      dog: '',
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

  it('carries an open dog detail so a single dog is shareable', () => {
    expect(parseSearchUrl('?dog=montgomery-county-animal-services-a542024', US_STATES).dog)
      .toBe('montgomery-county-animal-services-a542024')
    expect(buildSearchQuery({ ...defaults, dog: 'abc-123' })).toBe('dog=abc-123')
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
    expect(buildSearchQuery({ ...defaults, city: 'Houston', goal: 'buy' })).toBe('')
  })

  it('treats buy as the default goal and only serializes the others', () => {
    expect(buildSearchQuery({ ...defaults, goal: 'buy' })).toBe('')
    expect(buildSearchQuery({ ...defaults, goal: 'adopt' })).toBe('goal=adopt')
    expect(parseSearchUrl('', US_STATES).goal).toBe('buy')
    expect(parseSearchUrl('?goal=nonsense', US_STATES).goal).toBe('buy')
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
      dog: 'king-county-pet-adoption-a750208',
    }
    expect(parseSearchUrl(`?${buildSearchQuery(state)}`, US_STATES)).toEqual(state)
  })
})
