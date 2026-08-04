/**
 * Geometry for the price band meter.
 *
 * Separated from the component so the maths is unit-testable without mounting Vue — this is
 * the part that can be quietly wrong (a marker a few percent off reads as a different
 * verdict) and the part a screenshot won't catch.
 *
 * Scale is linear and anchored at $0, so distance along the track is proportional to dollars.
 * A log scale would compress the cheap end, which is exactly where the scam signal lives.
 */

/** Matches PriceCheck.FarBelowFactor on the backend — below this multiple of the low end,
 *  a quote is "far below" rather than merely under market. Mirrored rather than fetched
 *  because it only positions a zone edge; the verdict itself always comes from the API. */
export const FAR_BELOW_FACTOR = 0.5

/** How much headroom above the band the track shows, so "above typical" has somewhere to sit. */
const HEADROOM = 1.5

/**
 * @param {number} low  band low (the 25th percentile, or median-of-lows)
 * @param {number} high band high
 * @returns {{domainMax:number, scamEnd:number, bandStart:number, bandEnd:number}} percentages
 */
export function meterZones(low, high) {
  if (!(low > 0) || !(high > low)) return null

  const domainMax = high * HEADROOM
  const pct = (value) => (value / domainMax) * 100

  return {
    domainMax,
    scamEnd: pct(low * FAR_BELOW_FACTOR),
    bandStart: pct(low),
    bandEnd: pct(high),
  }
}

/**
 * Where to draw the quoted price, and whether it ran off the end.
 *
 * An extreme quote is clamped to the track rather than allowed to stretch the domain: a
 * $50,000 quote against a $400–$999 band would otherwise squash the band into a sliver and
 * destroy the thing the reader came to see. The clamp is disclosed via `offScale` so the
 * component can show it ran past the end instead of implying it sits exactly at the edge.
 */
export function markerPosition(quote, low, high) {
  const zones = meterZones(low, high)
  if (!zones || !Number.isFinite(quote) || quote < 0) return null

  const raw = (quote / zones.domainMax) * 100
  return {
    percent: Math.min(Math.max(raw, 0), 100),
    offScale: raw > 100,
  }
}

/**
 * Status role for a verdict level, in the app's own token names.
 *
 * Deliberately not a colour: the caller maps these to theme tokens, so the meter cannot
 * hardcode a hex that drifts from the theme. Only ever one of these is on screen at a time,
 * which is why status hues never sit adjacent and CVD adjacency doesn't arise here.
 */
export function verdictRole(level) {
  switch (level) {
    case 'Free':
    case 'FarBelow':
      return 'critical'
    case 'Below':
      return 'warning'
    case 'Typical':
      return 'good'
    case 'Above':
      return 'serious'
    default:
      return 'neutral'
  }
}
