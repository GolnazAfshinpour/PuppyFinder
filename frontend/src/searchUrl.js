// Round-trips the search state through the page URL so searches are
// bookmarkable and shareable. Only set filters appear; defaults stay clean.

// Buying is the app's primary path, so it's the default and never appears in the
// URL; 'adopt' and 'both' are the explicit choices.
const GOALS = ['adopt', 'buy', 'both']
const DEFAULT_GOAL = 'buy'
const SIZES = ['Teacup', 'Small', 'Medium', 'Large']
export const AGES = ['Puppy', 'Young', 'Adult', 'Senior']
// 'nearest' is only offered when a ZIP or geolocation supplied an origin, but it is accepted
// here regardless: a shared URL carries the zip alongside it, and App.vue drops the sort if
// the origin fails to resolve.
const SORTS = ['nearest', 'youngest', 'oldest']

// From the rescue's own listing, not from the breed table — see SearchHub for why the two are
// separate controls. Order is fixed so the URL and the chips read the same way every time.
export const GOOD_WITH = ['kids', 'dogs', 'cats']

// Radii the UI offers. Anything else in a URL is ignored rather than trusted — this value
// goes straight into a distance comparison.
const RADII = ['25', '50', '100', '250']

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
    // Unknown values are dropped rather than passed through: this goes into a filter that
    // removes dogs, and a typo in a shared link should not silently narrow someone's search.
    goodWith: GOOD_WITH.filter((w) => (params.get('goodWith') ?? '').split(',').includes(w)),
    goal: GOALS.includes(goal) ? goal : DEFAULT_GOAL,
    sort: match(SORTS, params.get('sort')) ?? '',
    // Five digits or nothing. A malformed ZIP would fail the lookup anyway, but rejecting it
    // here keeps a junk value out of the input box on load.
    zip: /^\d{5}$/.test(params.get('zip') ?? '') ? params.get('zip') : '',
    radius: RADII.includes(params.get('radius') ?? '') ? params.get('radius') : '',
    // Which dog's detail view is open. Ids are server-generated slugs, so anything
    // unexpected here just fails the lookup and shows the "no longer listed" state.
    dog: params.get('dog') ?? '',
  }
}

export function buildSearchQuery({ breed, state, city, size, age, traits, goodWith, goal, sort, dog, zip, radius }) {
  const params = new URLSearchParams()
  if (breed) params.set('breed', breed)
  if (state) params.set('state', state)
  if (city.trim() && state) params.set('city', city.trim())
  if (size) params.set('size', size)
  if (age) params.set('age', age)
  if (traits.length) params.set('traits', traits.join(','))
  // Canonical order, so two searches for the same thing produce the same URL.
  if (goodWith?.length) params.set('goodWith', GOOD_WITH.filter((w) => goodWith.includes(w)).join(','))
  if (goal !== DEFAULT_GOAL) params.set('goal', goal)
  if (sort) params.set('sort', sort)
  if (zip) params.set('zip', zip)
  // Only meaningful with a ZIP: a bare radius on a shared link would look applied and do nothing.
  if (radius && zip) params.set('radius', radius)
  if (dog) params.set('dog', dog)
  return params.toString()
}
