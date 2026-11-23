import AxeBuilder from '@axe-core/playwright'
import type { Page } from '@playwright/test'
import { expect, test } from './fixtures'
import { arrangeAdminStokvel } from './helpers/api'

const TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa']

// The ONE place a rule may be switched off, and only with a reason written beside it. Empty on purpose.
const DISABLED_RULES: string[] = []

async function expectNoSeriousViolations(page: Page) {
  const results = await new AxeBuilder({ page }).withTags(TAGS).disableRules(DISABLED_RULES).analyze()
  const serious = results.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical')
  expect(
    serious.map((v) => `${v.impact} ${v.id}: ${v.help} (${v.nodes.length} element(s))`),
    'serious or critical axe violations',
  ).toEqual([])
}

test('axe: the home screen', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByRole('heading').first()).toBeVisible()
  await expectNoSeriousViolations(page)
})

test('axe: a stokvel', async ({ page }) => {
  const a = await arrangeAdminStokvel({ thandiwePaid: true })
  await page.goto(`/stokvels/${a.stokvelId}`)
  await expect(page.getByText(a.name).first()).toBeVisible()
  await expectNoSeriousViolations(page)
})

test('axe: the contribution form showing its errors', async ({ page }) => {
  const a = await arrangeAdminStokvel({ thandiwePaid: false })
  await page.goto(`/stokvels/${a.stokvelId}`)
  const amount = page.getByLabel(/amount \(rands\)/i)
  await amount.fill('abc')
  await page.getByRole('button', { name: /record contribution/i }).click()
  await expect(page.getByText(/enter rands like/i)).toBeVisible()
  await expectNoSeriousViolations(page)
})

test('axe: the open Close Cycle dialog', async ({ page }) => {
  const a = await arrangeAdminStokvel({ thandiwePaid: false })
  await page.goto(`/stokvels/${a.stokvelId}`)
  await page.getByRole('button', { name: 'Close 2026-11', exact: true }).click()
  const dialog = page.getByRole('alertdialog')
  await expect(dialog).toBeVisible()
  // Wait for the preview, so the audit sees the real content and not the placeholder rows.
  await expect(dialog.getByRole('button', { name: /record|no arrears/i })).toBeEnabled()
  await expectNoSeriousViolations(page)
})