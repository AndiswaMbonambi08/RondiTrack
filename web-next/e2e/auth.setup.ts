import fs from 'node:fs'
import { expect, test as setup } from '@playwright/test'
import { apiLogin } from './helpers/api'
import { API_TOKENS, AUTH_DIR, SIPHO_STATE } from './paths'

// Three logins in total, so the 5-per-minute limit is never reached.
setup('sign in once per person and save the sessions', async ({ page }) => {
  fs.mkdirSync(AUTH_DIR, { recursive: true })

  // Tokens for creating test data straight in the API (Thandiwe and Sipho).
  const sipho = await apiLogin('sipho@example.com')
  const thandiwe = await apiLogin('thandiwe@example.com')
  fs.writeFileSync(API_TOKENS, JSON.stringify({ sipho, thandiwe }))

  // Sipho in the browser, saved for every other test.
  await page.goto('/login')
  await page.getByLabel(/email/i).fill('sipho@example.com')
  await page.locator('input[type="password"]').fill(process.env.SEED_DEMO_PASSWORD ?? '')
  await page.getByRole('button', { name: /sign in|log in/i }).click()
  await expect(page).not.toHaveURL(/\/login/)
  await page.context().storageState({ path: SIPHO_STATE })
})