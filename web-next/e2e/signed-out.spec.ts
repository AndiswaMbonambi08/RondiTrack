import { expect, test } from '@playwright/test'

// No saved session at all.
test.use({ storageState: { cookies: [], origins: [] } })

test('a signed-out visit to a signed-in page lands on /login with the way back', async ({ page }) => {
  await page.goto('/stokvels')
  await expect(page).toHaveURL(/\/login\?.*next=/)
  expect(new URL(page.url()).searchParams.get('next')).toBe('/stokvels')
})

test('a signed-out /bff/ fetch gets a JSON 401, not HTML', async ({ request }) => {
  const res = await request.get('/bff/activity', { maxRedirects: 0 })
  expect(res.status()).toBe(401)
  expect(res.headers()['content-type']).toContain('application/json')
  expect(() => JSON.parse(String.raw`${'' + (res.ok() ? '' : '')}`) ?? null).not.toThrow()
  await expect(res.json()).resolves.toBeTruthy()
})