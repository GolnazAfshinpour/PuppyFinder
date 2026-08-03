import { describe, expect, it } from 'vitest'
import { articleFor, withArticle } from './article.js'

describe('articleFor', () => {
  it('uses "a" before a consonant', () => {
    expect(articleFor('French Bulldog')).toBe('a')
    expect(articleFor('Beagle')).toBe('a')
    expect(articleFor('Poodle (Standard)')).toBe('a')
  })

  // The bug this exists for: "What a Afghan Hound actually costs" was on screen.
  it('uses "an" before a vowel', () => {
    expect(articleFor('Afghan Hound')).toBe('an')
    expect(articleFor('Akita')).toBe('an')
    expect(articleFor('Airedale')).toBe('an')
    expect(articleFor('English Setter')).toBe('an')
    expect(articleFor('Irish Wolfhound')).toBe('an')
    expect(articleFor('Italian Greyhound')).toBe('an')
    expect(articleFor('Old English Sheepdog')).toBe('an')
  })

  // Sound, not spelling, governs the article — and both of these are in the catalogue.
  it('follows the spoken sound where it differs from the spelling', () => {
    expect(articleFor('Eurasier')).toBe('a') // "yoo-rasier"
    expect(articleFor('Xoloitzcuintli')).toBe('an') // "sholo-"
  })

  it('is safe on empty or missing input', () => {
    expect(articleFor('')).toBe('a')
    expect(articleFor(null)).toBe('a')
    expect(articleFor(undefined)).toBe('a')
  })

  it('composes the full phrase', () => {
    expect(withArticle('Afghan Hound')).toBe('an Afghan Hound')
    expect(withArticle('Beagle')).toBe('a Beagle')
  })
})
