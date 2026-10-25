import { StrictMode, type ReactNode } from 'react'
import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { act, renderHook } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import { ApiError } from '../api/apiError'
import { __resetSessionForTests, login } from '../auth/session'
import { parseRandsToCents } from '../money/money'
import { describeContributionError } from './contributionErrors'
import { useRecordContribution } from './useRecordContribution'

const API = 'http://localhost:5051'
const STOKVEL = '11111111-1111-4111-8111-111111111111'
const CYCLE = '22222222-2222-4222-8222-222222222222'
const MEMBER = '33333333-3333-4333-8333-333333333333'

const recorded = {
  id: '44444444-4444-4444-8444-444444444444',
  stokvelId: STOKVEL,
  userId: MEMBER,
  contributionCycleId: CYCLE,
  amount: 150.5,
  recordedAt: '2026-10-26T08:00:00Z',
  amountCents: 15050,
  status: 'Paid',
}

interface Seen {
  key: string | null
  authorization: string | null
  body: { userId: string; contributionCycleId: string; amountInCents: unknown }
}

let seen: Seen[] = []
let respond: (attempt: number) => Response = () => created()

const created = () => HttpResponse.json(recorded, { status: 201 })
const problem = (status: number, headers: Record<string, string> = {}) =>
  HttpResponse.json(
    { title: 'Problem', status, correlationId: 'corr-123' },
    { status, headers: { 'Content-Type': 'application/problem+json', ...headers } },
  )

const server = setupServer(
  http.post(`${API}/api/v1/auth/login`, () => HttpResponse.json({ accessToken: 'token-1', expiresInSeconds: 900 })),
  http.post(`${API}/api/v1/auth/refresh`, () => HttpResponse.json({ accessToken: 'token-2', expiresInSeconds: 900 })),
  http.post(`${API}/api/v1/stokvels/:id/contributions`, async ({ request }) => {
    seen.push({
      key: request.headers.get('Idempotency-Key'),
      authorization: request.headers.get('Authorization'),
      body: (await request.json()) as Seen['body'],
    })
    return respond(seen.length)
  }),
)

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
afterEach(() => server.resetHandlers())
afterAll(() => server.close())
beforeEach(async () => {
  __resetSessionForTests()
  seen = []
  respond = () => created()
  await login('a@b.co', 'pw')
})

function cents(text: string): number {
  const value = parseRandsToCents(text)
  if (value === null) throw new Error(`test setup: ${text} is not a valid amount`)
  return value
}
const pay = (text = '150.50') => ({ userId: MEMBER, cycleId: CYCLE, amountInCents: cents(text) })

function setup(strict = false) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  const wrapper = ({ children }: { children: ReactNode }) => {
    const inner = <QueryClientProvider client={client}>{children}</QueryClientProvider>
    return strict ? <StrictMode>{inner}</StrictMode> : inner
  }
  return renderHook(() => useRecordContribution(STOKVEL), { wrapper })
}

type Hook = ReturnType<typeof setup>
async function attempt(hook: Hook, payment: ReturnType<typeof pay>): Promise<unknown> {
  let outcome: unknown
  await act(async () => {
    try {
      outcome = await hook.result.current.record(payment)
    } catch (error) {
      outcome = error
    }
  })
  return outcome
}

describe('recording a contribution', () => {
  it('a retry after a network error sends the same key', async () => {
    respond = (n) => (n === 1 ? HttpResponse.error() : created())
    const hook = setup()

    const first = await attempt(hook, pay())
    expect(describeContributionError(first)).toMatchObject({ kind: 'network', mayHaveLanded: true })
    expect(describeContributionError(first).message).toMatch(/may have gone through/)

    const second = await attempt(hook, pay())
    expect(second).toMatchObject({ id: recorded.id })

    expect(seen).toHaveLength(2)
    expect(seen[0].key).not.toBeNull()
    expect(seen[1].key).not.toBeNull()
    expect(seen[0].key).toBe(seen[1].key)
  })

  it('the next contribution gets a new key', async () => {
    const hook = setup()
    await attempt(hook, pay())
    await attempt(hook, pay()) // same member, cycle and amount, but the first one succeeded
    expect(seen).toHaveLength(2)
    expect(seen[0].key).not.toBe(seen[1].key)
  })

  it('a 429 keeps the key and exposes the wait', async () => {
    respond = (n) => (n === 1 ? problem(429, { 'Retry-After': '1' }) : created())
    const hook = setup()

    const first = await attempt(hook, pay())
    expect(first).toBeInstanceOf(ApiError)
    expect((first as ApiError).retryAfter).toBe(1)
    expect(describeContributionError(first)).toMatchObject({
      kind: 'rateLimited', retryAfter: 1, reference: 'corr-123', mayHaveLanded: false,
    })

    await attempt(hook, pay())
    expect(seen).toHaveLength(2)
    expect(seen[0].key).toBe(seen[1].key)
  })

  it('changing the amount after a failure is a new intent', async () => {
    respond = (n) => (n === 1 ? HttpResponse.error() : created())
    const hook = setup()

    await attempt(hook, pay('150.50'))
    await attempt(hook, pay('200'))

    expect(seen).toHaveLength(2)
    expect(seen[0].key).not.toBe(seen[1].key)
    expect(seen[1].body.amountInCents).toBe(20000)
  })

  it('a 401 retry keeps the key and carries the new token', async () => {
    respond = (n) => (n === 1 ? problem(401) : created())
    const hook = setup()

    const result = await attempt(hook, pay())
    expect(result).toMatchObject({ id: recorded.id })

    expect(seen).toHaveLength(2)
    expect(seen[0].key).not.toBeNull()
    expect(seen[0].key).toBe(seen[1].key)
    expect(seen[0].authorization).toBe('Bearer token-1')
    expect(seen[1].authorization).toBe('Bearer token-2')
  })

  it('cents survive the trip: "150.50" is sent as the number 15050', async () => {
    const hook = setup()
    await attempt(hook, pay('150.50'))

    expect(seen).toHaveLength(1)
    expect(seen[0].body.amountInCents).toBe(15050)
    expect(typeof seen[0].body.amountInCents).toBe('number')
    expect(seen[0].body).toMatchObject({ userId: MEMBER, contributionCycleId: CYCLE })
  })

  it('StrictMode does not produce a second key for one intent', async () => {
    respond = (n) => (n === 1 ? HttpResponse.error() : created())
    const hook = setup(true)

    await attempt(hook, pay())
    await attempt(hook, pay())

    expect(seen).toHaveLength(2)
    expect(seen[0].key).toBe(seen[1].key)
  })
})
