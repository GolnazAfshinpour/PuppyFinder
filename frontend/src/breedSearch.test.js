import { describe, expect, it } from 'vitest'
import { rankBreeds } from './breedSearch.js'

const BREEDS = [
  { slug: 'golden-retriever', displayName: 'Golden Retriever' },
  { slug: 'labrador-retriever', displayName: 'Labrador Retriever' },
  { slug: 'poodle', displayName: 'Poodle (Standard)' },
  { slug: 'cockapoo', displayName: 'Cockapoo' },
  { slug: 'australian-shepherd', displayName: 'Australian Shepherd' },
  { slug: 'german-shepherd', displayName: 'German Shepherd' },
  { slug: 'french-bulldog', displayName: 'French Bulldog' },
  { slug: 'bulldog', displayName: 'Bulldog' },
]

const names = (query) => rankBreeds(BREEDS, query).map((b) => b.displayName)

describe('rankBreeds', () => {
  it('returns everything for an empty query', () => {
    expect(rankBreeds(BREEDS, '')).toHaveLength(BREEDS.length)
    expect(rankBreeds(BREEDS, '   ')).toHaveLength(BREEDS.length)
  })

  it('finds a breed by a word in the middle of its name', () => {
    // The reason the native <select> had to go: typing "retriever" into one jumps to names
    // beginning with R and finds nothing, because nobody files a Labrador Retriever under L.
    expect(names('retriever')).toEqual(['Golden Retriever', 'Labrador Retriever'])
    expect(names('shepherd')).toEqual(['Australian Shepherd', 'German Shepherd'])
  })

  it('puts a name that starts with the query above one that merely contains it', () => {
    expect(names('poo')).toEqual(['Poodle (Standard)', 'Cockapoo'])
  })

  it('puts an exact name first', () => {
    expect(names('bulldog')[0]).toBe('Bulldog')
    expect(names('bulldog')).toContain('French Bulldog')
  })

  it('ignores case and surrounding space', () => {
    expect(names('  FRENCH  ')).toEqual(['French Bulldog'])
  })

  it('matches the slug too, so a pasted one still resolves', () => {
    expect(names('french-bulldog')).toEqual(['French Bulldog'])
    // ...including when typed with a space instead of the hyphen.
    expect(names('german shepherd')).toEqual(['German Shepherd'])
  })

  it('returns nothing rather than everything when there is no match', () => {
    // An empty list is what lets the UI say "no breeds match"; falling back to the full list
    // would silently ignore what was typed.
    expect(names('xyzzy')).toEqual([])
  })

  it('breaks ties alphabetically, so the order never wobbles', () => {
    expect(names('r')).toEqual(rankBreeds(BREEDS, 'r').map((b) => b.displayName))
    expect(names('e')).toEqual([...names('e')])
  })

  it('survives breeds with missing fields', () => {
    expect(() => rankBreeds([{ slug: 'x' }, {}], 'a')).not.toThrow()
  })
})
