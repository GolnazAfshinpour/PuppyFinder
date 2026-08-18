// "Good with kids / dogs / cats", turned into something honest to render.
//
// Three states per field, never two. RescueGroups omits null attributes from its response
// entirely, so a missing value means the rescue didn't record it — and showing that as
// "not good with cats" would rule a dog out over a blank field, which is the exact mistake
// the size and age filters were fixed for. Live coverage is 41% / 25% / 21%, so the unknown
// case is the common one and has to read as a prompt to ask rather than as an answer.

const FIELDS = [
  ['kids', 'goodWithKids'],
  ['dogs', 'goodWithDogs'],
  ['cats', 'goodWithCats'],
]

/** @returns {{yes: string[], no: string[], unknown: string[], known: boolean}} */
export function goodWith(listing) {
  const yes = []
  const no = []
  const unknown = []
  for (const [label, key] of FIELDS) {
    const value = listing?.[key]
    if (value === true) yes.push(label)
    else if (value === false) no.push(label)
    else unknown.push(label)
  }
  return { yes, no, unknown, known: yes.length + no.length > 0 }
}

/** "kids", "kids and dogs", "kids, dogs and cats" */
export function joinList(items) {
  if (items.length === 0) return ''
  if (items.length === 1) return items[0]
  return `${items.slice(0, -1).join(', ')} and ${items[items.length - 1]}`
}

/**
 * The card's one-line version, or '' when the rescue recorded nothing. Deliberately silent in
 * that case: a card that says "not recorded" on three quarters of the grid is noise, and the
 * detail view is where the prompt to ask belongs.
 */
export function goodWithLine(listing) {
  const { yes, no } = goodWith(listing)
  const parts = []
  if (yes.length) parts.push(`Good with ${joinList(yes)}`)
  // Stated plainly rather than omitted — someone with a cat needs the negative most of all.
  if (no.length) parts.push(`not with ${joinList(no)}`)
  return parts.join(' · ')
}

/** Badges for the detail view: the word carries the meaning, colour only supports it. */
export function goodWithBadges(listing) {
  const { yes, no } = goodWith(listing)
  return [
    ...yes.map((what) => ({ text: `Good with ${what}`, tone: 'badge-soft badge-success' })),
    // Not an alarm — it is an ordinary fact about a dog, and error colouring would read as one.
    ...no.map((what) => ({ text: `Not good with ${what}`, tone: 'badge-soft' })),
  ]
}
