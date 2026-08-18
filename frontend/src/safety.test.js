import { describe, expect, it } from 'vitest'
import { parseRoute } from './router.js'
import {
  GUIDE_META,
  PAYMENTS,
  PAYMENT_STYLE,
  SAFETY_SECTIONS,
  findSection,
  safetyPath,
} from './content/safety.js'

describe('parseRoute', () => {
  it('sends everything outside /safe to the search app', () => {
    expect(parseRoute('/')).toEqual({ name: 'app' })
    expect(parseRoute('/index.html')).toEqual({ name: 'app' })
    // The prefix has to be a path segment: /safety is not the guide.
    expect(parseRoute('/safety')).toEqual({ name: 'app' })
    expect(parseRoute('/safeguard/x')).toEqual({ name: 'app' })
  })

  it('routes the guide with no anchor', () => {
    expect(parseRoute('/safe')).toEqual({ name: 'safety', anchor: '' })
    // Prerendered pages are directories, so both forms reach a real user.
    expect(parseRoute('/safe/')).toEqual({ name: 'safety', anchor: '' })
  })

  it('keeps the older per-section URLs working, as an anchor', () => {
    // These were real URLs before the guide became one page; anything already shared has to
    // land in the right place rather than 404.
    expect(parseRoute('/safe/payments')).toEqual({ name: 'safety', anchor: 'payments' })
    expect(parseRoute('/safe/payments/')).toEqual({ name: 'safety', anchor: 'payments' })
  })

  it('ignores an unknown section rather than erroring', () => {
    expect(parseRoute('/safe/nonsense')).toEqual({ name: 'safety', anchor: '' })
  })
})

describe('safety content', () => {
  it('gives every section a unique, URL-safe slug', () => {
    const slugs = SAFETY_SECTIONS.map((s) => s.slug)
    expect(new Set(slugs).size).toBe(slugs.length)
    for (const slug of slugs) expect(slug).toMatch(/^[a-z0-9-]+$/)
  })

  it('gives every section a heading, a lede and content to render', () => {
    for (const section of SAFETY_SECTIONS) {
      expect(section.title).toBeTruthy()
      // The lede under the heading is intro ?? summary, so at least one must exist.
      expect(section.intro ?? section.summary).toBeTruthy()
      // The payments section renders PAYMENTS instead of a bullet list.
      if (section.kind !== 'payments') expect(section.items.length).toBeGreaterThan(0)
    }
  })

  it('resolves a section by slug', () => {
    expect(findSection('payments').title).toBe('What you can actually get back')
    expect(findSection('nonsense')).toBeNull()
  })

  it('points every section at an anchor on the one page', () => {
    expect(safetyPath('')).toBe('/safe')
    expect(safetyPath('payments')).toBe('/safe#payments')
  })

  it('describes the page once, because there is one page', () => {
    expect(GUIDE_META.title).toContain('PuppyFinder')
    expect(GUIDE_META.heading).toBe('Buy & adopt a puppy safely')
    expect(GUIDE_META.description.length).toBeGreaterThan(50)
  })

  it('pairs every payment method with a word, never colour alone', () => {
    for (const pay of PAYMENTS) {
      expect(pay.verdict).toBeTruthy()
      expect(PAYMENT_STYLE[pay.state]).toBeTruthy()
    }
  })
})
