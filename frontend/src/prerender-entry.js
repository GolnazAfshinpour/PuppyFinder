// SSR entry, used only by scripts/prerender.mjs at build time.
//
// The point of giving the guide a real URL is being findable, and a URL that returns an empty
// <div id="app"> to a crawler is not findable. This renders the same component the browser
// renders, so the static HTML and the SPA cannot describe the page differently.

import { createSSRApp, h } from 'vue'
import { renderToString } from 'vue/server-renderer'
import SafetyPage from './components/SafetyPage.vue'
import { GUIDE_META } from './content/safety.js'

export async function renderAll() {
  // One page. Section anchors need no page of their own — the ids are in this markup, so
  // /safe#payments lands in the right place in the prerendered HTML too.
  const app = createSSRApp({ render: () => h(SafetyPage) })
  return [{ path: '/safe', html: await renderToString(app), meta: GUIDE_META }]
}
