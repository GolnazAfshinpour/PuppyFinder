import { describe, expect, it } from 'vitest'
import { goodWith, goodWithBadges, goodWithLine, joinList } from './goodWith.js'

describe('goodWith', () => {
  it('separates yes, no and not-recorded', () => {
    const result = goodWith({ goodWithKids: true, goodWithDogs: false, goodWithCats: null })
    expect(result).toEqual({ yes: ['kids'], no: ['dogs'], unknown: ['cats'], known: true })
  })

  it('treats a missing field as unknown, never as no', () => {
    // The feed omits null attributes entirely, so absence is the common case — and reading it
    // as "no" would rule a dog out over a blank field.
    const result = goodWith({ goodWithKids: true })
    expect(result.unknown).toEqual(['dogs', 'cats'])
    expect(result.no).toEqual([])
  })

  it('reports nothing known when the rescue recorded nothing', () => {
    expect(goodWith({}).known).toBe(false)
    expect(goodWith(null).unknown).toEqual(['kids', 'dogs', 'cats'])
  })
})

describe('goodWithLine', () => {
  it('states the positives and the negatives', () => {
    expect(goodWithLine({ goodWithKids: true, goodWithDogs: true, goodWithCats: false }))
      .toBe('Good with kids and dogs · not with cats')
  })

  it('states a negative on its own — someone with a cat needs it most', () => {
    expect(goodWithLine({ goodWithCats: false })).toBe('not with cats')
  })

  it('says nothing at all when nothing was recorded', () => {
    // "Not recorded" on three quarters of the grid is noise; the prompt to ask belongs on the
    // detail view, where there is room for it.
    expect(goodWithLine({})).toBe('')
    expect(goodWithLine({ goodWithKids: null, goodWithDogs: undefined })).toBe('')
  })
})

describe('goodWithBadges', () => {
  it('carries the meaning in the word, not the colour', () => {
    const badges = goodWithBadges({ goodWithKids: true, goodWithCats: false })
    expect(badges.map((b) => b.text)).toEqual(['Good with kids', 'Not good with cats'])
  })

  it('does not colour an ordinary fact as an alarm', () => {
    // "Not good with cats" is a fact about a dog, not a warning about a seller.
    const [notGood] = goodWithBadges({ goodWithCats: false })
    expect(notGood.tone).not.toContain('error')
    expect(notGood.tone).not.toContain('warning')
  })

  it('emits nothing for fields the rescue left blank', () => {
    expect(goodWithBadges({})).toEqual([])
  })
})

describe('joinList', () => {
  it('reads as a sentence', () => {
    expect(joinList([])).toBe('')
    expect(joinList(['kids'])).toBe('kids')
    expect(joinList(['kids', 'dogs'])).toBe('kids and dogs')
    expect(joinList(['kids', 'dogs', 'cats'])).toBe('kids, dogs and cats')
  })
})
