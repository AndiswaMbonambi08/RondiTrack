import { ApiError } from '../api/apiError'

export type ErrorKind = 'notFound' | 'rateLimited' | 'unauthenticated' | 'validation' | 'network' | 'generic'

export interface ErrorDescription {
  kind: ErrorKind
  title: string
  message: string
  reference?: string            // correlationId; absent when the API never answered
  retryAfter?: number           // seconds
  fieldErrors?: Record<string, string[]>
}

export const waitMessage = (seconds: number | null) =>
  seconds === null
    ? 'Too many requests. Wait a little, then try again.'
    : `Too many requests. Try again in ${seconds} ${seconds === 1 ? 'second' : 'seconds'}.`

/** `context: 'login'` is only for the sign-in form, where a 401 means bad credentials. */
export function describeError(error: unknown, context?: 'login'): ErrorDescription {
  if (!(error instanceof ApiError)) {
    return { kind: 'network', title: 'No connection', message: "Can't reach RondiTrack. Check your connection." }
  }
  const problem = error.problem as (typeof error.problem & { errors?: Record<string, string[]> }) | null
  const reference = problem?.correlationId ?? undefined

  if (error.status === 429) {
    return { kind: 'rateLimited', title: 'Slow down', message: waitMessage(error.retryAfter), reference, retryAfter: error.retryAfter ?? undefined }
  }
  if (error.status === 401) {
    return context === 'login'
      ? { kind: 'unauthenticated', title: 'Sign-in failed', message: 'Email or password is incorrect.', reference }
      : { kind: 'unauthenticated', title: 'Session ended', message: 'Please sign in again.', reference }
  }
  if (error.status === 404) {
    return { kind: 'notFound', title: 'Stokvel not found', message: "This stokvel doesn't exist, or you're not a member of it.", reference }
  }
  if (error.status === 400 && problem?.errors) {
    return { kind: 'validation', title: 'Check the form', message: 'Some fields need fixing.', reference, fieldErrors: problem.errors }
  }
  // 500 and the rest: never show problem.detail, it can contain internals
  return { kind: 'generic', title: 'Something went wrong', message: 'Something went wrong on our side. Try again, and quote the reference if it keeps happening.', reference }
}