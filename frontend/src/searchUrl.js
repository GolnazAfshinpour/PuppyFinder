// Round-trips the search state through the page URL so searches are
// bookmarkable and shareable. Only set filters appear; defaults stay clean.

// Buying is the app's primary path, so it's the default and never appears in the
// URL; 'adopt' and 'both' are the explicit choices.
const GOALS = ['adopt', 'buy', 'both']
const DEFAULT_GOAL = 'buy'
const SIZES = ['Teacup', 'Small', 'Medium', 'Large']
export const AGES = ['Puppy', 'Young', 'Adult', 'Senior']
const SORTS = ['youngest', 'oldest']

// `tab` used to select between the site directory and the listings. Results are
// now one list, so a shared pre-July-2026 URL simply loses the parameter.
export function parseSearchUrl(search, usStates) {
  const params = new URLSearchParams(search)
  const state = (params.get('state') ?? '').toUpperCase()
  const match = (values, raw) => values.find((v) => v.toLowerCase() === (raw ?? '').toLowerCase())
  const goal = params.get('goal')
  return {
    breed: params.get('breed') ?? '',
    state: usStates.includes(state) ? state : '',
    city: params.get('city') ?? '',
    size: match(SIZES, params.get('size')) ?? '',
    age: match(AGES, params.get('age')) ?? '',
    traits: (params.get('traits') ?? '').split(',').filter(Boolean),
    goal: GOALS.includes(goal) ? goal : DEFAULT_GOAL,
    sort: match(SORTS, params.get('sort')) ?? '',
    // Which dog's detail view is open. Ids are server-generated slugs, so anything
    // unexpected here just fails the lookup and shows the "no longer listed" state.
    dog: params.get('dog') ?? '',
  }
}

export function buildSearchQuery({ breed, state, city, size, age, traits, goal, sort, dog }) {
  const params = new URLSearchParams()
  if (breed) params.set('breed', breed)
  if (state) params.set('state', state)
  if (city.trim() && state) params.set('city', city.trim())
  if (size) params.set('size', size)
  if (age) params.set('age', age)
  if (traits.length) params.set('traits', traits.join(','))
  if (goal !== DEFAULT_GOAL) params.set('goal', goal)
  if (sort) params.set('sort', sort)
  if (dog) params.set('dog', dog)
  return params.toString()
}
