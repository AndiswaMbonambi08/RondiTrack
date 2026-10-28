'use server'

import { redirect } from 'next/navigation'
import { publicApi } from '@/lib/api'
import { createSession, deleteSession, getSession } from '@/lib/session'

export type LoginState = { error: string; correlationId?: string } | null

// The API sends the refresh token as a Set-Cookie header. A browser's fetch cannot read it,
// but this server can.
function readRefreshToken(setCookies: string[]): string | null {
  const prefix = 'refresh_token='
  const line = setCookies.find((c) => c.startsWith(prefix))
  return line ? line.split(';')[0].slice(prefix.length) : null
}

export async function login(_prev: LoginState, formData: FormData): Promise<LoginState> {
  const email = String(formData.get('email') ?? '').trim()
  const password = String(formData.get('password') ?? '')
  if (!email || !password) return { error: 'Enter your email and password.' }

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
    return { error, correlationId: problem.correlationId }
  }

  const accessToken = signIn.data?.accessToken
  const refreshToken = readRefreshToken(signIn.response.headers.getSetCookie())
  if (!accessToken || !refreshToken) return { error: 'We could not complete your sign-in. Try again.' }

  const me = await api.GET('/api/v1/me', { headers: { Authorization: `Bearer ${accessToken}` } })
  if (!me.response.ok || !me.data) return { error: 'We could not load your account. Try again.' }

  await createSession({
    accessToken,
    refreshToken,
    userId: me.data.id,
    email: me.data.email,
    fullName: me.data.fullName,
  })

  // redirect() works by throwing, so it stays outside any try/catch.
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
