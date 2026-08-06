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
// The results heading states the full count ("53 adoptable dogs"); the grid reveals 24 at a
// time. Filter assertions must read the total, or a working filter looks broken once both
// sides of the comparison hit the page cap.
const resultTotal = async () => {
  const heading = await page.locator('h2:has-text("adoptable"), h2:has-text("Showing")').first().innerText()
  return Number(heading.match(/\d+/)?.[0] ?? 0)
}
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
const checkerPresent = await page.locator('input[aria-label="Price you were quoted, in dollars"]').count()
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
const sourcedChecker = await page.locator('input[aria-label="Price you were quoted, in dollars"]').count()
const sourcedRange = await page.locator('.text-primary.text-4xl').count()
// The provenance sentence differs by basis, and deliberately so — "49 sources" means 49
// articles for an editorial range and 49 puppies for sale for a listing one. Accept either
// wording, but require one of them: a range that can't say where it came from is the fault
// this whole feature exists to fix.
const sourcedProvenance = (await page.locator('text=independent source').count())
  + (await page.locator('text=live listings').count())
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
await page.click('button:has-text("Check a quote")')
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
const compareFromCard = await priceCard.locator('button:has-text("Compare")').count()
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
const countAll = await resultTotal()
console.log('adopt mode, breed=beagle:', countAll, 'results')

await page.selectOption(breedSelect, '')
await settle()
const countAny = await resultTotal()
console.log('any breed:', countAny, 'results')

await page.click('button:has-text("Puppy")')
await settle()
const countPuppies = await resultTotal()
console.log('age=Puppy:', countPuppies, 'results')
await page.click('button:has-text("Any age")')
await settle()

await page.selectOption('select:below(:text("State"))', 'MD')
await settle()
const countMd = await resultTotal()
console.log('state=MD:', countMd, 'results')

await page.selectOption('select:has(option[value="youngest"])', 'youngest')
await settle()
const countSorted = await resultTotal()
console.log('sort=youngest:', countSorted, 'results')

// Paging: the grid reveals a page at a time, but the heading must keep stating the true
// total — the honest-coverage rule applies to a "show more" button as much as to an empty
// state. 53 cards in one scroll measured 10,539px, about ten screens.
await page.selectOption('select:below(:text("State"))', '')
await settle()
const pagedTotal = await resultTotal()
const pagedShown = await cards().count()
const revealButton = page.locator('button:has-text("more dogs")')
const hasReveal = (await revealButton.count()) === 1
if (hasReveal) await revealButton.click()
await page.waitForTimeout(900)
const afterReveal = await cards().count()
console.log('paging — total:', pagedTotal, '| first page:', pagedShown,
  '| after reveal:', afterReveal)

// The advice must not slide back to a bare "have a video call". That was this guide's central
// recommendation until BBB warned it "may be going away" because generated video can satisfy
// it — so the call now has to be interactive on the buyer's terms, and a clean reverse-image
// result no longer clears anyone. Asserting the caveats exist is what stops a future copy edit
// quietly restoring advice that no longer holds.
await page.click('button:has-text("Scam-safety checklist")')
await page.waitForTimeout(1000)
// Open the section first: it's collapsed by default (correctly — that's the progressive
// disclosure the guide is built on), and innerText excludes hidden content.
await page.click('summary:has-text("Make the video call prove something")')
await page.waitForTimeout(500)
const guideText = await page.locator('.modal-box').innerText()
const namesLivenessTests = /on the spot|name the test|continuous pan/i.test(guideText)
const cleanImageSearchCaveated = /appears nowhere else|no longer clears/i.test(guideText)

// The one fact people get wrong about payments, asserted so a copy edit can't flatten it into
// "use a card". Credit and debit behave differently for the identical fraud: credit-card rights
// turn on what you bought, card-network-independent bank-transfer rights turn on who moved the
// money. BBB documents a victim who refused a wire as too risky and then paid by Zelle
// believing it was protected — the misunderstanding was itself the cause of the loss.
await page.click('summary:has-text("What you can actually get back")')
await page.waitForTimeout(500)
const payText = await page.locator('.modal-box').innerText()
const separatesCreditFromDebit = /Usually recoverable/.test(payText)
  && /Much weaker than credit/.test(payText)
const p2pNotProtected = /Rarely recoverable/.test(payText) && /Zelle/.test(payText)
// Every verdict is a word, not just a badge colour — colour alone never carries the meaning.
const everyMethodHasAWord = (await page
  .locator('[data-testid="payment-recourse"] .badge').allInnerTexts())
  .filter((t) => t.trim().length > 0).length >= 7
// Everything else in this guide fires before the first payment. BBB's finding is that the scam
// is profitable because its "multi-tiered setup" lets them come back for money several times, so
// the loss accumulates on payments the app never saw. The advice that matters to someone already
// in it is "stop paying" — anything softer is not an intervention.
await page.click('summary:has-text("They are asking for more money")')
await page.waitForTimeout(500)
const feeText = await page.locator('.modal-box').innerText()
const saysStopPaying = /Stop paying/i.test(feeText)
const namesTheInventedFees = /temperature-controlled|shipping insurance/i.test(feeText)
  && /refundable/i.test(feeText)
// Victims are threatened with animal-abandonment charges and told the dog's death is their
// fault. Naming the threats as scripted is what defuses them.
const defusesTheThreats = /animal abandonment/i.test(feeText)
// Order: recognise it, stop, then recover. Assert it rather than trusting the array index.
const sectionOrder = await page.locator('.modal-box summary').allInnerTexts()
const feesBeforeRecourse = sectionOrder.findIndex((t) => /asking for more money/i.test(t))
  < sectionOrder.findIndex((t) => /actually get back/i.test(t))

await page.keyboard.press('Escape')
await page.waitForTimeout(600)
console.log('advice — liveness tests named:', namesLivenessTests,
  '| clean image search caveated:', cleanImageSearchCaveated,
  '| says stop paying:', saysStopPaying,
  '| names the fees:', namesTheInventedFees,
  '| defuses the threats:', defusesTheThreats,
  '| fees before recourse:', feesBeforeRecourse,
  '| credit vs debit separated:', separatesCreditFromDebit,
  '| P2P not protected:', p2pNotProtected,
  '| verdicts worded:', everyMethodHasAWord)

// Line length, asserted rather than eyeballed. Measured at 91-117 chars across 13 of 13 prose
// blocks before this rule existed — past the 80 of WCAG 1.4.8, which Baymard finds readers
// experience as "intimidating and overwhelming". This is the check that stops it drifting back
// one unconstrained paragraph at a time, which is exactly how it got there.
const longLines = await page.evaluate(() => {
  const over = []
  for (const el of document.querySelectorAll('main p, main li, main span, .modal-box p, .modal-box li')) {
    const text = el.textContent.trim()
    // Only leaf running text: short labels and wrapper elements aren't prose.
    if (text.length < 60 || el.querySelector('p,li,span')) continue
    const width = el.getBoundingClientRect().width
    const fontSize = parseFloat(getComputedStyle(el).fontSize)
    const chars = Math.round(width / (fontSize * 0.5)) // ~0.5em average glyph width
    if (chars > 80) over.push(`${chars}ch: ${text.slice(0, 40)}`)
  }
  return over
})
if (longLines.length) console.log('  long lines:', longLines.join(' | '))
console.log('prose blocks over 80 chars:', longLines.length)

// Saving a dog was one click and available everywhere; getting back to one was a 5,000px
// scroll to a collapsed accordion at 90% of the page, and "recently viewed" rendered only in
// the empty-results branch — visible exactly when you had found nothing. Both now live behind
// one control in the sticky nav, so the answer to "where are my dogs?" doesn't depend on scroll
// position or mode.
const savedNav = () => page.locator('button:has-text("Your dogs")')
const navBeforeSaving = await savedNav().count()   // nothing saved yet, so nothing to offer
const hearts = page.locator('[data-testid="dog-results"] > li button[aria-pressed]')
for (let i = 0; i < 3; i++) {
  await hearts.nth(i).click()
  await page.waitForTimeout(250)
}
await page.waitForTimeout(600)
const navAfterSaving = await savedNav().count()
const navY = (await savedNav().boundingBox()).y
await savedNav().click()
await page.waitForTimeout(800)
const savedRows = await page.locator('[data-testid="saved-dogs"] > li').count()
// Removing from the list must actually unsave, not just hide the row.
await page.locator('[data-testid="saved-dogs"] button:has-text("Remove")').first().click()
await page.waitForTimeout(600)
const rowsAfterRemove = await page.locator('[data-testid="saved-dogs"] > li').count()
// A saved snapshot has to reopen the real dog, since shelters drop adopted dogs from the feed.
await page.locator('[data-testid="saved-dogs"] > li button').first().click()
await page.waitForTimeout(2000)
const savedOpensDetail = (await page.locator('#dog-detail-name').count()) === 1
await page.keyboard.press('Escape')
await page.waitForTimeout(700)
console.log('saved dogs — nav hidden when empty:', navBeforeSaving === 0,
  '| appears after saving:', navAfterSaving === 1,
  '| nav y:', Math.round(navY),
  '| rows:', savedRows, '-> after remove:', rowsAfterRemove,
  '| reopens the dog:', savedOpensDetail)

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

// Back and Forward, which did not work at all: the address bar synced with replaceState, so
// two filter changes created ZERO history entries and pressing Back left the site instead of
// undoing a filter. There was no popstate listener either, so even with entries the page would
// have silently disagreed with its own URL. A shareable URL that isn't navigable is half a
// feature.
await page.goto('about:blank')                 // give Back somewhere real to go
await page.goto('http://localhost:5173')
await page.waitForSelector('h1', { timeout: 15000 })
await settle()
await page.selectOption(breedSelect, 'beagle')
await settle()
const afterPick = page.url()
await page.click('button:has-text("Or adopt")')
await settle()
const afterAdopt = page.url()
await page.goBack()
await settle()
const backUrl = page.url()
const backHeading = await page.locator('h1').innerText()
const backKeptBreed = (await page.locator(breedSelect).first().inputValue()) === 'beagle'
await page.goForward()
await settle()
const forwardUrl = page.url()
const stillOnApp = page.url().includes('localhost:5173')
console.log('history — after pick:', afterPick.includes('beagle'),
  '| adopt:', afterAdopt.includes('goal=adopt'),
  '| Back undid it:', backUrl === afterPick,
  '| Back kept the breed:', backKeptBreed,
  '| Forward redid it:', forwardUrl === afterAdopt)

// Escape closed the dog detail and silently did nothing on the other three dialogs, which
// teaches the key and then ignores it.
//
// Back in buying mode first: goForward() above left us adopting, and the price-ranges chip is
// correctly hidden there — it advertises something that mode can't answer.
await page.click('button:has-text("Or buy from a breeder")')
await settle()
const dialogCount = () => page.locator('.modal-box').count()
const escapes = {}
for (const [name, selector] of [
  ['guide', 'button:has-text("Scam-safety checklist")'],
  ['prices', 'button:has-text("sourced price ranges")'],
  ['quiz', 'button:has-text("breed quiz")'],
]) {
  await page.click(selector)
  await page.waitForTimeout(800)
  const opened = (await dialogCount()) === 1
  await page.keyboard.press('Escape')
  await page.waitForTimeout(700)
  escapes[name] = opened && (await dialogCount()) === 0
}
console.log('escape closes:', JSON.stringify(escapes))

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
  'the grid pages rather than dumping every dog': pagedShown < pagedTotal && hasReveal,
  'revealing more shows more': afterReveal > pagedShown,
  'the heading states the true total, not the page': pagedTotal > pagedShown,
  'no prose block exceeds 80 characters per line': longLines.length === 0,
  'Back undoes a filter change instead of leaving the site':
    stillOnApp && backUrl === afterPick,
  'Back restores the rest of the search, not just the URL': backKeptBreed,
  'Forward redoes it': forwardUrl === afterAdopt,
  'every dialog closes on Escape': Object.values(escapes).every(Boolean),
  'saved dogs are reachable from the sticky nav, not a buried accordion':
    navBeforeSaving === 0 && navAfterSaving === 1 && navY < 100,
  'the saved list shows what was saved': savedRows === 3,
  'removing from the list actually unsaves': rowsAfterRemove === 2,
  'a saved dog reopens its detail view': savedOpensDetail,
  'video-call advice names liveness tests, not just "have a call"': namesLivenessTests,
  'a clean reverse-image result is caveated, not treated as proof': cleanImageSearchCaveated,
  'credit and debit are told apart, not lumped into "use a card"': separatesCreditFromDebit,
  'someone already paying is told to stop, not merely to be careful': saysStopPaying,
  'the invented fees are named so a victim recognises theirs': namesTheInventedFees,
  'the scripted threats are named as scripted': defusesTheThreats,
  'the fee section comes before the recourse section': feesBeforeRecourse,
  'payment apps are not presented as protected': p2pNotProtected,
  'every payment verdict is a word, not a badge colour alone': everyMethodHasAWord,
  'detail view opens in-app': detailOpen === 1 && detailAddressable,
  'detail view closes on Escape': detailClosed,
  'shared dog link resolves': sharedResolves,
  'adopted dog handled gracefully': goneHandled,
}
for (const [name, ok] of Object.entries(checks)) console.log(ok ? `PASS  ${name}` : `FAIL  ${name}`)
process.exit(Object.values(checks).every(Boolean) ? 0 : 1)
