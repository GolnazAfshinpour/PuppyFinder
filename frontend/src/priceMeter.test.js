import { describe, expect, it } from 'vitest'
import { FAR_BELOW_FACTOR, markerPosition, meterZones, verdictRole } from './priceMeter.js'

// French Bulldog's real band at the time of writing.
const LOW = 1700
const HIGH = 3200

describe('meterZones', () => {
  it('places the band inside the track with headroom above it', () => {
    const z = meterZones(LOW, HIGH)

    expect(z.bandStart).toBeGreaterThan(0)
    expect(z.bandEnd).toBeGreaterThan(z.bandStart)
    // Headroom exists, or "above typical" would have nowhere to be drawn.
    expect(z.bandEnd).toBeLessThan(100)
  })

  it('puts the scam edge at half the band low, matching the backend rule', () => {
    const z = meterZones(LOW, HIGH)

    // The 0.5x rule becomes visible geometry rather than living only in prose.
    expect(z.scamEnd).toBeCloseTo(z.bandStart * FAR_BELOW_FACTOR, 5)
  })

  it('is proportional to dollars, so distance is not misleading', () => {
    const z = meterZones(1000, 2000)

    expect(z.bandStart).toBeCloseTo((1000 / 3000) * 100, 5)
    expect(z.bandEnd).toBeCloseTo((2000 / 3000) * 100, 5)
  })

  it('refuses a band it cannot draw rather than rendering nonsense', () => {
    expect(meterZones(0, 3200)).toBeNull()
    expect(meterZones(3200, 1700)).toBeNull() // inverted
    expect(meterZones(1700, 1700)).toBeNull() // zero width
    expect(meterZones(null, undefined)).toBeNull()
  })
})

describe('markerPosition', () => {
  it('puts a typical quote inside the band', () => {
    const z = meterZones(LOW, HIGH)
    const m = markerPosition(2400, LOW, HIGH)

    expect(m.percent).toBeGreaterThan(z.bandStart)
    expect(m.percent).toBeLessThan(z.bandEnd)
    expect(m.offScale).toBe(false)
  })

  it('puts a scam-priced quote left of the scam edge', () => {
    const z = meterZones(LOW, HIGH)

    expect(markerPosition(800, LOW, HIGH).percent).toBeLessThan(z.scamEnd)
  })

  it('puts a merely-under quote between the scam edge and the band', () => {
    const z = meterZones(LOW, HIGH)
    const m = markerPosition(1500, LOW, HIGH)

    expect(m.percent).toBeGreaterThan(z.scamEnd)
    expect(m.percent).toBeLessThan(z.bandStart)
  })

  it('puts an expensive quote right of the band', () => {
    const z = meterZones(LOW, HIGH)

    expect(markerPosition(4000, LOW, HIGH).percent).toBeGreaterThan(z.bandEnd)
  })

  it('clamps an extreme quote and says that it clamped', () => {
    // $50,000 against a $400-$999 band. Stretching the domain to fit would squash the band
    // into a sliver and destroy the thing the reader came to see.
    const m = markerPosition(50_000, 400, 999)

    expect(m.percent).toBe(100)
    expect(m.offScale).toBe(true)
  })

  it('puts a free dog at the very start', () => {
    expect(markerPosition(0, LOW, HIGH).percent).toBe(0)
  })

  it('returns nothing when there is no quote or no band', () => {
    expect(markerPosition(NaN, LOW, HIGH)).toBeNull()
    expect(markerPosition(-100, LOW, HIGH)).toBeNull()
    expect(markerPosition(2000, 0, 0)).toBeNull()
  })
})

describe('verdictRole', () => {
  it('maps every verdict level the API can return', () => {
    expect(verdictRole('Free')).toBe('critical')
    expect(verdictRole('FarBelow')).toBe('critical')
    expect(verdictRole('Below')).toBe('warning')
    expect(verdictRole('Typical')).toBe('good')
    expect(verdictRole('Above')).toBe('serious')
  })

  it('falls back to neutral for Unknown and Unavailable', () => {
    // Both are real API responses — 116 breeds have no range at all.
    expect(verdictRole('Unknown')).toBe('neutral')
    expect(verdictRole('Unavailable')).toBe('neutral')
    expect(verdictRole(undefined)).toBe('neutral')
  })
})
