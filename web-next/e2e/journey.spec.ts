import { expect, test } from './fixtures'
import { amountPattern } from './format'
import { arrangeAdminStokvel, collectedCents } from './helpers/api'

test('the journey: record R 150,50 and the total goes up by exactly R 150,50, without a reload', async ({ page }) => {
  const a = await arrangeAdminStokvel({ thandiwePaid: true })

  // Do not assume what the total was: ask the API.
  const before = await collectedCents(a.sipho.token, a.stokvelId)

  await page.goto(`/stokvels/${a.stokvelId}`)
  const amount = page.getByLabel(/amount \(rands\)/i)
  await expect(amount).toBeVisible()

  let loads = 0
  page.on('load', () => loads++)

  await page.getByRole('combobox', { name: /member/i }).selectOption(a.sipho.userId)
  await amount.fill('150,50')
  await page.getByRole('button', { name: /record contribution/i }).click()

  await expect(page.getByRole('status').filter({ hasText: /Recorded/ })).toContainText(amountPattern(15050))
  // The new total is on the page, and it is the old total plus exactly 15 050 cents.
  await expect(page.getByText(amountPattern(before + 15050)).first()).toBeVisible()
  await expect.poll(() => collectedCents(a.sipho.token, a.stokvelId)).toBe(before + 15050)
  expect(loads).toBe(0) // no full page load happened
})