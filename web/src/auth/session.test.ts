import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { __resetSessionForTests, authFetch, login, onSignedOut } from './session'

const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms))
const json = (body: unknown) =>
  new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })

function installFakeApi(opts: { refreshFails?: boolean } = {}) {
  const state = { valid: 'new', refreshCalls: 0, protectedCalls: 0, bodies: [] as string[] }
  const fake = async (input: RequestInfo | URL, init?: RequestInit) => {
    // openapi-fetch passes a Request; handle both shapes
    const req = input instanceof Request ? input : new Request(input, init)
    const { pathname } = new URL(req.url)
    if (pathname.endsWith('/auth/login')) return json({ accessToken: 'old' })
    if (pathname.endsWith('/auth/refresh')) {
      state.refreshCalls++
      await sleep(20) // without this delay the race never happens
      return opts.refreshFails ? new Response(null, { status: 401 }) : json({ accessToken: state.valid })
    }
    state.protectedCalls++
    state.bodies.push(await req.text())
    return req.headers.get('Authorization') === `Bearer ${state.valid}`
      ? json({ ok: true })
      : new Response(null, { status: 401 })
  }
  vi.stubGlobal('fetch', vi.fn(fake))
  return state
}

const url = 'http://localhost:5051/api/v1/things'
const fetchFive = () => Promise.all(Array.from({ length: 5 }, () => authFetch(new Request(url))))

beforeEach(() => __resetSessionForTests())
afterEach(() => vi.unstubAllGlobals())

describe('session', () => {
  it('five parallel 401s cause exactly one refresh', async () => {
    const api = installFakeApi()
    await login('a@b.co', 'pw')
    const responses = await fetchFive()
    expect(api.refreshCalls).toBe(1)
    expect(responses.map((r) => r.status)).toEqual([200, 200, 200, 200, 200])
  })

  it('a later expiry starts a new refresh', async () => {
    const api = installFakeApi()
    await login('a@b.co', 'pw')
    await fetchFive()
    api.valid = 'newer' // the token held now has expired
    const later = await Promise.all([authFetch(new Request(url)), authFetch(new Request(url))])
    expect(api.refreshCalls).toBe(2)
    expect(later.map((r) => r.status)).toEqual([200, 200])
  })

  it('a failed refresh signs out once and retries nothing', async () => {
    const api = installFakeApi({ refreshFails: true })
    await login('a@b.co', 'pw')
    const signedOut = vi.fn()
    onSignedOut(signedOut)
    const responses = await Promise.all([1, 2, 3].map(() => authFetch(new Request(url))))
    expect(signedOut).toHaveBeenCalledTimes(1)
    expect(api.refreshCalls).toBe(1)
    expect(api.protectedCalls).toBe(3) // each request sent once, none retried
    expect(responses.map((r) => r.status)).toEqual([401, 401, 401])
  })

  it('no token means no loop', async () => {
    const api = installFakeApi({ refreshFails: true })
    const res = await authFetch(new Request(url))
    expect(res.status).toBe(401)
    expect(api.refreshCalls).toBe(1)
    expect(api.protectedCalls).toBe(1)
  })

  it('a POST body survives the retry', async () => {
    const api = installFakeApi()
    await login('a@b.co', 'pw')
    const res = await authFetch(
      new Request(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ amountInCents: 50000 }),
      }),
    )
    expect(res.status).toBe(200)
    expect(api.bodies).toEqual(['{"amountInCents":50000}', '{"amountInCents":50000}'])
  })
})