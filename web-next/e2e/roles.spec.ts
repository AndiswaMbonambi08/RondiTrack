import { expect, test } from './fixtures'
import { addMember, createCycle, createStokvel, tokens, uniqueName } from './helpers/api'

// Roles belong to a stokvel, not to a person. Thandiwe creates a stokvel (she is its Admin) and Sipho joins as an
// ordinary member there.
test('the UI is not the rule: a member who calls the treasurer route gets the API 403 and stays signed in', async ({ page }) => {
  const { sipho, thandiwe } = tokens()
  const stokvelId = await createStokvel(thandiwe.token, uniqueName('E2E Work'))
  const cycleId = await createCycle(thandiwe.token, stokvelId)
  await addMember(thandiwe.token, stokvelId, sipho.userId)

  await page.goto(`/stokvels/${stokvelId}`)
  await expect(page.getByRole('button', { name: /^close 2026-11$/i })).toHaveCount(0)
  await expect(page.getByRole('button', { name: /invite/i })).toHaveCount(0)

  // What a person with dev tools can still send: the close preview, from the signed-in page.
  const answer = await page.evaluate(async (url) => {
    const r = await fetch(url, { cache: 'no-store' })
    return { status: r.status, body: (await r.json().catch(() => null)) as unknown }
  }, `/bff/stokvels/${stokvelId}/cycles/${cycleId}/close-preview`)
  expect(answer.status).toBe(403)

  // Still signed in: the session works and the pages do not send him to /login.
  expect((await page.request.get('/bff/activity')).status()).toBe(200)
  await page.goto(`/stokvels/${stokvelId}`)
  await expect(page).not.toHaveURL(/\/login/)
})