// Round-trips the search state through the page URL so searches are
// bookmarkable and shareable. Only set filters appear; defaults stay clean.

const GOALS = ['adopt', 'buy', 'both']
const SIZES = ['Teacup', 'Small', 'Medium', 'Large']
const TABS = ['sites', 'adopt']

export function parseSearchUrl(search, usStates) {
  const params = new URLSearchParams(search)
  const state = (params.get('state') ?? '').toUpperCase()
  const size = SIZES.find((s) => s.toLowerCase() === (params.get('size') ?? '').toLowerCase())
  const goal = params.get('goal')
  const tab = params.get('tab')
  return {
    breed: params.get('breed') ?? '',
    state: usStates.includes(state) ? state : '',
    city: params.get('city') ?? '',
    size: size ?? '',
    traits: (params.get('traits') ?? '').split(',').filter(Boolean),
    goal: GOALS.includes(goal) ? goal : 'both',
    tab: TABS.includes(tab) ? tab : 'sites',
  }
}

export function buildSearchQuery({ breed, state, city, size, traits, goal, tab }) {
  const params = new URLSearchParams()
  if (breed) params.set('breed', breed)
  if (state) params.set('state', state)
  if (city.trim() && state) params.set('city', city.trim())
  if (size) params.set('size', size)
  if (traits.length) params.set('traits', traits.join(','))
  if (goal !== 'both') params.set('goal', goal)
  if (tab !== 'sites') params.set('tab', tab)
  return params.toString()
}
