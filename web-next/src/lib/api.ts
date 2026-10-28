import 'server-only'
import createClient from 'openapi-fetch'
import { notFound, redirect } from 'next/navigation'
import { ApiError, type Problem } from './apiError'
import { parseRetryAfter } from './retryAfter'
import type { paths } from './schema'
import { getSession } from './session'

function baseUrl(): string {
  const url = process.env.API_BASE_URL
  if (!url) throw new Error('API_BASE_URL is not set')
  return url
}

const noStore: typeof fetch = (input, init) => fetch(input, { ...init, cache: 'no-store' })

// For calls that need no session (sign in).
export function publicApi() {
  return createClient<paths>({ baseUrl: baseUrl(), fetch: noStore })
}

// For everything else: adds the Bearer token from the session, or sends the person to /login.
export async function signedInApi() {
  const session = await getSession()
  if (!session) redirect('/login')

  const api = createClient<paths>({
    baseUrl: baseUrl(),
    fetch: noStore,
    headers: { Authorization: `Bearer ${session.accessToken}` },
  })
  return { api, session }
}

type Result<T> = { data?: T; error?: unknown; response: Response }

// One place that turns an API result into data or the right response:
// 401 -> /login?expired=1, 404 -> notFound(), anything else -> an ApiError the page can show.
// redirect() and notFound() work by throwing, so never call this inside try/catch.
export function readOrFail<T>(result: Result<T>): T | ApiError {
  const { data, error, response } = result

  if (response.ok && data !== undefined) return data
  if (response.status === 401) redirect('/login?expired=1')
  if (response.status === 404) notFound()

  return new ApiError(
    response.status,
    (typeof error === 'object' ? error : null) as Problem | null,
    parseRetryAfter(response.headers.get('Retry-After')),
  )
}