// Path routing, deliberately tiny.
//
// The app has exactly two kinds of page: the search app (whose whole state lives in the query
// string, handled in App.vue) and the safety guide. Those two never share state, so navigation
// between them is a real page load rather than a client-side swap — no router dependency, no
// unmount-time listener leaks, and Back works natively.
//
// Within the search app nothing changed: filters still push query strings onto the same path.

import { findSection } from './content/safety.js'

const SAFE_PREFIX = '/safe'

/**
 * @param {string} pathname e.g. "/safe" or the older "/safe/payments"
 * @returns {{name: 'app'} | {name: 'safety', anchor: string}}
 *
 * The guide is one page. `anchor` is only ever set by a /safe/<slug> URL from when it was
 * eight — those still work, and SafetyPage rewrites them to /safe#<slug> so there stays
 * exactly one canonical URL rather than nine showing the same content.
 */
export function parseRoute(pathname) {
  // Trailing slashes come from prerendered directory-style URLs (/safe/index.html), so /safe
  // and /safe/ have to mean the same page.
  const path = pathname.replace(/\/+$/, '') || '/'
  if (path !== SAFE_PREFIX && !path.startsWith(`${SAFE_PREFIX}/`)) {
    return { name: 'app' }
  }
  const slug = path.slice(SAFE_PREFIX.length + 1)
  // An unknown slug just lands at the top of the guide: every one of these URLs is something a
  // stranger pasted, and the guide answers the question either way.
  return { name: 'safety', anchor: findSection(slug) ? slug : '' }
}
