'use server'

import { redirect } from 'next/navigation'
import { publicApi } from '@/lib/api'
import { readRefreshToken } from '@/lib/refresh'
import { safeNext } from '@/lib/safeNext'
import { createSession, deleteSession, getSession } from '@/lib/session'

export type LoginState = { error: string; correlationId?: string; email?: string } | null

export type RegisterState = {
  error?: string
  correlationId?: string
  fieldErrors?: Partial<Record<'fullName' | 'email' | 'password', string>>
  values: { fullName: string; email: string } // never the password
} | null

// Signs in with the API and writes the session cookie. Returns an error state, or null on success.
async function startSession(email: string, password: string): Promise<LoginState> {
  const api = publicApi()
  const signIn = await api.POST('/api/v1/auth/login', { body: { email, password } })

  if (!signIn.response.ok) {
    const problem = (signIn.error ?? {}) as { detail?: string; correlationId?: string }
    const error =
      signIn.response.status === 401
        ? 'That email or password is not right.'
        : signIn.response.status === 429
          ? 'Too many sign-in attempts. Wait a moment and try again.'
          : (problem.detail ?? 'We could not sign you in. Try again.')
    return { error, correlationId: problem.correlationId, email }
  }

  const accessToken = signIn.data?.accessToken
  const expiresInSeconds = signIn.data?.expiresInSeconds
  const refreshToken = readRefreshToken(signIn.response.headers.getSetCookie())
  if (!accessToken || !expiresInSeconds || !refreshToken) {
    return { error: 'We could not complete your sign-in. Try again.', email }
  }

  const me = await api.GET('/api/v1/me', { headers: { Authorization: `Bearer ${accessToken}` } })
  if (!me.response.ok || !me.data) return { error: 'We could not load your account. Try again.', email }

  await createSession({
    accessToken,
    refreshToken,
    accessExpiresAt: Math.floor(Date.now() / 1000) + expiresInSeconds,
    userId: me.data.id,
    email: me.data.email,
    fullName: me.data.fullName,
  })
  return null
}

export async function login(_prev: LoginState, formData: FormData): Promise<LoginState> {
  const email = String(formData.get('email') ?? '').trim()
  const password = String(formData.get('password') ?? '')
  const next = safeNext(String(formData.get('next') ?? ''))
  if (!email || !password) return { error: 'Enter your email and password.', email }

  const failed = await startSession(email, password)
  if (failed) return failed

  // redirect() works by throwing, so it stays outside any try/catch.
  redirect(next)
}

export async function register(_prev: RegisterState, formData: FormData): Promise<RegisterState> {
  const fullName = String(formData.get('fullName') ?? '').trim()
  const email = String(formData.get('email') ?? '').trim()
  const password = String(formData.get('password') ?? '')
  const values = { fullName, email }

  const fieldErrors: NonNullable<RegisterState>['fieldErrors'] = {}
  if (!fullName) fieldErrors.fullName = 'Enter your full name.'
  if (!/^\S+@\S+\.\S+$/.test(email)) fieldErrors.email = 'Enter a valid email address.'
  if (password.length < 12) fieldErrors.password = 'Use at least 12 characters.'
  if (Object.keys(fieldErrors).length > 0) return { fieldErrors, values }

  const api = publicApi()
  const created = await api.POST('/api/v1/auth/register', { body: { fullName, email, password } })

  if (!created.response.ok) {
    const problem = (created.error ?? {}) as {
      correlationId?: string
      errors?: Record<string, string[]>
    }
    const reference = problem.correlationId

    // 409 (email already registered): say nothing that confirms someone uses RondiTrack.
    if (created.response.status === 409) {
      return {
        error:
          "We couldn't create an account with those details. If you already have an account, sign in instead.",
        correlationId: reference,
        values,
      }
    }
    if (created.response.status === 400 && problem.errors) {
      const mapped: NonNullable<RegisterState>['fieldErrors'] = {}
      for (const [rawKey, messages] of Object.entries(problem.errors)) {
        const key = (rawKey.charAt(0).toLowerCase() + rawKey.slice(1)) as 'fullName' | 'email' | 'password'
        if (key === 'fullName' || key === 'email' || key === 'password') mapped[key] = messages.join(' ')
      }
      return { error: 'Check the form.', fieldErrors: mapped, correlationId: reference, values }
    }
    if (created.response.status === 429) {
      return { error: 'Too many attempts. Wait a moment and try again.', correlationId: reference, values }
    }
    return { error: 'We could not create your account. Try again.', correlationId: reference, values }
  }

  // A successful registration signs the person in.
  const failed = await startSession(email, password)
  if (failed) {
    return {
      error: 'Your account was created, but we could not sign you in. Go to Sign in and use your new details.',
      correlationId: failed.correlationId,
      values,
    }
  }
  redirect('/dashboard/member')
}

export async function logout(): Promise<void> {
  const session = await getSession()
  if (session) {
    try {
      await publicApi().POST('/api/v1/auth/logout', {
        headers: {
          Authorization: `Bearer ${session.accessToken}`,
          Cookie: `refresh_token=${session.refreshToken}`,
        },
      })
    } catch {
      // Signing out locally still has to work if the API is down.
    }
  }
  await deleteSession()
  redirect('/login')
}