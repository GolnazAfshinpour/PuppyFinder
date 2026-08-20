// Path routing, deliberately tiny.
//
// The page kinds never share state, so navigation between them is a real page load rather
// than a client-side swap — no router dependency, no unmount-time listener leaks, and Back
// works natively. Within the search app nothing changed: filters still push query strings
// onto the same path.

import { findArticle } from './content/articles.js'
import { findSection } from './content/safety.js'

const SAFE_PREFIX = '/safe'

/**
 * @param {string} pathname e.g. "/safe", "/teacup-puppy-scam", "/widget/fee-check"
 * @returns {{name: 'app'} | {name: 'safety', anchor: string} | {name: 'article', slug: string}
 *   | {name: 'widget'} | {name: 'embed'}}
 *
 * The guide is one page. `anchor` is only ever set by a /safe/<slug> URL from when it was
 * eight — those still work, and SafetyPage rewrites them to /safe#<slug> so there stays
 * exactly one canonical URL rather than nine showing the same content.
 *
 * The scam-guide articles are top-level slugs (/teacup-puppy-scam), because the URL is the
 * search snippet's first impression and "/articles/" would be a word the reader didn't
 * search for. An unknown path still falls through to the app rather than a 404 — every one
 * of these URLs is something a stranger pasted.
 */
export function parseRoute(pathname) {
  // Trailing slashes come from prerendered directory-style URLs (/safe/index.html), so /safe
  // and /safe/ have to mean the same page.
  const path = pathname.replace(/\/+$/, '') || '/'

  if (path === SAFE_PREFIX || path.startsWith(`${SAFE_PREFIX}/`)) {
    const slug = path.slice(SAFE_PREFIX.length + 1)
    return { name: 'safety', anchor: findSection(slug) ? slug : '' }
  }

  if (path === '/widget/fee-check') return { name: 'widget' }
  if (path === '/embed') return { name: 'embed' }

  const article = findArticle(path.slice(1))
  if (article) return { name: 'article', slug: article.slug }

  return { name: 'app' }
}
