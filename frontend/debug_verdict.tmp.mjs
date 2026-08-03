import { chromium } from 'playwright'

const browser = await chromium.launch()
const page = await browser.newPage({ viewport: { width: 1280, height: 1200 } })
page.on('pageerror', (e) => console.log('  [pageerror]', e.message))
page.on('response', (r) => {
  if (r.url().includes('price-check')) console.log(`  [http ${r.status()}]`, r.url().replace('http://localhost:5173', ''))
})

await page.goto('http://localhost:5173')
await page.waitForSelector('h1')
await page.waitForTimeout(2500)
await page.selectOption('select:below(:text("BREED"))', 'afghan-hound')
await page.waitForTimeout(2500)

const input = 'input[aria-label="Price you were quoted, in dollars"]'
for (const price of [369, 2238, 6000]) {
  console.log(`\n=== $${price}`)
  await page.fill(input, String(price))
  console.log('  input value after fill:', JSON.stringify(await page.inputValue(input)))
  await page.click('button:has-text("Check this price")')
  await page.waitForTimeout(2000)
  const count = await page.locator('[data-testid="price-verdict"]').count()
  console.log('  verdict elements:', count)
  if (count) {
    console.log('  classes:', await page.locator('[data-testid="price-verdict"]').getAttribute('class'))
    console.log('  text:', (await page.locator('[data-testid="price-verdict"]').innerText()).slice(0, 80))
  }
  if (await page.locator('p.text-error').count()) {
    console.log('  form error:', await page.locator('p.text-error').first().innerText())
  }
}

await browser.close()
