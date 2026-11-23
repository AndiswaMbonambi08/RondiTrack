import { expect, request as playwrightRequest, test as plainTest } from '@playwright/test'
import { test } from './fixtures'
import { createInvite, createStokvel, tokens } from './helpers/api'

const BASE = 'http://localhost:3000'

// ---- signed-in pages: noindex, set once in the signed-in layout ----

test('a signed-in page is noindex', async ({ page }) => {
  await page.goto('/stokvels')
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', /noindex/)
})

test('/bff/ routes carry X-Robots-Tag, and robots.txt does not Disallow the signed-in pages', async ({ page }) => {
  const bff = await page.request.get('/bff/activity')
  expect(bff.headers()['x-robots-tag']).toMatch(/noindex/)

  // A crawler that may not fetch a page never sees its noindex.
  const robots = await (await page.request.get('/robots.txt')).text()
  expect(robots).not.toMatch(/disallow\s*:\s*\S/i)
})

// ---- the invite page, signed out, as a link-preview bot sees it ----

function metaTags(html: string): { key: string; content: string }[] {
  return [...html.matchAll(/<meta\s+[^>]*>/gi)].map((m) => {
    const tag = m[0]
    const key = /(?:property|name)="([^"]+)"/i.exec(tag)?.[1] ?? ''
    const content = /content="([^"]*)"/i.exec(tag)?.[1] ?? ''
    return { key, content }
  })
}

plainTest('a signed-out invite shows neither the stokvel name nor the amount, in any tag a bot reads', async () => {
  const { thandiwe } = tokens()
  const secretName = `Zulu Secret Stokvel ${Date.now()}`
  const stokvelId = await createStokvel(thandiwe.token, secretName, 7123)
  const code = await createInvite(thandiwe.token, stokvelId)

  const anon = await playwrightRequest.newContext({
    baseURL: BASE,
    extraHTTPHeaders: { 'user-agent': 'WhatsApp/2' },
  })
  const res = await anon.get(`/join?code=${code}`, { maxRedirects: 0 })
  expect(res.status()).toBe(200) // no redirect to /login: the bot gets the invite page itself
  const html = await res.text()
  await anon.dispose()

  const head = /<head[\s\S]*?<\/head>/i.exec(html)?.[0] ?? ''
  const title = /<title>([^<]*)<\/title>/i.exec(head)?.[1] ?? ''
  const tags = metaTags(head).filter((t) => t.key === 'description' || t.key.startsWith('og:'))

  expect(title).toMatch(/invited/i)
  expect(tags.some((t) => t.key === 'og:title')).toBe(true)

  const read = [title, ...tags.map((t) => t.content)].join('\n')
  expect(read).not.toContain(secretName)
  expect(read).not.toMatch(/7\s?123/)
  expect(read).not.toMatch(/R\s?\d/)
  // The name is in no part of the page at all: the signed-out page never asks the API about the invite.
  expect(html).not.toContain(secretName)

  expect(tags.find((t) => t.key === 'robots')?.content ?? head).toMatch(/noindex/)
})