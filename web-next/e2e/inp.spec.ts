import { createRequire } from 'node:module'
import { expect, test } from './fixtures'
import { arrangeAdminStokvel } from './helpers/api'

const resolve = createRequire(__filename).resolve

// Measures INP with the web-vitals library (4x CPU slowdown) while recording a contribution and opening Close Cycle.
// Tagged @perf: it is a measurement, so CI skips it (npm run test:e2e:perf runs it).
test('INP stays under 200 ms while recording a contribution and opening Close Cycle @perf', async ({ page }) => {
  const a = await arrangeAdminStokvel({ thandiwePaid: true })
  await page.goto(`/stokvels/${a.stokvelId}`)
  await expect(page.getByLabel(/amount \(rands\)/i)).toBeVisible()

  const cdp = await page.context().newCDPSession(page)
  await cdp.send('Emulation.setCPUThrottlingRate', { rate: 4 })

  await page.addScriptTag({ path: resolve('web-vitals/dist/web-vitals.iife.js') })
  await page.evaluate(() => {
    const w = window as unknown as { __inp: number; webVitals: { onINP: (cb: (m: { value: number }) => void, o: object) => void } }
    w.__inp = 0
    w.webVitals.onINP((m) => { w.__inp = m.value }, { reportAllChanges: true })
  })

  await page.getByRole('combobox', { name: /member/i }).selectOption(a.sipho.userId)
  await page.getByLabel(/amount \(rands\)/i).fill('150,50')
  await page.getByRole('button', { name: /record contribution/i }).click()
  await expect(page.getByRole('status').filter({ hasText: /Recorded/ })).toBeVisible()

  await page.getByRole('button', { name: 'Close 2026-11', exact: true }).click()
  await expect(page.getByRole('alertdialog')).toBeVisible()
  await page.getByRole('button', { name: /cancel/i }).click()

  const read = () => page.evaluate(() => (window as unknown as { __inp: number }).__inp)
  await expect.poll(read).toBeGreaterThan(0)
  const inp = await read()
  console.log(`INP: ${inp} ms`)
  expect(inp).toBeLessThan(200)
})