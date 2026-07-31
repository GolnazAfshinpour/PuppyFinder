import { describe, expect, it } from 'vitest'
import { parseQuery } from './smartSearch.js'

const ctx = {
  breeds: [
    { slug: 'golden-retriever', displayName: 'Golden Retriever' },
    { slug: 'french-bulldog', displayName: 'French Bulldog' },
    { slug: 'poodle', displayName: 'Poodle (Standard)' },
    { slug: 'affenpinscher', displayName: 'Affenpinscher' },
  ],
  usStates: ['MD', 'WA', 'TX', 'NY', 'IN', 'ME', 'OR', 'HI'],
}

describe('parseQuery', () => {
  it('parses breed + city + state words', () => {
    const r = parseQuery('golden retriever near seattle washington', ctx)
    expect(r.breed).toBe('golden-retriever')
    expect(r.state).toBe('WA')
    expect(r.city).toBe('Seattle')
    expect(r.unmatched).toEqual([])
  })

  it('understands aliases, sizes, traits and goals', () => {
    const r = parseQuery('small hypoallergenic frenchie to adopt for an apartment', ctx)
    expect(r.breed).toBe('french-bulldog')
    expect(r.size).toBe('Small')
    expect(r.traits).toEqual(expect.arrayContaining(['lowshed', 'apartment']))
    expect(r.goal).toBe('adopt')
  })

  it('matches uppercase state abbreviations but not lowercase stopwords', () => {
    const md = parseQuery('poodle in MD', ctx)
    expect(md.state).toBe('MD')
    expect(md.city).toBe('') // "in MD" must not double as city "Md"
    // "in", "me", "or", "hi" must never become Indiana/Maine/Oregon/Hawaii
    const r = parseQuery('find me a dog in oregon or hi energy', ctx)
    expect(r.state).toBe('OR') // the word "oregon", not the token "or"
  })

  it('prefers the full breed name over a shorter alias', () => {
    expect(parseQuery('golden retriever', ctx).breed).toBe('golden-retriever')
    expect(parseQuery('golden', ctx).breed).toBe('golden-retriever')
  })

  it('strips parentheticals from breed names', () => {
    expect(parseQuery('poodle puppies', ctx).breed).toBe('poodle')
  })

  it('parses the age group — the filter the product is named after', () => {
    expect(parseQuery('poodle puppies', ctx).age).toBe('Puppy')
    expect(parseQuery('senior dog near me', ctx).age).toBe('Senior')
    expect(parseQuery('young adult golden retriever', ctx).age).toBe('Young')
    expect(parseQuery('golden retriever', ctx).age).toBe('')
  })

  it('resolves "young puppy" to Puppy without leaving "young" unmatched', () => {
    const r = parseQuery('young puppy in TX', ctx)
    expect(r.age).toBe('Puppy')
    expect(r.state).toBe('TX')
    expect(r.unmatched).toEqual([])
  })

  it('infers the state from a known metro city', () => {
    const r = parseQuery('golden retriever near seattle', ctx)
    expect(r.city).toBe('Seattle')
    expect(r.state).toBe('WA')
    expect(r.inferredState).toBe('WA')
    // explicit state always beats inference
    expect(parseQuery('poodle near portland maine', { ...ctx, usStates: [...ctx.usStates, 'ME'] }).state).toBe('ME')
  })

  it('parses multi-word trait phrases like apartment friendly', () => {
    const r = parseQuery('apartment friendly french bulldog', ctx)
    expect(r.traits).toContain('apartment')
    expect(r.breed).toBe('french-bulldog')
    expect(r.unmatched).toEqual([])
  })

  it('detects near me and reports unmatched words', () => {
    const r = parseQuery('fluffy purple dog near me', ctx)
    expect(r.nearMe).toBe(true)
    expect(r.unmatched).toEqual(expect.arrayContaining(['fluffy', 'purple']))
  })

  it('handles empty and filler-only input', () => {
    expect(parseQuery('', ctx)).toMatchObject({ breed: '', state: '', nearMe: false })
    expect(parseQuery('a dog for me', ctx).unmatched).toEqual([])
  })
})
