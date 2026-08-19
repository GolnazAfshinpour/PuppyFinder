import { describe, expect, it } from 'vitest'
import { buildSearchQuery, parseSearchUrl } from './searchUrl.js'

const US_STATES = ['TX', 'NY', 'CA', 'ME']

const defaults = {
  breed: '', state: '', city: '', size: '', age: '', traits: [], goodWith: [], goal: 'buy',
  sort: '', zip: '', radius: '', dog: '',
}

describe('parseSearchUrl', () => {
  it('returns clean defaults for an empty query', () => {
    expect(parseSearchUrl('', US_STATES)).toEqual(defaults)
  })

  it('restores a full search', () => {
    expect(
      parseSearchUrl(
        '?breed=golden-retriever&state=TX&city=Houston&size=Large&age=Puppy&traits=kids,lowshed'
          + '&goal=buy&sort=youngest&zip=77002&radius=50',
        US_STATES,
      ),
    ).toEqual({
      breed: 'golden-retriever',
      state: 'TX',
      city: 'Houston',
      size: 'Large',
      age: 'Puppy',
      traits: ['kids', 'lowshed'],
      goodWith: [],
      goal: 'buy',
      sort: 'youngest',
      zip: '77002',
      radius: '50',
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
    // 'nearest' used to belong in this list and is now a real sort, so the rejected example is a
    // sort that still does not exist. The assertion was always right; only the premise moved.
    const parsed = parseSearchUrl(
      '?state=ZZ&size=gigantic&age=teenager&goal=steal&traits=&sort=cheapest',
      US_STATES,
    )
    expect(parsed).toEqual(defaults)
  })

  it('accepts nearest now that distance search exists', () => {
    expect(parseSearchUrl('?sort=nearest&zip=20009', US_STATES).sort).toBe('nearest')
  })

  it('only accepts a five-digit ZIP', () => {
    // This value ends up in a distance comparison, so anything else is dropped rather than trusted.
    for (const bad of ['2000', '200099', 'abcde', '2000a', '']) {
      expect(parseSearchUrl(`?zip=${bad}`, US_STATES).zip).toBe('')
    }

    expect(parseSearchUrl('?zip=20009', US_STATES).zip).toBe('20009')
  })

  it('only accepts a radius the UI actually offers', () => {
    expect(parseSearchUrl('?zip=20009&radius=50', US_STATES).radius).toBe('50')
    for (const bad of ['5', '9999', 'near', '-50']) {
      expect(parseSearchUrl(`?zip=20009&radius=${bad}`, US_STATES).radius).toBe('')
    }
  })

  it('carries an open dog detail so a single dog is shareable', () => {
    expect(parseSearchUrl('?dog=montgomery-county-animal-services-a542024', US_STATES).dog)
      .toBe('montgomery-county-animal-services-a542024')
    expect(buildSearchQuery({ ...defaults, dog: 'abc-123' })).toBe('dog=abc-123')
  })

  it('opens a shared dog link in adopt mode, since every dog here is a rescue dog', () => {
    // Without this, a shared dog opened over "Buy a puppy. Don't get scammed." with
    // breeder marketplaces behind the detail view.
    expect(parseSearchUrl('?dog=abc-123', US_STATES).goal).toBe('adopt')
    // An explicit goal in the link still wins.
    expect(parseSearchUrl('?dog=abc-123&goal=buy', US_STATES).goal).toBe('buy')
  })

  it('ignores the retired tab parameter from older shared links', () => {
    expect(parseSearchUrl('?tab=adopt&breed=beagle', US_STATES)).toEqual({ ...defaults, breed: 'beagle' })
  })

  it('drops a radius that arrives without a ZIP to measure from', () => {
    // Serialising one would make a shared link look filtered while doing nothing.
    expect(buildSearchQuery({ ...defaults, radius: '50' })).toBe('')
    expect(buildSearchQuery({ ...defaults, zip: '20009', radius: '50' })).toBe('zip=20009&radius=50')
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

  it('keeps only the good-with values it knows', () => {
    // This one narrows the results, so a typo in a shared link must not silently remove dogs.
    expect(parseSearchUrl('?goodWith=kids,horses,cats', US_STATES).goodWith).toEqual(['kids', 'cats'])
    expect(parseSearchUrl('?goodWith=', US_STATES).goodWith).toEqual([])
    // Not a substring match: "kid" is not "kids".
    expect(parseSearchUrl('?goodWith=kid', US_STATES).goodWith).toEqual([])
  })

  it('writes good-with in a fixed order, so one search is one URL', () => {
    // Two people who picked the same filters in a different order should share the same link.
    expect(buildSearchQuery({ ...defaults, goodWith: ['cats', 'kids'] })).toBe('goodWith=kids%2Ccats')
    expect(buildSearchQuery({ ...defaults, goodWith: ['kids', 'cats'] })).toBe('goodWith=kids%2Ccats')
  })

  it('omits good-with entirely when nothing is selected', () => {
    expect(buildSearchQuery(defaults)).toBe('')
  })

  it('round-trips through parseSearchUrl', () => {
    const state = {
      breed: 'french-bulldog',
      state: 'NY',
      city: 'New York',
      size: 'Small',
      age: 'Young',
      traits: ['apartment'],
      goodWith: ['kids', 'cats'],
      goal: 'adopt',
      sort: 'nearest',
      zip: '11238',
      radius: '25',
      dog: 'king-county-pet-adoption-a750208',
    }
    expect(parseSearchUrl(`?${buildSearchQuery(state)}`, US_STATES)).toEqual(state)
  })
})
