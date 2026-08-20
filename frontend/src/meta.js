// Document metadata for client-rendered pages. ArticlePage/SafetyPage/EmbedPage each carry a
// private copy of setMeta from before this file existed — new pages use this one, and the
// copies can fold into it when those pages are next touched.

/** Create-or-update a <meta> tag, e.g. setMeta('property', 'og:title', '...'). */
export function setMeta(attr, key, content) {
  let tag = document.head.querySelector(`meta[${attr}="${key}"]`)
  if (!tag) {
    tag = document.createElement('meta')
    tag.setAttribute(attr, key)
    document.head.appendChild(tag)
  }
  tag.setAttribute('content', content)
}
