// E2E smoke test: drives the running app (vite on 5173 + API on 5133) in headless
// Chromium and checks that the search filters actually change the adoptable
// listings. Run with: npm run test:e2e
import { chromium } from 'playwright'

const browser = await chromium.launch()
const page = await browser.newPage()
const apiCalls = []
page.on('request', (r) => {
  if (r.url().includes('/api/listings')) apiCalls.push(r.url().replace('http://localhost:5173', ''))
})
page.on('console', (m) => {
  if (m.type() === 'error') console.log('CONSOLE ERROR:', m.text())
})
page.on('pageerror', (e) => console.log('PAGE ERROR:', e.message))

await page.goto('http://localhost:5173')
await page.waitForSelector('text=Your matching sites', { timeout: 15000 })

// Open the Adoptable now tab
await page.click('button:has-text("Adoptable now")')
await page.waitForTimeout(2500)
const countAll = await page.locator('main ul li:has-text("Meet")').count()
console.log('adopt tab, no filters:', countAll, 'cards')

// Pick state WA
await page.selectOption('select:below(:text("State"))', 'WA')
await page.waitForTimeout(2500)
const countWa = await page.locator('main ul li:has-text("Meet")').count()
console.log('state=WA:', countWa, 'cards')

// Pick state MD
await page.selectOption('select:below(:text("State"))', 'MD')
await page.waitForTimeout(2500)
const countMd = await page.locator('main ul li:has-text("Meet")').count()
console.log('state=MD:', countMd, 'cards')

// Size filter
await page.click('button:has-text("Small")')
await page.waitForTimeout(2500)
const countMdSmall = await page.locator('main ul li:has-text("Meet")').count()
console.log('state=MD size=Small:', countMdSmall, 'cards')

console.log('API calls observed:', JSON.stringify(apiCalls, null, 1))
await page.screenshot({ path: (process.env.SCRATCH ?? '.') + '/adopt-tab.png', fullPage: false })
await browser.close()

const pass = countWa < countAll && countMd < countAll && countMdSmall < countMd
console.log(pass ? 'PASS: filters change the listings' : 'FAIL: filters had no effect')
process.exit(pass ? 0 : 1)
