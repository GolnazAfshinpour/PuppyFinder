// SSR entry, used only by scripts/prerender.mjs at build time.
//
// The point of giving these pages real URLs is being findable, and a URL that returns an
// empty <div id="app"> to a crawler is not findable. This renders the same components the
// browser renders, so the static HTML and the SPA cannot describe a page differently.

import { createSSRApp, h } from 'vue'
import { renderToString } from 'vue/server-renderer'
import ArticlePage from './components/ArticlePage.vue'
import EmbedPage from './components/EmbedPage.vue'
import SafetyPage from './components/SafetyPage.vue'
import { ARTICLES, EMBED_META, articlePath } from './content/articles.js'
import { GUIDE_META } from './content/safety.js'

export async function renderAll() {
  const pages = []

  // The guide is one page. Section anchors need no page of their own — the ids are in this
  // markup, so /safe#payments lands in the right place in the prerendered HTML too.
  pages.push({
    path: '/safe',
    html: await renderToString(createSSRApp({ render: () => h(SafetyPage) })),
    meta: GUIDE_META,
  })

  // The scam-guide articles: each one exists to rank for a search no interactive tool
  // currently answers, which only works if a crawler sees the text.
  for (const article of ARTICLES) {
    pages.push({
      path: articlePath(article.slug),
      html: await renderToString(
        createSSRApp({ render: () => h(ArticlePage, { slug: article.slug }) })),
      meta: article.meta,
    })
  }

  // The widget pitch page for rescues. The widget itself (/widget/fee-check) is deliberately
  // NOT prerendered or in the sitemap: it is an iframe fragment whose canonical content lives
  // on /puppy-shipping-fee-scam, and it marks itself noindex at runtime.
  pages.push({
    path: '/embed',
    html: await renderToString(createSSRApp({ render: () => h(EmbedPage) })),
    meta: EMBED_META,
  })

  return pages
}
