import createClient from 'openapi-fetch'
import type { paths } from './schema'
import { authFetch } from '../auth/session'
import { ApiError, type Problem } from './apiError'
import { parseRetryAfter } from '../lib/retryAfter'

export const api = createClient<paths>({
  baseUrl: import.meta.env.VITE_API_BASE_URL,
  fetch: authFetch,
})

/** Returns the data or throws an ApiError (status, problem+json body, Retry-After seconds). */
export async function unwrap<T>(
  call: Promise<{ data?: T; error?: unknown; response: Response }>,
): Promise<T> {
  const { data, error, response } = await call
  if (!response.ok || error !== undefined) {
    throw new ApiError(
      response.status,
      (error as Problem | undefined) ?? null,
      parseRetryAfter(response.headers.get('Retry-After')),
    )
  }
  return data as T
}