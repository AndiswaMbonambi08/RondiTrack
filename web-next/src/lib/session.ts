import 'server-only'
import { cookies } from 'next/headers'
import {
  decryptSession,
  encryptSession,
  SESSION_COOKIE,
  sessionCookieOptions,
  type Session,
} from './sessionCookie'

export type { Session }

// Server Actions and Route Handlers only: a Server Component cannot set a cookie.
export async function createSession(session: Session): Promise<void> {
  const token = await encryptSession(session)
  const jar = await cookies()
  jar.set(SESSION_COOKIE, token, sessionCookieOptions)
}

export async function getSession(): Promise<Session | null> {
  const jar = await cookies()
  const token = jar.get(SESSION_COOKIE)?.value
  return token ? decryptSession(token) : null
}

export async function deleteSession(): Promise<void> {
  const jar = await cookies()
  jar.delete(SESSION_COOKIE)
}