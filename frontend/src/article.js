/**
 * "a" or "an" for a breed name.
 *
 * The copy hardcoded "a", which reads fine for most of the catalogue and wrong for the ones
 * that matter: "What a Afghan Hound actually costs", "our range for a Afghan Hound". With 175
 * breeds there are enough vowel-initial names — Afghan Hound, Akita, Airedale, English
 * Setter, Irish Wolfhound, Italian Greyhound, English Mastiff — that this shows up on real
 * searches rather than being a curiosity.
 *
 * Vowel *sound*, not vowel letter, is what governs the article, so the exceptions are listed
 * rather than inferred: a "Eurasier" takes "a" (yoo-), and an "Xoloitzcuintli" takes "an"
 * (sho-). Both are in the catalogue.
 */

// Written vowel, spoken consonant → "a".
const CONSONANT_SOUND = [/^eu/i, /^u[bcdfghjklmnpqrstvwxyz]?[aeiou]/i, /^one/i]

// Written consonant, spoken vowel → "an".
const VOWEL_SOUND = [/^xolo/i, /^hour/i]

export function articleFor(name) {
  const word = String(name ?? '').trim()
  if (!word) return 'a'
  if (VOWEL_SOUND.some((re) => re.test(word))) return 'an'
  if (CONSONANT_SOUND.some((re) => re.test(word))) return 'a'
  return /^[aeiou]/i.test(word) ? 'an' : 'a'
}

/** "an Afghan Hound" — the article and the name, for interpolating into a sentence. */
export function withArticle(name) {
  return `${articleFor(name)} ${name}`
}
