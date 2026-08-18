// Writes the safety guide's pages into dist/ as real HTML.
//
// Runs after `vite build` (client) and `vite build --ssr` (this entry). Each page is the
// built index.html with the rendered markup injected into #app and its own <title>,
// description and canonical in <head>. The SPA still boots on top and renders the identical
// component, so JavaScript-off readers and crawlers get the text and everyone else gets the
// app — no runtime SSR server to operate.
//
// SITE_URL is optional. Without it, canonical tags and the sitemap are skipped rather than
// guessed: a canonical pointing at the wrong origin actively hurts, and the app's rule is to
// publish nothing it cannot attribute.

import { mkdir, readFile, writeFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = dirname(dirname(fileURLToPath(import.meta.url)))
const dist = join(root, 'dist')
const siteUrl = (process.env.SITE_URL ?? '').replace(/\/+$/, '')

const { renderAll } = await import(join(root, '.prerender', 'prerender-entry.js'))
const template = await readFile(join(dist, 'index.html'), 'utf8')
const pages = await renderAll()

for (const page of pages) {
  const head = [
    `<title>${escapeHtml(page.meta.title)}</title>`,
    `<meta name="description" content="${escapeHtml(page.meta.description)}" />`,
    `<meta property="og:title" content="${escapeHtml(page.meta.title)}" />`,
    `<meta property="og:description" content="${escapeHtml(page.meta.description)}" />`,
    `<meta property="og:type" content="article" />`,
    siteUrl ? `<link rel="canonical" href="${siteUrl}${page.path}" />` : '',
  ].filter(Boolean).join('\n    ')

  const html = template
    // The template's own <title> would otherwise sit alongside the page's.
    .replace(/<title>.*?<\/title>/s, head)
    .replace('<div id="app"></div>', `<div id="app">${page.html}</div>`)

  const dir = join(dist, page.path.replace(/^\//, ''))
  await mkdir(dir, { recursive: true })
  await writeFile(join(dir, 'index.html'), html)
  console.log(`prerendered ${page.path}`)
}

const robots = ['User-agent: *', 'Allow: /']
if (siteUrl) {
  const urls = pages
    .map((p) => `  <url><loc>${siteUrl}${p.path}</loc></url>`)
    .join('\n')
  await writeFile(
    join(dist, 'sitemap.xml'),
    `<?xml version="1.0" encoding="UTF-8"?>\n`
    + `<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n`
    + `  <url><loc>${siteUrl}/</loc></url>\n${urls}\n</urlset>\n`,
  )
  robots.push(`Sitemap: ${siteUrl}/sitemap.xml`)
  console.log('wrote sitemap.xml')
} else {
  console.log('SITE_URL not set — skipped sitemap.xml and canonical tags')
}
await writeFile(join(dist, 'robots.txt'), `${robots.join('\n')}\n`)

function escapeHtml(value) {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}
