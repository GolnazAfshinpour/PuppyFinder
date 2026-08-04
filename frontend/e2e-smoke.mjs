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

// Pick the sourced/unsourced example breeds from live data rather than naming them.
// Hardcoding french-bulldog as "the unsourced one" broke four checks the moment its range
// got sourced — the assertions were right and only the fixture was stale, which is the
// most annoying kind of failure. Both states are permanent properties of the system, so
// the test should find a breed in each rather than assume which breed that is.
const examples = await page.evaluate(async () => {
  const breeds = await (await fetch('/api/breeds')).json()
  const priced = breeds.filter((b) => b.priceLow)
  return {
    sourced: priced.find((b) => b.confidence === 'verified')?.slug ?? null,
    unsourced: priced.find((b) => b.confidence === 'unverified')?.slug ?? null,
  }
})
if (!examples.sourced || !examples.unsourced) {
  throw new Error(
    `need one sourced and one unsourced priced breed to test both gate states, got ${JSON.stringify(examples)}`,
  )
}
console.log('example breeds — sourced:', examples.sourced, '| unsourced:', examples.unsourced)

const hero = await page.locator('h1').innerText()
const buyDefault = (await page.locator('h2:has-text("Puppies from breeders")').count()) === 1
console.log('landing hero:', JSON.stringify(hero.replace(/\n/g, ' ')))
console.log('buying is the default path:', buyDefault)

// Price screening is gated on sourced data (owner decision, July 2026). With
// nothing verified in the DB, the checker and the headline range must both be
// absent — and the hero must not promise a check it doesn't offer.
await page.selectOption(breedSelect, examples.unsourced)
await settle()
const heroSub = await page.locator('header p').first().innerText()
const checkerPresent = await page.locator('text=Been quoted a price').count()
const rangeShown = await page.locator('.text-primary.text-4xl').count()
const priceFreeAdvice = await page.locator('text=What to check before you send money').count()
console.log('checker offered:', checkerPresent, '| range shown:', rangeShown, '| price-free advice:', priceFreeAdvice)

// The API must refuse too, not just the UI — a direct call can't produce a verdict.
const gated = await page.evaluate(async (slug) => {
  const res = await fetch(`/api/price-check?breed=${slug}&price=800`)
  return res.json()
}, examples.unsourced)
console.log('api verdict level:', gated.level, '| isWarning:', gated.isWarning)

// ...and the other half of the gate, which nothing covered until a breed actually reached
// verified: for a sourced breed the checker, the range and a real verdict must all appear.
// Asserting only the hidden case would keep passing if the feature never switched on for
// anyone.
await page.selectOption(breedSelect, examples.sourced)
await settle()
const sourcedChecker = await page.locator('text=Been quoted a price').count()
const sourcedRange = await page.locator('.text-primary.text-4xl').count()
// The provenance sentence differs by basis, and deliberately so — "49 sources" means 49
// articles for an editorial range and 49 puppies for sale for a listing one. Accept either
// wording, but require one of them: a range that can't say where it came from is the fault
// this whole feature exists to fix.
const sourcedProvenance = (await page.locator('text=independent source').count())
  + (await page.locator('text=listed for sale').count())
console.log('sourced breed — checker:', sourcedChecker, '| range:', sourcedRange,
  '| provenance lines:', sourcedProvenance)

// Quote a tenth of the band's floor, so the verdict is FarBelow whatever the breed's
// actual range is — the assertion is about the gate being open, not about one breed's price.
const screened = await page.evaluate(async (slug) => {
  const breeds = await (await fetch('/api/breeds')).json()
  const { priceLow, priceHigh } = breeds.find((b) => b.slug === slug)
  const res = await fetch(`/api/price-check?breed=${slug}&price=${Math.round(priceLow / 10)}`)
  return { ...(await res.json()), expectedLow: priceLow, expectedHigh: priceHigh }
}, examples.sourced)
console.log('sourced verdict:', screened.level, '| isWarning:', screened.isWarning,
  '| range:', screened.priceLow, '-', screened.priceHigh)

// A price that isn't a round number. step="50" on the input made the browser silently refuse
// to submit the form for anything that wasn't a multiple of 50 — no request, no error, no
// verdict. $6,000 worked and $1,299 did nothing, which is most real quotes, so every check
// here passed while the feature was broken for most users. Driving the actual form is the only
// thing that catches it.
const odd = await page.evaluate(async (slug) => {
  const breeds = await (await fetch('/api/breeds')).json()
  const { priceLow } = breeds.find((b) => b.slug === slug)
  return Math.round(priceLow / 4) * 10 + 9 // deliberately not a multiple of 50
}, examples.sourced)
await page.fill('input[aria-label="Price you were quoted, in dollars"]', String(odd))
await page.click('button:has-text("Check this price")')
await page.waitForTimeout(2500)
const oddVerdictShown = (await page.locator('[data-testid="price-verdict"]').count()) === 1
console.log(`non-round quote $${odd} produced a verdict:`, oddVerdictShown)

// The no-range card must not claim we publish nothing. Its copy said "We're not publishing
// price ranges or checking quotes yet" long after 50 breeds had sourced ranges — written to
// avoid overstating what we had, and left understating it. Assert on what it must NOT say,
// because that's the failure mode: copy that quietly outlives the state it described.
await page.selectOption(breedSelect, '')
await settle()
const pickCard = page.locator('section.card-lift').first()
const pickText = await pickCard.innerText()
const staleClaim = /not publishing price ranges|checking quotes yet/i.test(pickText)
const namesSourcedCount = /\d+ breeds have a range/.test(pickText)
// Clicking a listed example must actually select that breed, or the card is decoration.
const exampleChip = pickCard.locator('button.btn-outline.btn-sm').first()
const exampleCount = await pickCard.locator('button.btn-outline.btn-sm').count()
await exampleChip.click()
await settle()
const chipSelectedBreed = await page.locator(breedSelect).first().inputValue()
console.log('no-range card — stale claim present:', staleClaim,
  '| names a count:', namesSourcedCount, '| examples:', exampleCount,
  '| chip selected:', chipSelectedBreed)

// The list existed, worked, and nobody could find it. Two causes: the hero's chips were all
// styled identically so three buttons sat in a row of four with nothing marking them as
// clickable, and the hero was the only entry point — while someone asking "which breeds have a
// price?" is reading the price card, not re-scanning the header. So assert the routes from
// where the question is actually asked, and that clickable chips are distinguishable from the
// static one.
const priceCard = page.locator('section.card-lift').first()
// Clear the breed first: a breed is still selected from the checks above, and with one
// selected the card shows that breed's range rather than the examples-and-"See all" state.
await page.selectOption(breedSelect, '')
await settle()
const seeAllFromCard = await priceCard.locator('button:has-text("See all")').count()
await page.selectOption(breedSelect, examples.sourced)
await settle()
const compareFromCard = await priceCard.locator('button:has-text("Compare with the other")').count()
const clickableChipsMarked = await page.locator('div.mt-4 > button.underline').count()
const staticChipsMarked = await page.locator('div.mt-4 > span.underline').count()
console.log('routes to the list — card "See all":', seeAllFromCard,
  '| card "Compare":', compareFromCard,
  '| chips marked clickable:', clickableChipsMarked, '| static chips underlined:', staticChipsMarked)
await page.selectOption(breedSelect, '')
await settle()

// The hero advertised "N sourced price ranges" as plain text, with no way to see them: the
// only routes in were guessing a breed in the dropdown or reading the card's six examples.
// Advertising a number you can't inspect is the same shape of problem as publishing a range
// with no way to see its sources. The badge now opens the full list.
await page.click('button:has-text("sourced price ranges")')
await page.waitForTimeout(900)
const listedRanges = await page.locator('[data-testid="sourced-prices"] > li').count()
const claimedRanges = await page.evaluate(async () => {
  const all = await (await fetch('/api/breeds')).json()
  return all.filter((b) => b.confidence === 'verified' && b.priceLow).length
})
// Every row must name what backs it — "143 live listings" and "3 published sources" are very
// different claims, and a bare count would read as more authoritative than one of them is.
const rangesCiteEvidence = await page.locator(
  '[data-testid="sourced-prices"] >> text=/live listings|published source/',
).count()
// Picking a row has to select that breed, or the list is a dead end.
await page.locator('[data-testid="sourced-prices"] > li button').first().click()
await settle()
const listClosed = (await page.locator('.modal-box').count()) === 0
const listPickedBreed = await page.locator(breedSelect).first().inputValue()
console.log('sourced list — rows:', listedRanges, 'of', claimedRanges, 'claimed |',
  'rows citing evidence:', rangesCiteEvidence, '| closed:', listClosed,
  '| selected:', listPickedBreed)

// ---------- adopting: the secondary path ----------
await page.click('button:has-text("Adopt a rescue dog")')
await settle()

// Pick a breed the shelter feeds actually carry. With a breed that has zero matches
// the auto-broadening kicks in and correctly returns everything, which would make
// "does the filter narrow?" unanswerable.
await page.selectOption(breedSelect, 'beagle')
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
  'no price checker while data is unsourced': checkerPresent === 0,
  'no price range shown while unsourced': rangeShown === 0,
  'price-free advice shown instead': priceFreeAdvice === 1,
  'hero does not promise a price check': !/price check/i.test(heroSub),
  // Defence in depth: the gate is enforced server-side, so a direct API call
  // cannot produce a scam verdict either.
  'api refuses to screen unsourced ranges': gated.level === 'Unavailable' && gated.isWarning === false,
  'checker appears for a sourced breed': sourcedChecker === 1,
  'sourced range is shown': sourcedRange === 1,
  'provenance cites the sources': sourcedProvenance >= 1,
  'a non-round price still gets a verdict': oddVerdictShown,
  'the price card offers a way to see every range': seeAllFromCard === 1,
  'a sourced breed offers the comparison': compareFromCard === 1,
  'clickable chips are visually distinct from static ones':
    clickableChipsMarked >= 3 && staticChipsMarked === 0,
  'the badge opens every sourced range, not a sample': listedRanges === claimedRanges,
  'each listed range says what backs it': rangesCiteEvidence === listedRanges,
  'picking from the list selects that breed': listClosed && listPickedBreed.length > 0,
  'no-range card does not claim we publish nothing': staleClaim === false,
  'no-range card says how many breeds are sourced': namesSourcedCount,
  'no-range card offers real breeds to pick': exampleCount >= 3,
  'picking a listed example selects that breed': chipSelectedBreed.length > 0,
  'api screens a sourced breed against its real range':
    screened.level === 'FarBelow' && screened.isWarning === true
    && screened.priceLow === screened.expectedLow && screened.priceHigh === screened.expectedHigh,
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
