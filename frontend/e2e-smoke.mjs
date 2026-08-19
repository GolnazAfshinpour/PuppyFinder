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
    apiCalls.push(url.replace(BASE, ''))
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
// Where the app under test is. Overridable because the port is not ours to assume: a different
// project's Vite server was on 5173 once, and the suite silently pointed at it — `/` returned 200,
// `/api/breeds` 404'd, and the first sign of trouble was a JSON parse error 40 lines later. A
// suite that can be aimed at the wrong app should at least say so.
const BASE = (process.env.BASE_URL ?? 'http://localhost:5173').replace(/\/$/, '')

const settle = () => page.waitForTimeout(2500)
// The breed control is a typeahead now, not a <select>, so driving it means typing rather than
// selecting. Both helpers go through the real control: `pickBreed` opens it, types enough to
// find the breed, and clicks the option by its slug, which is what a person does.
const pickBreed = async (slug) => {
  const input = page.locator('[data-testid="breed-input"]').first()
  await input.click()
  if (!slug) {
    // "Any breed" is the first row, and clearing has to be reachable without scrolling 179 of them.
    await page.locator('[role="option"]:has-text("Any breed")').first().click()
  } else {
    await input.fill(slug.replace(/-/g, ' '))
    await page.locator(`[role="option"][data-slug="${slug}"]`).first().click()
  }
  await settle()
}
// Read from the URL rather than from the widget: that is the real contract (searches are
// shareable), and it does not need a test-only attribute on the component to work.
const currentBreed = () => page.evaluate(
  () => new URLSearchParams(window.location.search).get('breed') ?? '')

// ---------- buying: the default path ----------
await page.goto(BASE)
await page.waitForSelector('h1', { timeout: 15000 })
await settle()

// Confirm this is the app we think it is, and that its API is reachable through the same origin.
// Everything below asserts against selectors and JSON shapes; being pointed at some other app
// produces confusing failures at best and meaningless passes at worst.
const identity = await page.evaluate(async () => {
  const res = await fetch('/api/breeds')
  return { ok: res.ok, status: res.status, title: document.title }
})
if (!identity.ok) {
  console.error(`FATAL: ${BASE} does not serve PuppyFinder's API `
    + `(/api/breeds -> ${identity.status}, page title "${identity.title}").`)
  console.error('Start this app\'s dev server, or set BASE_URL to where it is running.')
  await browser.close()
  process.exit(1)
}

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
await pickBreed(examples.unsourced)
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
await pickBreed(examples.sourced)
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
await pickBreed('')
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
const chipSelectedBreed = await currentBreed()
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
await pickBreed('')
await settle()
const seeAllFromCard = await priceCard.locator('button:has-text("See all")').count()
await pickBreed(examples.sourced)
await settle()
const compareFromCard = await priceCard.locator('button:has-text("Compare")').count()
// Anchors count too: the scam-safety chip became a link to /safe when the guide stopped being
// a dialog, and the rule is about what looks clickable, not which tag produces the click.
const clickableChipsMarked = await page.locator('div.mt-4 > :is(button, a).underline').count()
const staticChipsMarked = await page.locator('div.mt-4 > span.underline').count()
console.log('routes to the list — card "See all":', seeAllFromCard,
  '| card "Compare":', compareFromCard,
  '| chips marked clickable:', clickableChipsMarked, '| static chips underlined:', staticChipsMarked)
await pickBreed('')
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
const listPickedBreed = await currentBreed()
console.log('sourced list — rows:', listedRanges, 'of', claimedRanges, 'claimed |',
  'rows citing evidence:', rangesCiteEvidence, '| closed:', listClosed,
  '| selected:', listPickedBreed)

// ---------- adopting: the secondary path ----------
await page.click('button:has-text("Adopt a rescue dog")')
await settle()

// Pick a breed the shelter feeds actually carry. With a breed that has zero matches
// the auto-broadening kicks in and correctly returns everything, which would make
// "does the filter narrow?" unanswerable.
await pickBreed('beagle')
await settle()
const countAll = await resultTotal()
console.log('adopt mode, breed=beagle:', countAll, 'results')

await pickBreed('')
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
//
// Read on a second tab so the main page keeps the search state the checks below depend on:
// the guide is a real page now, not a dialog over this one.
const guide = await browser.newPage()
await guide.goto(BASE + '/safe')
await guide.waitForSelector('h1')
// Scoped per section rather than reading the whole page. Every one of these sentences lives
// in exactly one section, and a page-wide regex would keep passing after a section vanished.
const readSection = (slug) => guide.locator(`article#${slug}`).innerText()
const namesLivenessTests = /on the spot|name the test|continuous pan/i
  .test(await readSection('video-call'))
// The reverse-image caveat is a red flag, not a video-call test.
const cleanImageSearchCaveated = /appears nowhere else|no longer clears/i
  .test(await readSection('red-flags'))

// The one fact people get wrong about payments, asserted so a copy edit can't flatten it into
// "use a card". Credit and debit behave differently for the identical fraud: credit-card rights
// turn on what you bought, card-network-independent bank-transfer rights turn on who moved the
// money. BBB documents a victim who refused a wire as too risky and then paid by Zelle
// believing it was protected — the misunderstanding was itself the cause of the loss.
const payText = await readSection('payments')
const separatesCreditFromDebit = /Usually recoverable/.test(payText)
  && /Much weaker than credit/.test(payText)
const p2pNotProtected = /Rarely recoverable/.test(payText) && /Zelle/.test(payText)
// Every verdict is a word, not just a badge colour — colour alone never carries the meaning.
const everyMethodHasAWord = (await guide
  .locator('[data-testid="payment-recourse"] .badge').allInnerTexts())
  .filter((t) => t.trim().length > 0).length >= 7
// Everything else in this guide fires before the first payment. BBB's finding is that the scam
// is profitable because its "multi-tiered setup" lets them come back for money several times, so
// the loss accumulates on payments the app never saw. The advice that matters to someone already
// in it is "stop paying" — anything softer is not an intervention.
const feeText = await readSection('escalating-fees')
const saysStopPaying = /Stop paying/i.test(feeText)
const namesTheInventedFees = /temperature-controlled|shipping insurance/i.test(feeText)
  && /refundable/i.test(feeText)
// Victims are threatened with animal-abandonment charges and told the dog's death is their
// fault. Naming the threats as scripted is what defuses them.
const defusesTheThreats = /animal abandonment/i.test(feeText)
// Order: recognise it, stop, then recover. Assert it rather than trusting the array index.
const sectionOrder = await guide.locator('main article h2').allInnerTexts()
const feesBeforeRecourse = sectionOrder.findIndex((t) => /asking for more money/i.test(t))
  < sectionOrder.findIndex((t) => /actually get back/i.test(t))

// Every section is reachable from the app without opening anything — the footer is the only
// crawlable route in, and the guide is an orphan without it.
const footerGuideLinks = await page.locator('footer a[href^="/safe"]').count()
// A footer link must land on its section, not merely on the page. The anchor is only useful
// if the id it names exists, and the two live in different files.
const footerAnchors = await page.locator('footer a[href^="/safe#"]').evaluateAll(
  (links) => links.map((a) => a.getAttribute('href').split('#')[1]),
)
const sectionIds = await guide.locator('main article').evaluateAll((els) => els.map((e) => e.id))
// Counted off the page rather than hardcoded, so adding a ninth section doesn't fail the run
// — the check is "the footer covers all of them", not "there are exactly eight".
const SECTION_COUNT = sectionIds.length
const everyFooterLinkHasASection = footerAnchors.length > 0
  && footerAnchors.every((slug) => sectionIds.includes(slug))
// The per-section URLs from before the guide became one page still have to resolve, and to
// leave one canonical URL behind rather than a second copy of the same content.
await guide.goto(BASE + '/safe/payments')
await guide.waitForSelector('article#payments')
const oldUrlRedirects = guide.url().endsWith('/safe#payments')
await guide.close()

// The fee check: the other half of "is this a scam", and the half that needs no price range.
// Price screening is live for a minority of breeds and silent for the rest; an invented crate
// fee is the same invented crate fee whatever the breed, and this is the only check in the app
// aimed at someone who has already paid.
const fees = await browser.newPage()
const checkFee = async (text, paid, asker) => {
  const card = fees.locator('[data-testid="fee-check"]').first()
  await card.locator('input[aria-label="What the seller is asking money for"]').fill(text)
  await card.locator(`button:has-text("${paid ? "Yes, I've paid" : 'Not yet'}")`).click()
  if (asker) await card.locator(`button:has-text("${asker}")`).click()
  await card.locator('button:has-text("Check this fee")').click()
  await fees.waitForSelector('[data-testid="fee-verdict"]')
  const verdict = card.locator('[data-testid="fee-verdict"]')
  return {
    text: await verdict.innerText(),
    warning: ((await verdict.getAttribute('class')) ?? '').includes('alert-error'),
    actions: await card.locator('[data-testid="fee-actions"] > li').allInnerTexts(),
  }
}
const CONTACTED_ME = 'A transport company that contacted me'
const I_BOOKED = 'A transporter I found and booked'

// It lives on the buying path, where someone is deciding...
await fees.goto(BASE)
await fees.waitForSelector('[data-testid="fee-check"]', { timeout: 15000 })
const feeCheckOnBuyPath = await fees.locator('[data-testid="fee-check"]').count()
const feeBeforePaying = await checkFee('a $350 refundable crate deposit', false)
// ...and the instruction changes with the sequence, because "don't send it" and "stop" are
// different sentences to different people.
const feeAfterPaying = await checkFee('a $350 refundable crate deposit', true)
// A legitimate deposit must not come back as a scam. Being wrong in that direction costs
// someone a real dog and teaches them to ignore the next warning.
const legitimateDeposit = await checkFee('a deposit to hold a puppy from the next litter', false)
// The catalog is a list of fees people have already reported; a scammer renames one for free.
// "We don't recognise it" must not read as "it's fine" once money has moved.
const unknownFeeAfterPaying = await checkFee('a $600 lineage verification charge', true)
// The scam's second actor. BBB's script is that after the deposit a "shipping company" appears
// and every fee from there comes from them — so a transporter who contacted you is the finding
// whatever the fee is called, including one the catalog has never seen.
const handoffUnknownFee = await checkFee('a $240 lineage verification charge', false, CONTACTED_ME)
// ...and the distinction that keeps it from calling every real pet shipper a scammer.
const bookedTransporter = await checkFee('ground transport', false, I_BOOKED)
// The test that settles it without any analysis of the fee, and which the app did not have
// anywhere before: a real puppy can be collected.
const offersThePickupTest = handoffUnknownFee.actions.some((a) => /collect the dog yourself/i.test(a))
const namesTheDirectory = handoffUnknownFee.actions.some((a) => /IPATA/.test(a))
// Western Union and MoneyGram are the two rails the reports name most often, and neither
// appeared anywhere in the app.
const namesAllTheRails = handoffUnknownFee.actions.some((a) => /Western Union/.test(a) && /MoneyGram/.test(a))

// And on the guide, which is where someone mid-scam actually lands from a search.
await fees.goto(BASE + '/safe#escalating-fees')
await fees.waitForSelector('[data-testid="fee-check"]')
const feeCheckOnGuide = await fees.locator('#escalating-fees [data-testid="fee-check"]').count()

// The point of a separate endpoint: it answers for every breed, including the ones price
// screening is switched off for.
const feeApiNeedsNoBreed = await fees.evaluate(async () => {
  const res = await fetch('/api/fee-check?fee=shipping%20insurance&paid=true')
  const body = await res.json()
  return res.ok && body.level === 'StopPaying'
})
await fees.close()
// The breed typeahead. It replaced a 179-option <select>, and the point is not that it looks
// nicer: a native select only jumps to names *beginning* with what you type, so "retriever"
// matched nothing at all and "shepherd" missed Australian Shepherd.
const typeahead = await browser.newPage()
await typeahead.goto(BASE)
await typeahead.waitForSelector('[data-testid="breed-input"]', { timeout: 20000 })
const typeaheadInput = typeahead.locator('[data-testid="breed-input"]').first()
const suggest = async (text) => {
  await typeaheadInput.click()
  await typeaheadInput.fill(text)
  await typeahead.waitForTimeout(300)
  return typeahead.locator('[role="option"][data-slug]').allInnerTexts()
}

const midWordMatches = await suggest('retriever')
// Every suggestion has to actually contain what was typed, or the filter is decorative.
const midWordSearchWorks = midWordMatches.length > 1
  && midWordMatches.every((n) => /retriever/i.test(n))
// A name that starts with the query outranks one that merely contains it.
const prefixOutranksContains = (await suggest('poo'))[0]?.toLowerCase().startsWith('poo') === true
// An empty list is explained rather than left as a blank box.
await suggest('xyzzy')
const noMatchIsExplained = (await typeahead.locator('[role="listbox"]').innerText()).includes('No breeds match')

// Keyboard operable end to end: the list is useless to anyone not using a mouse otherwise.
await typeaheadInput.fill('beagle')
await typeahead.waitForTimeout(300)
await typeaheadInput.press('ArrowDown')
await typeaheadInput.press('ArrowDown')
await typeaheadInput.press('Enter')
await typeahead.waitForTimeout(1800)
const keyboardSelects = await typeahead.evaluate(
  () => new URLSearchParams(location.search).get('breed')) === 'beagle'
// Clearing is one click, not a scroll back to the top of a 174-row list.
await typeahead.locator('[aria-label="Clear breed"]').click()
await typeahead.waitForTimeout(1500)
const clearIsOneClick = await typeahead.evaluate(
  () => new URLSearchParams(location.search).get('breed')) === null

// The catalog must not carry the same animal twice. The typeahead is what exposed the German
// Shepherd duplicate: alphabetically the two sat 170 rows apart, so the old select hid it.
const duplicateBreeds = await typeahead.evaluate(async () => {
  const breeds = await (await fetch('/api/breeds')).json()
  const seen = new Map()
  const dupes = []
  for (const b of breeds) {
    const key = [...new Set(b.displayName.toLowerCase().replace(/[()]/g, ' ').split(/\s+/).filter(Boolean))]
      .sort().join(' ')
    if (seen.has(key)) dupes.push(`${seen.get(key)} / ${b.slug}`)
    else seen.set(key, b.slug)
  }
  return dupes
})
await typeahead.close()
console.log('breed typeahead — "retriever":', midWordMatches.length, 'matches',
  '| prefix outranks contains:', prefixOutranksContains,
  '| keyboard:', keyboardSelects, '| clear:', clearIsOneClick,
  '| duplicate breeds:', duplicateBreeds.length ? duplicateBreeds : 'none')

// The seller check: the only thing in the app that ends in a public database rather than in
// advice. Under the Animal Welfare Act a breeder needs a USDA licence when they keep more than
// four breeding females AND sell sight-unseen, and a puppy shipped to a buyer is not a
// face-to-face sale — so the exemption a shipper claims can only be the four-females one.
const seller = await browser.newPage()
const askSeller = async (delivery, licence) => {
  const card = seller.locator('[data-testid="seller-check"]').first()
  await card.locator(`button:has-text("${delivery}")`).click()
  await seller.waitForTimeout(150)
  if (licence) await card.locator(`button:has-text("${licence}")`).click()
  await card.locator('button:has-text("What does that mean?")').click()
  await seller.waitForSelector('[data-testid="seller-verdict"]')
  const verdict = card.locator('[data-testid="seller-verdict"]')
  return {
    text: await verdict.innerText(),
    warning: ((await verdict.getAttribute('class')) ?? '').includes('alert-error'),
  }
}
await seller.goto(BASE)
await seller.waitForSelector('[data-testid="seller-check"]', { timeout: 20000 })

const SHIPS = "They'd ship it to me"
const IN_PERSON = "I'd see the puppy first"
// The one branch that warns, and the reason it does: both answers are trivially easy to give.
const sellerRefuses = await askSeller(SHIPS, "They won't say")
// A number is something to check, not something to trust — it can be copied off another site.
const sellerGaveNumber = await askSeller(SHIPS, 'They gave me a number')
// The line that does the work.
const sellerClaimsExempt = await askSeller(SHIPS, "They say they don't need one")
// The other direction: most good hobby breeders are legitimately exempt, and saying "this check
// doesn't apply" is worth more than manufacturing a warning that points buyers away from them.
const sellerInPerson = await askSeller(IN_PERSON, null)

// The licence question is only asked where the answer means something.
await seller.locator(`button:has-text("${IN_PERSON}")`).first().click()
await seller.waitForTimeout(300)
const licenceAskedInPerson = await seller.locator('text=USDA licence number?').count()
await seller.goto(BASE + '/safe#vet-a-breeder')
await seller.waitForTimeout(1200)
const sellerCheckInGuide = await seller.locator('#vet-a-breeder [data-testid="seller-check"]').count()
await seller.close()
console.log('seller check — refuses:', JSON.stringify(sellerRefuses.text.split('\n')[0]),
  '| in person:', JSON.stringify(sellerInPerson.text.split('\n')[0]),
  '| licence asked in person:', licenceAskedInPerson,
  '| in guide:', sellerCheckInGuide)

// Filtering on good-with. The display landed first; this is the filter, and it is the one place
// in the app where "unknown is not no" and "no really means no" have to hold at the same time.
const gw = await browser.newPage()
const gwCount = async (query) => {
  const dogs = await gw.evaluate(async (q) => (await (await fetch(`/api/listings${q}`)).json()), query)
  return {
    total: dogs.length,
    // The hard requirement: a rescue that wrote "not good with cats" must never be overridden.
    explicitNos: dogs.filter((d) => d.goodWithCats === false).length,
    unconfirmed: dogs.filter((d) => d.unconfirmed).length,
  }
}
await gw.goto(BASE + '/?goal=adopt')
await gw.waitForSelector('[data-testid="dog-results"] > li', { timeout: 20000 })

const gwAll = await gwCount('')
const gwCats = await gwCount('?goodWith=cats')
const gwCatsStrict = await gwCount('?goodWith=cats&includeUnlisted=false')
// Narrows, rather than being a control that quietly does nothing.
const goodWithNarrows = gwCats.total < gwAll.total && gwCats.total > 0
// The asymmetry: unknowns survive, explicit noes do not, and `includeUnlisted` cannot reach them.
const noExplicitNoSurvives = gwCats.explicitNos === 0 && gwCatsStrict.explicitNos === 0
const unknownsAreKeptAndLabelled = gwCats.unconfirmed > 0
const strictDropsOnlyTheUnrecorded = gwCatsStrict.total > 0
  && gwCatsStrict.total === gwCats.total - gwCats.unconfirmed

// Offered only where it can do something: buy mode has no listings to narrow.
const gwGroupWhenAdopting = await gw.locator("text=From each rescue's own listing").count()
await gw.goto(BASE + '/')
await gw.waitForTimeout(2000)
const gwGroupWhenBuying = await gw.locator("text=From each rescue's own listing").count()

// Two controls that sound alike must not produce two identical chips.
await gw.goto(BASE + '/?goal=adopt&goodWith=kids&traits=kids')
await gw.waitForTimeout(2500)
const chipLabels = await gw.locator('.badge-primary.badge-soft').allInnerTexts()
const chipsAreDistinguishable = new Set(chipLabels.map((c) => c.trim())).size === chipLabels.length

// The banner explaining why unconfirmed dogs are in the list has to name the right field.
await gw.goto(BASE + '/?goal=adopt&goodWith=cats')
await gw.waitForTimeout(2500)
const caveat = await gw.locator('.alert:has-text("unconfirmed")').first().innerText().catch(() => '')
const caveatNamesTheRightField = /good with cats/i.test(caveat)
await gw.close()
console.log('good-with filter — all:', gwAll.total, '| cats:', gwCats.total,
  '(', gwCats.unconfirmed, 'unconfirmed )', '| strict:', gwCatsStrict.total,
  '| explicit noes surviving:', gwCats.explicitNos,
  '| chips:', JSON.stringify(chipLabels))

// Adoption fee and good-with-kids/dogs/cats: the two fields DESIGN.md named as the biggest
// listing gaps. Both are sparse in the feed (fee ~24%, good-with 21-41%), so the checks are
// about honesty as much as presence — a blank must never render as "no".
const profiles = await browser.newPage()
await profiles.goto(BASE + '/?goal=adopt')
await profiles.waitForSelector('[data-testid="dog-results"] > li', { timeout: 20000 })
const sample = await profiles.evaluate(async () => {
  const dogs = await (await fetch('/api/listings')).json()
  return {
    total: dogs.length,
    withFee: dogs.filter((d) => d.adoptionFee).length,
    // A fee that is a bare number, a placeholder, or "$0" would all be bugs.
    badFees: dogs.map((d) => d.adoptionFee).filter(Boolean)
      .filter((f) => /^\s*[\d.,]+\s*$/.test(f) || /^(n\/a|none|tbd|-)$/i.test(f) || /^\$0(\.00)?$/.test(f)),
    // Two dogs, chosen for what they prove. One sample can only ever show whichever of the
    // two the feed happened to give it, and the negative case is the one worth pinning: a run
    // that picked an all-negative dog reported "the detail view shows good-with" as broken.
    withFeeAndPositive: dogs.find((d) => d.adoptionFee
      && (d.goodWithKids === true || d.goodWithDogs === true || d.goodWithCats === true))?.id,
    withNegative: dogs.find((d) => d.goodWithKids === false
      || d.goodWithDogs === false || d.goodWithCats === false)?.id,
    // Deliberately a dog with no phone number either: the prompt used to live inside the
    // contact box, so the dogs carrying the least information got no prompt at all.
    noFee: (dogs.find((d) => !d.adoptionFee && !d.contactInfo) ?? dogs.find((d) => !d.adoptionFee))?.id,
    // The three-state rule, checked on the wire rather than in the UI: absent must arrive as
    // null, not false, or the whole "unknown is not no" contract is broken at the source.
    nullNotFalse: dogs.some((d) => d.goodWithCats === null),
  }
})
const feesAreFormatted = sample.badFees.length === 0
const someFeesPublished = sample.withFee > 0

const badgesFor = async (id) => {
  await profiles.goto(`${BASE}/?dog=${encodeURIComponent(id)}`)
  await profiles.waitForSelector('#dog-detail-name')
  return (await profiles.locator('.modal-box .badge').allInnerTexts()).join(' | ')
}

const detailBadges = await badgesFor(sample.withFeeAndPositive)
const detailShowsFee = /Adoption fee \$/.test(detailBadges)
// A positive must read as one, and not be swallowed by the "Not good with" wording.
const detailShowsGoodWith = /(^|\| )Good with/.test(detailBadges)

// Stated plainly rather than hidden — someone with a cat needs the negative most of all.
const negativeBadges = await badgesFor(sample.withNegative)
const detailStatesNegatives = /Not good with/.test(negativeBadges)

await profiles.goto(`${BASE}/?dog=${encodeURIComponent(sample.noFee)}`)
// The name, not the box. `.modal-box` is present during the loading skeleton too, so counting
// against it raced the fetch — and passed for months only because the dogs sampled before
// happened to already be in the loaded grid and rendered instantly.
await profiles.waitForSelector('#dog-detail-name')
// Pinned to a dog with no contact info too, since that was the case the prompt used to miss.
const noFeeAsksInstead = (await profiles.locator("text=hasn't listed an adoption fee").count()) === 1
await profiles.close()
console.log('listing profiles — total:', sample.total, '| with a fee:', sample.withFee,
  '| malformed fees:', sample.badFees.length,
  '| detail badges:', JSON.stringify(detailBadges),
  '| negative badges:', JSON.stringify(negativeBadges))

console.log('fee check — handoff:', JSON.stringify(handoffUnknownFee.text.split('\n')[0]),
  '| booked transporter warns:', bookedTransporter.warning,
  '| pickup test offered:', offersThePickupTest,
  '| directory named:', namesTheDirectory,
  '| all rails named:', namesAllTheRails)
console.log('fee check — on buy path:', feeCheckOnBuyPath, '| on guide:', feeCheckOnGuide,
  '| before paying:', JSON.stringify(feeBeforePaying.text.split('\n')[0]),
  '| after paying:', JSON.stringify(feeAfterPaying.text.split('\n')[0]),
  '| legitimate deposit warns:', legitimateDeposit.warning,
  '| unknown fee after paying warns:', unknownFeeAfterPaying.warning,
  '| API needs no breed:', feeApiNeedsNoBreed)
console.log('advice — liveness tests named:', namesLivenessTests,
  '| clean image search caveated:', cleanImageSearchCaveated,
  '| says stop paying:', saysStopPaying,
  '| names the fees:', namesTheInventedFees,
  '| defuses the threats:', defusesTheThreats,
  '| fees before recourse:', feesBeforeRecourse,
  '| credit vs debit separated:', separatesCreditFromDebit,
  '| P2P not protected:', p2pNotProtected,
  '| verdicts worded:', everyMethodHasAWord,
  '| linked from the app footer:', footerGuideLinks,
  '| every footer anchor has a section:', everyFooterLinkHasASection,
  '| old section URL redirects:', oldUrlRedirects)

// No source's markup may reach the reader. RescueGroups stores bios as HTML source, so 193 of
// 297 arrived with entities intact and the page showed "I&rsquo;ve been at the Orangeburg SPCA"
// verbatim. Checked across the whole rendered page rather than one field, because the next
// occurrence will be in whichever field a future source maps carelessly.
const entityMarkup = await page.evaluate(() => {
  const text = document.body.innerText
  return [...new Set([...text.matchAll(/&[a-z]{2,8};|&#\d+;/gi)].map((m) => m[0]))]
})
console.log('undecoded entity markup on the page:', entityMarkup.join(' ') || 'none')

// The adopt path names its sources and explains the main one. RescueGroups supplies most of the
// dogs and is the name a reader is least likely to know, so "who" without "why" is a gap — and
// the caveats are the part most likely to be dropped in a tidy-up, since they are the only
// paragraph that makes the product sound worse.
await page.click('summary:has-text("Where these dogs come from")')
await page.waitForTimeout(400)
const provenance = await page.locator('main').innerText()
const namesTheNonProfit = /501\(c\)\(3\)/.test(provenance) && /RescueGroups/.test(provenance)
const saysWhyNotAMarketplace = /nobody pays to be listed|not a marketplace/i.test(provenance)
const admitsUnevenCoverage = /uneven by state|rather than complete/i.test(provenance)
const admitsMissingFields = /no photo or no size/i.test(provenance)

// Distance search. The filter adopters use most, per the Adopt-a-Pet research DESIGN.md cites, and
// the one whose failure is invisible — a mile count that is quietly wrong still looks like a mile
// count, and a "nearest" option that sorts nothing looks like a working control.
const zipBox = 'input[aria-label="ZIP code to measure distance from"]'
const totalBeforeZip = await page.locator('[data-testid="dog-results"] > li').count()
await page.fill(zipBox, '20009')          // Washington DC
await page.waitForTimeout(2600)           // debounced lookup, then a refetch
const nearestOffered = (await page.locator('option:has-text("Nearest first")').count()) > 0
await page.selectOption('select[aria-label="How far you will travel"]', '50')
await page.waitForTimeout(2600)

const nearHeading = await page.locator('[data-testid="results-heading"], h2').first().innerText()
const withinFifty = await page.locator('[data-testid="dog-results"] > li').count()
// Mileages as rendered, in the order the page lists them.
const shownMiles = (await page.locator('[data-testid="dog-results"] > li').allInnerTexts())
  .map((t) => t.match(/(\d+)\s*mi away/))
  .filter(Boolean)
  .map((m) => Number(m[1]))
const everyDogHasAMileage = shownMiles.length === withinFifty
const ascending = shownMiles.every((m, i) => i === 0 || m >= shownMiles[i - 1])
const withinTheRadius = shownMiles.every((m) => m <= 50)
const zipInUrl = new URL(page.url()).searchParams.get('zip') === '20009'
  && new URL(page.url()).searchParams.get('radius') === '50'

// A ZIP that resolves to nothing must say so rather than leaving a filter that looks applied.
await page.fill(zipBox, '00000')
await page.waitForTimeout(2600)
const badZipWarns = (await page.locator('p.text-warning').count()) > 0
await page.fill(zipBox, '')
await page.waitForTimeout(2000)

console.log('distance — nearest offered:', nearestOffered,
  '| dogs:', totalBeforeZip, '->', withinFifty,
  '| mileages:', shownMiles.slice(0, 5).join(','),
  '| ascending:', ascending, '| within 50:', withinTheRadius, '| bad zip warns:', badZipWarns)

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

await page.goto(`${BASE}/?dog=${sharedId}`)
await page.waitForTimeout(3000)
const sharedResolves = (await page.locator('#dog-detail-name').count()) === 1
console.log('shared ?dog= link resolves:', sharedResolves)

await page.goto(`${BASE}/?dog=montgomery-county-animal-services-a000000`)
await page.waitForTimeout(2500)
const goneHandled = (await page.locator('text=no longer listed').count()) === 1
console.log('adopted dog handled gracefully:', goneHandled)

// Back and Forward, which did not work at all: the address bar synced with replaceState, so
// two filter changes created ZERO history entries and pressing Back left the site instead of
// undoing a filter. There was no popstate listener either, so even with entries the page would
// have silently disagreed with its own URL. A shareable URL that isn't navigable is half a
// feature.
await page.goto('about:blank')                 // give Back somewhere real to go
await page.goto(BASE)
await page.waitForSelector('h1', { timeout: 15000 })
await settle()
await pickBreed('beagle')
await settle()
const afterPick = page.url()
await page.click('button:has-text("Or adopt")')
await settle()
const afterAdopt = page.url()
await page.goBack()
await settle()
const backUrl = page.url()
const backHeading = await page.locator('h1').innerText()
const backKeptBreed = (await currentBreed()) === 'beagle'
await page.goForward()
await settle()
const forwardUrl = page.url()
const stillOnApp = page.url().startsWith(BASE)
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
  // The safety guide left this list when it stopped being a dialog: it is pages now.
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
  'no source\'s HTML entity markup reaches the reader': entityMarkup.length === 0,
  'a ZIP offers a nearest-first sort': nearestOffered,
  'a radius narrows the results': withinFifty > 0 && withinFifty < totalBeforeZip,
  'every dog in a radius search shows its distance': everyDogHasAMileage,
  'nearest really is ascending by distance': ascending,
  'nothing outside the radius survives it': withinTheRadius,
  'the distance search is shareable': zipInUrl,
  'a ZIP that resolves to nothing says so': badZipWarns,
  'the adopt path says what RescueGroups is': namesTheNonProfit,
  'and why a non-profit feed beats a marketplace': saysWhyNotAMarketplace,
  'while admitting coverage is uneven': admitsUnevenCoverage,
  'and that some listings are incomplete': admitsMissingFields,
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
  // The guide is reachable and its links land where they claim. A page nothing links to is an
  // orphan however good it is, and an anchor whose id was renamed fails silently at the top of
  // the page — the two halves live in different files, so only a test connects them.
  'the app footer links every section of the guide': footerGuideLinks === SECTION_COUNT + 1,
  'every footer anchor lands on a real section': everyFooterLinkHasASection,
  'the older per-section URLs still resolve, to one canonical URL': oldUrlRedirects,
  // The fee check. Every other check in this app fires before the first payment; BBB's finding
  // is that the loss accumulates on payments two, three and four.
  'the fee check is on the buying path and on the guide': feeCheckOnBuyPath === 1 && feeCheckOnGuide === 1,
  'an invented fee is refused before any money moves': feeBeforePaying.warning
    && !/stop paying/i.test(feeBeforePaying.text),
  'someone who already paid is told to stop, not merely warned': feeAfterPaying.warning
    && /stop paying/i.test(feeAfterPaying.text),
  'a legitimate deposit is not called a scam': !legitimateDeposit.warning,
  'a legitimate cost is still not an all-clear': /does not make this request safe/i.test(legitimateDeposit.text),
  'an unrecognised fee after payment still warns': unknownFeeAfterPaying.warning,
  'the fee check answers without a breed, for every breed': feeApiNeedsNoBreed,
  // Who is asking is often the decisive input: the transport company is the scam's second act.
  'a transporter that made contact is flagged whatever the fee is called': handoffUnknownFee.warning
    && /second act/i.test(handoffUnknownFee.text),
  'a transporter the buyer booked is not swept up with it': !bookedTransporter.warning,
  'the pickup test is offered — a real puppy can be collected': offersThePickupTest,
  'the shipper directory is named rather than their own paperwork': namesTheDirectory,
  'Western Union and MoneyGram are named among the unrecoverable rails': namesAllTheRails,
  // The two fields adopters rank highest, and the app showed neither.
  'adoption fees reach the listings': someFeesPublished,
  'every published fee is formatted, never a bare number or a placeholder': feesAreFormatted,
  'the detail view shows the adoption fee': detailShowsFee,
  'a dog with no published fee is told to ask instead': noFeeAsksInstead,
  'the detail view shows good-with-kids/dogs/cats': detailShowsGoodWith,
  'a negative is stated plainly, not hidden': detailStatesNegatives,
  // The contract the whole feature rests on: the feed omits null attributes, so an unrecorded
  // field must arrive as null. Coercing it to false would rule dogs out over a blank.
  'an unrecorded good-with field arrives as null, not false': sample.nullNotFalse,
  // The filter, and the asymmetry at the heart of it.
  'the good-with filter actually narrows the results': goodWithNarrows,
  'a dog the rescue marked unsuitable is never shown, even in loose mode': noExplicitNoSurvives,
  'dogs with nothing recorded are kept and labelled': unknownsAreKeptAndLabelled,
  'strict match drops only the unrecorded ones': strictDropsOnlyTheUnrecorded,
  'the good-with filter is offered when adopting': gwGroupWhenAdopting === 1,
  'and hidden when buying, where there are no dogs to narrow': gwGroupWhenBuying === 0,
  'the breed narrower and the dog filter make distinguishable chips': chipsAreDistinguishable,
  'the unconfirmed banner names the field actually filtered on': caveatNamesTheRightField,
  // The seller check. A licence is a floor, never an endorsement — and its absence is only
  // meaningful for a sight-unseen sale.
  'a shipper who will not produce a licence number is the one warning': sellerRefuses.warning
    && /no innocent silence/i.test(sellerRefuses.text),
  'a licence number is something to verify, not to trust': !sellerGaveNumber.warning
    && /name and address match/i.test(sellerGaveNumber.text),
  'holding a licence is never presented as an endorsement': /floor/i.test(sellerGaveNumber.text),
  'a shipped puppy cannot be a face-to-face sale': /not a face-to-face sale/i.test(sellerClaimsExempt.text),
  'seeing the puppy in person takes licensing off the table': !sellerInPerson.warning
    && /proves nothing/i.test(sellerInPerson.text),
  'the licence question is only asked when it could matter': licenceAskedInPerson === 0,
  'the seller check is on the buying path and in the vetting section': sellerCheckInGuide === 1,
  // The breed typeahead, and the reason it replaced a <select>.
  'a breed is findable by a word in the middle of its name': midWordSearchWorks,
  'a name starting with the query outranks one merely containing it': prefixOutranksContains,
  'no matches is explained rather than left blank': noMatchIsExplained,
  'the breed list is fully keyboard operable': keyboardSelects,
  'clearing the breed is one click': clearIsOneClick,
  'the breed catalog never lists the same animal twice': duplicateBreeds.length === 0,
  'detail view opens in-app': detailOpen === 1 && detailAddressable,
  'detail view closes on Escape': detailClosed,
  'shared dog link resolves': sharedResolves,
  'adopted dog handled gracefully': goneHandled,
}
for (const [name, ok] of Object.entries(checks)) console.log(ok ? `PASS  ${name}` : `FAIL  ${name}`)
process.exit(Object.values(checks).every(Boolean) ? 0 : 1)
