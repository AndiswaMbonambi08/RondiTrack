// Test data goes straight to the API with the person's own token. Every test makes its own stokvel with a
// unique name, so no test depends on another or on what an earlier run left behind. The only shared things
// are the seeded accounts.
import fs from 'node:fs'
import { randomUUID } from 'node:crypto'
import { API_TOKENS } from '../paths'

export const API_URL = process.env.E2E_API_URL ?? 'http://localhost:5051'

export type Login = { token: string; userId: string }
export type Tokens = { sipho: Login; thandiwe: Login }

function demoPassword(): string {
  const p = process.env.SEED_DEMO_PASSWORD
  if (!p) throw new Error('Set SEED_DEMO_PASSWORD (the demo password the API was seeded with).')
  return p
}

function subjectOf(token: string): string {
  const payload = JSON.parse(Buffer.from(token.split('.')[1], 'base64url').toString('utf8')) as { sub?: string }
  if (!payload.sub) throw new Error('The access token has no "sub" claim.')
  return payload.sub
}

// The API allows 5 logins per minute per the RateLimit:Login setting. One run uses 3 (two here, one in the browser).
export async function apiLogin(email: string): Promise<Login> {
  const res = await fetch(`${API_URL}/api/v1/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password: demoPassword() }),
  })
  if (res.status === 429) {
    throw new Error(`Login rate limit reached. Wait ${res.headers.get('Retry-After') ?? '60'} seconds and run again.`)
  }
  if (!res.ok) throw new Error(`Login for ${email} failed with ${res.status}.`)
  const body = (await res.json()) as Record<string, unknown>
  const token = [body.accessToken, body.token, body.access_token].find((v): v is string => typeof v === 'string')
  if (!token) throw new Error(`No access token in the login answer. Keys: ${Object.keys(body).join(', ')}`)
  return { token, userId: subjectOf(token) }
}

export function tokens(): Tokens {
  if (!fs.existsSync(API_TOKENS)) throw new Error('Run the setup project first: npx playwright test --project=setup')
  return JSON.parse(fs.readFileSync(API_TOKENS, 'utf8')) as Tokens
}

async function call<T>(
  token: string,
  method: string,
  path: string,
  body?: unknown,
  headers: Record<string, string> = {},
): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...headers,
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })
  if (!res.ok) throw new Error(`${method} ${path} -> ${res.status} ${await res.text()}`)
  return (res.status === 204 ? undefined : await res.json()) as T
}

export const uniqueName = (prefix: string) => `${prefix} ${randomUUID().slice(0, 8)}`

export async function createStokvel(token: string, name: string, contributionAmount = 500): Promise<string> {
  return (await call<{ id: string }>(token, 'POST', '/api/v1/stokvels', { name, contributionAmount })).id
}

export async function createCycle(token: string, stokvelId: string, label = '2026-11'): Promise<string> {
  return (
    await call<{ id: string }>(token, 'POST', '/api/v1/contribution-cycles', { stokvelId, label, targetAmount: 500 })
  ).id
}

export async function addMember(token: string, stokvelId: string, userId: string): Promise<void> {
  await call(token, 'POST', `/api/v1/stokvels/${stokvelId}/members`, { userId })
}

export async function recordContribution(
  token: string,
  stokvelId: string,
  cycleId: string,
  userId: string,
  amountInCents: number,
): Promise<void> {
  await call(
    token,
    'POST',
    `/api/v1/stokvels/${stokvelId}/contributions`,
    { userId, contributionCycleId: cycleId, amountInCents },
    { 'Idempotency-Key': randomUUID() },
  )
}

export async function createInvite(token: string, stokvelId: string): Promise<string> {
  return (await call<{ code: string }>(token, 'POST', `/api/v1/stokvels/${stokvelId}/invites`, { expiresInDays: 7 })).code
}

export async function collectedCents(token: string, stokvelId: string): Promise<number> {
  const d = await call<{ currentCycle: { collectedCents: number } | null }>(token, 'GET', `/api/v1/stokvels/${stokvelId}`)
  return d.currentCycle?.collectedCents ?? 0
}

// Sipho creates a stokvel (so he is its Admin) with an open cycle. Thandiwe joins it, and has already paid
// R 200,00, so the total is not zero and "went up by exactly R 150,50" cannot be a coincidence.
export async function arrangeAdminStokvel(options: { thandiwePaid: boolean }) {
  const { sipho, thandiwe } = tokens()
  const name = uniqueName('E2E Admin')
  const stokvelId = await createStokvel(sipho.token, name)
  const cycleId = await createCycle(sipho.token, stokvelId)
  await addMember(sipho.token, stokvelId, thandiwe.userId)
  if (options.thandiwePaid) await recordContribution(sipho.token, stokvelId, cycleId, thandiwe.userId, 20000)
  return { stokvelId, cycleId, name, sipho, thandiwe }
}