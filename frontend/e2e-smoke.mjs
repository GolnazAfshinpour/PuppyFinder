// E2E smoke test: drives the running app (vite on 5173 + API on 5133) in headless
// Chromium. Buying is the primary path, so that's what the landing checks cover;
// the adoption path is exercised after switching goals. Run with: npm run test:e2e
import { chromium } from 'playwright'

const browser = await chromium.launch()
const page = await browser.newPage({ viewport: { width: 1440, height: 1100 } })
const apiCalls = []
page.on('request', (r) => {
  const url = r.url()
  if (url.includes('/api/listings') || url.includes('/api/price-check')) {
    apiCalls.push(url.replace('http://localhost:5173', ''))
  }
})
page.on('console', (m) => {
  if (m.type() === 'error') console.log('CONSOLE ERROR:', m.text())
})
page.on('pageerror', (e) => console.log('PAGE ERROR:', e.message))

// Counted off a test hook, not link text: two site cards in the fallback happen
// to contain the word "meet" and used to be counted as dogs.
const cards = () => page.locator('[data-testid="dog-results"] > li')
const settle = () => page.waitForTimeout(2500)
const breedSelect = 'select:below(:text("BREED"))'

// ---------- buying: the default path ----------
await page.goto('http://localhost:5173')
await page.waitForSelector('h1', { timeout: 15000 })
await settle()
const hero = await page.locator('h1').innerText()
const buyDefault = (await page.locator('h2:has-text("Puppies from breeders")').count()) === 1
console.log('landing hero:', JSON.stringify(hero.replace(/\n/g, ' ')))
console.log('buying is the default path:', buyDefault)

// A breed's verified price range is the anchor the whole scam check hangs off.
await page.selectOption(breedSelect, 'french-bulldog')
await settle()
const priceRange = (await page.locator('.text-primary.text-4xl').innerText()).trim()
console.log('breed price range shown:', priceRange)

// The range must label its own reliability, and the hero must not overstate it.
// This is the regression that motivated the whole provenance pipeline: unsourced
// numbers presented as "verified".
const heroBadges = (await page.locator('header .badge').allInnerTexts()).join(' | ')
const provenance = await page.locator('text=/isn\'t sourced yet|independent sources?|disagree materially/').count()
console.log('hero badges:', heroBadges)
console.log('range states its provenance:', provenance > 0)

// The core feature: a far-below-market quote must be flagged as a warning.
await page.fill('input[aria-label*="quoted"]', '800')
await page.click('button:has-text("Check this price")')
await page.waitForTimeout(1200)
const scamAlert = page.locator('[data-testid="price-verdict"]')
const scamText = await scamAlert.innerText()
const scamFlagged = (await scamAlert.getAttribute('class')).includes('alert-error')
console.log('scam quote flagged:', scamFlagged, '|', scamText.split('\n')[0])

// ...and a plausible quote must NOT read as an all-clear.
await page.fill('input[aria-label*="quoted"]', '4000')
await page.click('button:has-text("Check this price")')
await page.waitForTimeout(1200)
const okText = await page.locator('[data-testid="price-verdict"]').innerText()
const typicalHonest = okText.includes('not a safety check')
console.log('typical quote stays honest:', typicalHonest)

// Changing breed must invalidate a stale verdict rather than mislabel it.
await page.selectOption(breedSelect, 'beagle')
await settle()
const verdictCleared = (await page.locator('text=not a safety check').count()) === 0
console.log('verdict clears when breed changes:', verdictCleared)

// ---------- adopting: the secondary path ----------
await page.click('button:has-text("Adopt a rescue dog")')
await settle()
const countAll = await cards().count()
console.log('adopt mode, breed=beagle:', countAll, 'cards')

await page.selectOption(breedSelect, '')
await settle()
const countAny = await cards().count()
console.log('any breed:', countAny, 'cards')

await page.click('button:has-text("Puppy")')
await settle()
const countPuppies = await cards().count()
console.log('age=Puppy:', countPuppies, 'cards')
await page.click('button:has-text("Any age")')
await settle()

await page.selectOption('select:below(:text("State"))', 'MD')
await settle()
const countMd = await cards().count()
console.log('state=MD:', countMd, 'cards')

await page.selectOption('select:has(option[value="youngest"])', 'youngest')
await settle()
const countSorted = await cards().count()
console.log('sort=youngest:', countSorted, 'cards')

// ---------- the in-app dog detail view ----------
await page.click('[data-testid="dog-results"] > li a:has-text("Meet")')
await page.waitForTimeout(900)
const detailOpen = await page.locator('[role="dialog"]').count()
const detailAddressable = new URL(page.url()).searchParams.has('dog')
const sharedId = new URL(page.url()).searchParams.get('dog')
console.log('detail opens in-app:', detailOpen === 1, '| addressable:', detailAddressable)

await page.keyboard.press('Escape')
await page.waitForTimeout(500)
const detailClosed = (await page.locator('[role="dialog"]').count()) === 0
  && !new URL(page.url()).searchParams.has('dog')
console.log('Escape closes it and clears the param:', detailClosed)

await page.goto(`http://localhost:5173/?dog=${sharedId}`)
await page.waitForTimeout(3000)
const sharedResolves = (await page.locator('#dog-detail-name').count()) === 1
console.log('shared ?dog= link resolves:', sharedResolves)

await page.goto('http://localhost:5173/?dog=montgomery-county-animal-services-a000000')
await page.waitForTimeout(2500)
const goneHandled = (await page.locator('text=no longer listed').count()) === 1
console.log('adopted dog handled gracefully:', goneHandled)

console.log('API calls observed:', JSON.stringify(apiCalls, null, 1))
await page.screenshot({ path: (process.env.SCRATCH ?? '.') + '/adopt-tab.png', fullPage: false })
await browser.close()

const checks = {
  'buying is the default path': buyDefault,
  'hero leads on buying': /buy/i.test(hero),
  'breed price range is shown': /^\$[\d,]+–\$[\d,]+$/.test(priceRange),
  'range states its own provenance': provenance > 0,
  // "verified" may only appear once the data actually says so; today it does not.
  'hero does not claim unearned verification': !/verified/i.test(heroBadges),
  'below-market quote is flagged': scamFlagged && /below the typical/.test(scamText),
  'typical quote is not an all-clear': typicalHonest,
  'stale verdict clears on breed change': verdictCleared,
  'adopt mode lists dogs': countAny > 0,
  'breed filter narrows adoption results': countAll < countAny,
  'puppy filter narrows the list': countPuppies > 0 && countPuppies < countAny,
  'state filter narrows the list': countMd < countAny,
  'sorting keeps the same dogs': countSorted === countMd,
  'detail view opens in-app': detailOpen === 1 && detailAddressable,
  'detail view closes on Escape': detailClosed,
  'shared dog link resolves': sharedResolves,
  'adopted dog handled gracefully': goneHandled,
}
for (const [name, ok] of Object.entries(checks)) console.log(ok ? `PASS  ${name}` : `FAIL  ${name}`)
process.exit(Object.values(checks).every(Boolean) ? 0 : 1)
