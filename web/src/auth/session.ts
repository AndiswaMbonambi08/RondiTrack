/**
 * The ONLY file in the project that reads or writes an access token.
 *
 * Why refresh must be single-flight for this API: the Week 6 API rotates the refresh cookie on
 * every /auth/refresh and detects reuse. If five requests fail together and each sends its own
 * refresh with the same cookie, the first rotates it and the other four present a spent token,
 * which the API answers with an error or treats as theft. One shared refresh per tab means the
 * cookie is only ever presented once. (Edit this to match what YOUR check 5 showed.)
 */
import { ApiError } from '../api/apiError'

const baseUrl = import.meta.env.VITE_API_BASE_URL as string

let accessToken: string | null = null          // memory only, never any storage
let refreshing: Promise<string | null> | null = null
const listeners = new Set<() => void>()

export function onSignedOut(listener: () => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

function notifySignedOut() {
  listeners.forEach((l) => l())
}

export async function login(email: string, password: string): Promise<void> {
  const res = await fetch(`${baseUrl}/api/v1/auth/login`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  if (!res.ok) throw await ApiError.fromResponse(res)
  accessToken = ((await res.json()) as { accessToken: string }).accessToken
}

export async function logout(): Promise<void> {
  const token = accessToken
  accessToken = null
  try {
    await fetch(`${baseUrl}/api/v1/auth/logout`, {
      method: 'POST',
      credentials: 'include',
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    })
  } catch {
    // the member is signed out locally whatever the network did
  } finally {
    notifySignedOut()
  }
}

async function doRefresh(): Promise<string | null> {
  const hadSession = accessToken !== null
  const res = await fetch(`${baseUrl}/api/v1/auth/refresh`, { method: 'POST', credentials: 'include' })
  if (!res.ok) {
    accessToken = null
    if (hadSession) notifySignedOut() // a first-time visitor was never signed in
    return null
  }
  accessToken = ((await res.json()) as { accessToken: string }).accessToken
  return accessToken
}

export function refreshAccessToken(): Promise<string | null> {
  refreshing ??= doRefresh().finally(() => {
    refreshing = null
  })
  return refreshing
}

function withToken(request: Request, token: string | null): Request {
  if (!token) return request
  const headers = new Headers(request.headers)
  headers.set('Authorization', `Bearer ${token}`)
  return new Request(request, { headers })
}

/** Hand this to openapi-fetch as `fetch`. It is called with a Request object. */
export async function authFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const request = new Request(input, init)
  if (!accessToken) await refreshAccessToken()      // after a reload, restore from the cookie
  const usedToken = accessToken
  const retryCopy = request.clone()                 // a body can only be read once
  const first = await fetch(withToken(request, usedToken))

  if (first.status !== 401 || !usedToken) return first // no token to begin with: never loop

  const token =
    accessToken && accessToken !== usedToken ? accessToken : await refreshAccessToken()
  if (!token) return first                          // refresh failed, listeners already told

  return fetch(withToken(retryCopy, token))         // one retry, whatever it returns
}

/** Tests only. */
export function __resetSessionForTests() {
  accessToken = null
  refreshing = null
  listeners.clear()
}