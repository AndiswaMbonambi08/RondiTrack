// The only code that calls POST /auth/refresh. It is called only from proxy.ts.
// No 'server-only' (the proxy bundle imports it), and it never runs in the browser.

export type RefreshOutcome =
  | { ok: true; accessToken: string; refreshToken: string; accessExpiresAt: number }
  | { ok: false } // the API refused: this session is over

// The API sends the refresh token as a Set-Cookie header. A browser's fetch cannot read it,
// but this server can.
export function readRefreshToken(setCookies: string[]): string | null {
  const prefix = 'refresh_token='
  const line = setCookies.find((c) => c.startsWith(prefix))
  return line ? line.split(';')[0].slice(prefix.length) : null
}

async function callApi(refreshToken: string): Promise<RefreshOutcome> {
  const base = process.env.API_BASE_URL
  if (!base) throw new Error('API_BASE_URL is not set')

  const res = await fetch(`${base}/api/v1/auth/refresh`, {
    method: 'POST',
    headers: { Cookie: `refresh_token=${refreshToken}` },
    cache: 'no-store',
  })
  if (res.status === 401) return { ok: false }
  // Anything else that is not a success (5xx, 429) is not a verdict on the session: throw, keep it.
  if (!res.ok) throw new Error(`refresh answered ${res.status}`)

  const body = (await res.json()) as { accessToken?: string; expiresInSeconds?: number }
  const next = readRefreshToken(res.headers.getSetCookie())
  if (!body.accessToken || !body.expiresInSeconds || !next) throw new Error('refresh answer was incomplete')

  return {
    ok: true,
    accessToken: body.accessToken,
    refreshToken: next,
    accessExpiresAt: Math.floor(Date.now() / 1000) + body.expiresInSeconds,
  }
}

// THE RACE FIX. The API revokes the whole token family when a spent refresh token comes back,
// so three tabs refreshing with the same token at once would sign the person out.
// Here, everyone who arrives with the same refresh token shares ONE call to the API:
//  - while the call is in flight, they all wait for the same promise;
//  - for MEMO_MS after it finished, a request that left the browser before the new cookie
//    arrived (a slow tab) still gets the same answer instead of replaying a spent token.
// This lives in this server process's memory only. Tokens are never logged.
const MEMO_MS = 20_000
type Entry = { promise: Promise<RefreshOutcome>; settledAt: number | null }
const entries = new Map<string, Entry>()

export function refreshOnce(refreshToken: string): Promise<RefreshOutcome> {
  const now = Date.now()
  for (const [key, entry] of entries) {
    if (entry.settledAt !== null && now - entry.settledAt > MEMO_MS) entries.delete(key)
  }

  const existing = entries.get(refreshToken)
  if (existing) {
    console.log('[refresh] shared an existing refresh instead of calling the API again')
    return existing.promise
  }

  const entry: Entry = {
    settledAt: null,
    promise: callApi(refreshToken).then(
      (outcome) => {
        entry.settledAt = Date.now()
        console.log(`[refresh] ${outcome.ok ? 'rotated' : 'refused'} at ${new Date().toISOString()}`)
        return outcome
      },
      (error) => {
        entries.delete(refreshToken) // a network failure is not memoised: the next request may try again
        throw error
      },
    ),
  }
  entries.set(refreshToken, entry)
  return entry.promise
}