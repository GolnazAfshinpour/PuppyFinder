// E2E smoke test: drives the running app (vite on 5173 + API on 5133) in headless
// Chromium and checks that the search filters actually change the adoptable
// listings. Run with: npm run test:e2e
import { chromium } from 'playwright'

const browser = await chromium.launch()
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } })
const apiCalls = []
page.on('request', (r) => {
  if (r.url().includes('/api/listings')) apiCalls.push(r.url().replace('http://localhost:5173', ''))
})
page.on('console', (m) => {
  if (m.type() === 'error') console.log('CONSOLE ERROR:', m.text())
})
page.on('pageerror', (e) => console.log('PAGE ERROR:', e.message))

// Counted off a test hook, not link text: two site cards in the fallback happen
// to contain the word "meet" and used to be counted as dogs.
const cards = () => page.locator('[data-testid="dog-results"] > li')
const settle = () => page.waitForTimeout(2500)

// Dogs are the landing page now — there's no tab to click first.
await page.goto('http://localhost:5173')
await page.waitForSelector('text=adoptable', { timeout: 15000 })
await settle()
const countAll = await cards().count()
console.log('landing, no filters:', countAll, 'cards')

// Age is the filter this product is named after.
await page.click('.badge:has-text("Puppies only")')
await settle()
const countPuppies = await cards().count()
console.log('age=Puppy:', countPuppies, 'cards')
await page.click('.badge:has-text("Puppies only")') // back off
await settle()

await page.selectOption('select:below(:text("State"))', 'WA')
await settle()
const countWa = await cards().count()
console.log('state=WA:', countWa, 'cards')

await page.selectOption('select:below(:text("State"))', 'MD')
await settle()
const countMd = await cards().count()
console.log('state=MD:', countMd, 'cards')

// Sorting reorders without dropping anyone.
// Targeted by its options: ":below(text=Sort)" also matches the sidebar selects.
await page.selectOption('select:has(option[value="youngest"])', 'youngest')
await settle()
const countSorted = await cards().count()
console.log('sort=youngest:', countSorted, 'cards')

// The site directory is now the fallback below the dogs, not the front door.
const fallback = await page.locator('summary:has-text("Compare all")').count()
console.log('site directory present as fallback:', fallback === 1)

// Buying switches the page over to the vetted-marketplace guide, because we have
// no breeder listings of our own to show.
await page.click('button:has-text("Buy from a breeder")')
await settle()
const breederHeading = await page.locator('h2:has-text("Puppies from breeders")').count()
console.log('goal=buy shows the breeder guide:', breederHeading === 1)

// Dogs open in an in-app detail view instead of ejecting to petharbor.com, and
// that view is addressable so a single dog can be shared.
await page.goto('http://localhost:5173')
await page.waitForTimeout(3000)
await page.click('[data-testid="dog-results"] > li a:has-text("Meet")')
await page.waitForTimeout(800)
const detailOpen = await page.locator('[role="dialog"]').count()
const detailAddressable = page.url().includes('?dog=')
const sharedId = new URL(page.url()).searchParams.get('dog')
console.log('detail opens in-app:', detailOpen === 1, '| addressable:', detailAddressable)

await page.keyboard.press('Escape')
await page.waitForTimeout(500)
const detailClosed = (await page.locator('[role="dialog"]').count()) === 0 && !page.url().includes('?dog=')
console.log('Escape closes it and clears the param:', detailClosed)

// A shared link carries no filters, so this also proves the fetch-by-id path.
await page.goto(`http://localhost:5173/?dog=${sharedId}`)
await page.waitForTimeout(3000)
const sharedResolves = (await page.locator('#dog-detail-name').count()) === 1
console.log('shared ?dog= link resolves:', sharedResolves)

// A dog that has since been adopted is a happy ending, not an error page.
await page.goto('http://localhost:5173/?dog=montgomery-county-animal-services-a000000')
await page.waitForTimeout(2500)
const goneHandled = (await page.locator('text=no longer listed').count()) === 1
console.log('adopted dog handled gracefully:', goneHandled)

console.log('API calls observed:', JSON.stringify(apiCalls, null, 1))
await page.screenshot({ path: (process.env.SCRATCH ?? '.') + '/adopt-tab.png', fullPage: false })
await browser.close()

const checks = {
  'landing shows dogs, not websites': countAll > 0,
  'puppy filter narrows the list': countPuppies > 0 && countPuppies < countAll,
  'state filters narrow the list': countWa < countAll && countMd < countAll,
  'sorting keeps the same dogs': countSorted === countMd,
  'directory demoted to fallback': fallback === 1,
  'buy mode shows the breeder guide': breederHeading === 1,
  'detail view opens in-app': detailOpen === 1 && detailAddressable,
  'detail view closes on Escape': detailClosed,
  'shared dog link resolves': sharedResolves,
  'adopted dog handled gracefully': goneHandled,
}
for (const [name, ok] of Object.entries(checks)) console.log(ok ? `PASS  ${name}` : `FAIL  ${name}`)
process.exit(Object.values(checks).every(Boolean) ? 0 : 1)
