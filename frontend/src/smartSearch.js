// Client-side natural-language search: "golden retriever puppy near seattle"
// becomes structured filters via dictionary matching — the same NL→predefined-
// filters architecture the big players ship, minus the hallucination risk.

const STATE_NAMES = {
  alabama: 'AL', alaska: 'AK', arizona: 'AZ', arkansas: 'AR', california: 'CA',
  colorado: 'CO', connecticut: 'CT', delaware: 'DE', florida: 'FL', georgia: 'GA',
  hawaii: 'HI', idaho: 'ID', illinois: 'IL', indiana: 'IN', iowa: 'IA',
  kansas: 'KS', kentucky: 'KY', louisiana: 'LA', maine: 'ME', maryland: 'MD',
  massachusetts: 'MA', michigan: 'MI', minnesota: 'MN', mississippi: 'MS',
  missouri: 'MO', montana: 'MT', nebraska: 'NE', nevada: 'NV',
  'new hampshire': 'NH', 'new jersey': 'NJ', 'new mexico': 'NM', 'new york': 'NY',
  'north carolina': 'NC', 'north dakota': 'ND', ohio: 'OH', oklahoma: 'OK',
  oregon: 'OR', pennsylvania: 'PA', 'rhode island': 'RI', 'south carolina': 'SC',
  'south dakota': 'SD', tennessee: 'TN', texas: 'TX', utah: 'UT', vermont: 'VT',
  virginia: 'VA', washington: 'WA', 'west virginia': 'WV', wisconsin: 'WI', wyoming: 'WY',
}

const BREED_ALIASES = {
  lab: 'labrador-retriever', labrador: 'labrador-retriever',
  golden: 'golden-retriever', gsd: 'german-shepherd', 'german shepard': 'german-shepherd',
  frenchie: 'french-bulldog', yorkie: 'yorkshire-terrier', corgi: 'pembroke-welsh-corgi',
  husky: 'siberian-husky', pom: 'pomeranian', dane: 'great-dane',
  doxie: 'dachshund', 'wiener dog': 'dachshund', cavalier: 'cavalier-king-charles-spaniel',
  'standard poodle': 'poodle', berner: 'bernese-mountain-dog',
}

const SIZE_WORDS = [
  [['teacup', 'tiny'], 'Teacup'],
  [['small', 'little', 'lap dog', 'lapdog'], 'Small'],
  [['medium', 'mid size', 'mid sized', 'midsize'], 'Medium'],
  [['large', 'big', 'giant'], 'Large'],
]

const TRAIT_WORDS = [
  [['good with kids', 'kid friendly', 'kids', 'children', 'family dog', 'family'], 'kids'],
  [['apartment friendly', 'apartment', 'condo', 'city dog'], 'apartment'],
  [['hypoallergenic', 'low shedding', 'low shed', 'no shedding', 'doesnt shed', "doesn't shed", 'non shedding'], 'lowshed'],
]

const GOAL_WORDS = [
  [['adopt', 'adoption', 'rescue', 'shelter'], 'adopt'],
  [['buy', 'breeder', 'purchase', 'for sale'], 'buy'],
]

// Major-metro home states so "near seattle" filters without the user naming
// WA (same-name-city ambiguity accepted for the biggest metro; the hint says
// what was assumed). Includes our live shelter feeds' cities.
const CITY_STATES = {
  seattle: 'WA', tacoma: 'WA', kent: 'WA', spokane: 'WA',
  derwood: 'MD', baltimore: 'MD', rockville: 'MD',
  houston: 'TX', dallas: 'TX', austin: 'TX', 'san antonio': 'TX', 'fort worth': 'TX', 'el paso': 'TX',
  'new york': 'NY', brooklyn: 'NY', 'los angeles': 'CA', 'san francisco': 'CA', 'san diego': 'CA',
  'san jose': 'CA', sacramento: 'CA', chicago: 'IL', phoenix: 'AZ', philadelphia: 'PA',
  pittsburgh: 'PA', miami: 'FL', tampa: 'FL', orlando: 'FL', jacksonville: 'FL',
  atlanta: 'GA', boston: 'MA', denver: 'CO', detroit: 'MI', minneapolis: 'MN',
  portland: 'OR', 'las vegas': 'NV', 'st louis': 'MO', 'kansas city': 'MO',
  charlotte: 'NC', raleigh: 'NC', nashville: 'TN', memphis: 'TN', columbus: 'OH',
  cleveland: 'OH', cincinnati: 'OH', indianapolis: 'IN', milwaukee: 'WI',
  'salt lake city': 'UT', 'new orleans': 'LA', 'oklahoma city': 'OK', albuquerque: 'NM',
}

// Words that carry no filter meaning; whatever else is left over is reported
// back so the user knows what wasn't understood.
const FILLER = new Set([
  'a', 'an', 'the', 'i', 'want', 'looking', 'for', 'find', 'me', 'my', 'us',
  'dog', 'dogs', 'puppy', 'puppies', 'pup', 'in', 'near', 'around', 'nearby',
  'with', 'that', 'is', 'and', 'or', 'to', 'of', 'good',
])

export function parseQuery(text, { breeds = [], usStates = [] } = {}) {
  const result = { breed: '', state: '', city: '', size: '', traits: [], goal: '', nearMe: false, inferredState: '', unmatched: [] }
  if (!text?.trim()) return result

  // 2-letter state abbreviations only when typed uppercase ("MD") — otherwise
  // "in", "me", "or", "hi" false-positive as Indiana/Maine/Oregon/Hawaii.
  for (const token of text.split(/\s+/)) {
    if (/^[A-Z]{2}$/.test(token) && usStates.includes(token)) result.state = token
  }

  let q = ` ${text.toLowerCase().replace(/[^a-z0-9\s]/g, ' ').replace(/\s+/g, ' ').trim()} `

  const consume = (phrase) => {
    const needle = ` ${phrase} `
    if (!q.includes(needle)) return false
    q = q.replace(needle, ' ')
    return true
  }

  // Remove a matched abbreviation from the working text so "in MD" can't
  // also be read as city "Md" below.
  if (result.state) consume(result.state.toLowerCase())

  if (consume('near me') || consume('nearby') || consume('around me')) result.nearMe = true

  // Longest names first so "golden retriever" wins over the "golden" alias.
  const breedEntries = [
    ...breeds.map((b) => [b.displayName.split('(')[0].trim().toLowerCase(), b.slug]),
    ...Object.entries(BREED_ALIASES),
  ].sort((a, b) => b[0].length - a[0].length)
  for (const [name, slug] of breedEntries) {
    if (consume(name)) {
      result.breed = slug
      break
    }
  }

  for (const [name, abbrev] of Object.entries(STATE_NAMES).sort((a, b) => b[0].length - a[0].length)) {
    if (consume(name)) {
      result.state = abbrev
      break
    }
  }

  for (const [words, value] of SIZE_WORDS) {
    if (words.some(consume)) {
      result.size = value
      break
    }
  }

  for (const [words, trait] of TRAIT_WORDS) {
    if (words.some(consume)) result.traits.push(trait)
  }

  for (const [words, goal] of GOAL_WORDS) {
    if (words.some(consume)) {
      result.goal = goal
      break
    }
  }

  // "in <something>" / "near <something>" that survived everything above is a city.
  const cityMatch = q.match(/ (?:in|near|around) ((?:[a-z]+ ?){1,2}?)(?= in | near | around | $)/)
  if (cityMatch) {
    const city = cityMatch[1].trim()
    if (city && !FILLER.has(city)) {
      result.city = city.replace(/\b[a-z]/g, (c) => c.toUpperCase())
      q = q.replace(` ${cityMatch[1]}`, ' ')
      // A known metro implies its state, so "near seattle" filters immediately.
      if (!result.state && CITY_STATES[city] && usStates.includes(CITY_STATES[city])) {
        result.state = CITY_STATES[city]
        result.inferredState = CITY_STATES[city]
      }
    }
  }

  result.unmatched = q.split(/\s+/).filter((t) => t && !FILLER.has(t) && t.toUpperCase() !== result.state)
  return result
}
