import { describe, expect, it } from 'vitest'
import { ARTICLES, EMBED_META, articlePath, findArticle } from './content/articles.js'
import { SAFETY_SECTIONS } from './content/safety.js'
import { parseRoute } from './router.js'

// Structural invariants on the scam-guide pages, in the same spirit as safety.test.js:
// the content is data and the renderer trusts it, so the shape has to be pinned here —
// a typo'd block kind renders as silent nothing, not an error.

const BLOCK_KINDS = new Set(['h2', 'p', 'list', 'callout', 'tool'])
const TOOLS = new Set(['price-check', 'fee-check', 'seller-check'])
// Top-level slugs share a namespace with the app's real paths, so a collision would
// shadow a page.
const RESERVED = new Set(['safe', 'embed', 'widget', 'api', 'assets'])

describe('article content', () => {
  it('has unique, URL-safe, unreserved slugs', () => {
    const slugs = ARTICLES.map((a) => a.slug)
    expect(new Set(slugs).size).toBe(slugs.length)
    for (const slug of slugs) {
      expect(slug).toMatch(/^[a-z0-9-]+$/)
      expect(RESERVED.has(slug)).toBe(false)
    }
  })

  it('keeps titles and descriptions inside search-snippet lengths', () => {
    for (const meta of [...ARTICLES.map((a) => a.meta), EMBED_META]) {
      // Google truncates around 60 chars of title and 160 of description; a truncated
      // pitch is a wasted one. Small headroom on titles for brand-suffix-free display.
      expect(meta.title.length, meta.title).toBeLessThanOrEqual(70)
      expect(meta.description.length, meta.title).toBeLessThanOrEqual(165)
      expect(meta.description.length, meta.title).toBeGreaterThanOrEqual(70)
    }
  })

  it('uses only block kinds and tools the renderer knows', () => {
    for (const article of ARTICLES) {
      expect(article.blocks.length).toBeGreaterThanOrEqual(5)
      for (const block of article.blocks) {
        expect(BLOCK_KINDS.has(block.kind), `${article.slug}: ${block.kind}`).toBe(true)
        if (block.kind === 'tool') {
          expect(TOOLS.has(block.tool), `${article.slug}: ${block.tool}`).toBe(true)
          expect(block.lead?.length ?? 0).toBeGreaterThan(0)
        }
      }
      // Every page carries at least one interactive check — the tool is the part a search
      // answer box can't replace, and the whole reason these pages exist.
      expect(article.blocks.some((b) => b.kind === 'tool'), article.slug).toBe(true)
    }
  })

  it('cites every article to at least two named https sources', () => {
    for (const article of ARTICLES) {
      expect(article.sources.length, article.slug).toBeGreaterThanOrEqual(2)
      for (const source of article.sources) {
        expect(source.url, article.slug).toMatch(/^https:\/\//)
        expect(source.name.length, article.slug).toBeGreaterThan(5)
      }
    }
  })

  it('only links related articles and guide sections that exist', () => {
    const sections = new Set(SAFETY_SECTIONS.map((s) => s.slug))
    for (const article of ARTICLES) {
      for (const slug of article.related) {
        expect(findArticle(slug), `${article.slug} -> ${slug}`).not.toBeNull()
        expect(slug).not.toBe(article.slug)
      }
      for (const anchor of article.safeAnchors) {
        expect(sections.has(anchor), `${article.slug} -> #${anchor}`).toBe(true)
      }
    }
  })
})

describe('routing', () => {
  it('routes every article slug, with and without a trailing slash', () => {
    for (const article of ARTICLES) {
      expect(parseRoute(articlePath(article.slug)))
        .toEqual({ name: 'article', slug: article.slug })
      expect(parseRoute(`${articlePath(article.slug)}/`))
        .toEqual({ name: 'article', slug: article.slug })
    }
  })

  it('routes the widget and its pitch page', () => {
    expect(parseRoute('/widget/fee-check')).toEqual({ name: 'widget' })
    expect(parseRoute('/embed')).toEqual({ name: 'embed' })
  })

  it('still falls through to the app for everything unknown', () => {
    expect(parseRoute('/')).toEqual({ name: 'app' })
    expect(parseRoute('/no-such-page')).toEqual({ name: 'app' })
    expect(parseRoute('/widget/no-such-widget')).toEqual({ name: 'app' })
  })

  it('leaves the safety guide routing untouched', () => {
    expect(parseRoute('/safe')).toEqual({ name: 'safety', anchor: '' })
    expect(parseRoute('/safe/payments')).toEqual({ name: 'safety', anchor: 'payments' })
  })
})
